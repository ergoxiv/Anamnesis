// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor.Refresh;

using Anamnesis.Memory;
using Anamnesis.Services;
using RemoteController.Interop.Delegates;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

public enum RedrawStage
{
	Before,
	After,
}

public class AnamnesisActorRefresher : IActorRefresher
{
	public RedrawService RedrawService { get; } = new RedrawService();

	public bool CanRefresh(ActorMemory actor)
	{
		if (PoseService.Instance.IsEnabled)
			return false;

		// Ana can't refresh gpose actors
		//if (actor.IsGPoseActor)
		//	return false;

		// Ana can't refresh world frozen actors
		if (PoseService.Instance.FreezeWorldPosition)
			return false;

		return true;
	}

	public async Task RefreshActor(ActorMemory actor)
	{
		//if (SettingsService.Current.EnableNpcHack && actor.ObjectKind == ActorTypes.Player)
		//{
		//	actor.ObjectKind = ActorTypes.EventNpc;
		//	actor.RenderMode = RenderModes.Unload;
		//	await Task.Delay(75);
		//	actor.RenderMode = RenderModes.Draw;
		//	await Task.Delay(75);
		//	actor.ObjectKind = ActorTypes.Player;
		//	actor.RenderMode = RenderModes.Draw;
		//}
		//else
		//{
		//	actor.RenderMode = RenderModes.Unload;
		//	await Task.Delay(75);
		//	actor.RenderMode = RenderModes.Draw;
		//}

		var handle = ActorService.Instance.ObjectTable.Get<GameObjectMemory>(actor.Address);
		if (handle == null || !handle.IsValid)
			return;

		await this.RedrawService.Redraw(handle);
	}
}

public class RedrawService
{
	private static HookHandle? s_enableDrawHook = null;
	private static HookHandle? s_disableDrawHook = null;
	private static HookHandle? s_isReadyToDrawHook = null;

	public RedrawService()
	{
		Log.Debug("RedrawService: Registering hooks...");
		s_enableDrawHook = ControllerService.Instance.RegisterWrapper<GameObject.EnableDraw>();
		Log.Debug("RedrawService: Registered s_enableDrawHook: {Hook}", s_enableDrawHook);
		s_disableDrawHook = ControllerService.Instance.RegisterWrapper<GameObject.DisableDraw>();
		Log.Debug("RedrawService: Registered s_disableDrawHook: {Hook}", s_disableDrawHook);
		s_isReadyToDrawHook = ControllerService.Instance.RegisterWrapper<GameObject.IsReadyToDraw>();
		Log.Debug("RedrawService: Registered s_isReadyToDrawHook: {Hook}", s_isReadyToDrawHook);
	}

	public delegate void RedrawEvent(ObjectHandle<GameObjectMemory> obj, RedrawStage stage);
	public event RedrawEvent? OnRedraw;

	public static async Task DrawWhenReady(int objectIndex)
	{
		if (ControllerService.Instance == null || ControllerService.Instance.Framework?.Active != true)
			return;

		IntPtr currentPtr = ActorService.Instance.ObjectTable.GetAddress(objectIndex);
		using var obj = new ObjectHandle<GameObjectMemory>(currentPtr, ActorService.Instance.ObjectTable);

		await ControllerService.Instance.Framework.RunOnTickUntilAsync(
			() => IsReadyToDraw(obj),
			deferTicks: 5,
			timeoutTicks: 100,
			() => EnableDraw(objectIndex));
	}

	public static bool IsReadyToDraw(ObjectHandle<GameObjectMemory> obj)
	{
		if (!obj.IsValid || s_isReadyToDrawHook == null)
			return false;
		bool? result = null;
		try
		{
			result = ControllerService.Instance.InvokeHook<bool>(s_isReadyToDrawHook, mode: RemoteController.Interop.DispatchMode.FrameworkTick, args: obj.Address);
			if (result == null)
			{
				Log.Warning("Object is ready to draw invocation returned null");
			}
		}
		catch
		{
			Log.Verbose("Failed to invoke 'IsReadyToDraw' hook.");
		}

		return result ?? false;
	}

	public static bool DisableDraw(int objectIndex)
	{
		IntPtr currentPtr = ActorService.Instance.ObjectTable.GetAddress(objectIndex);
		using var obj = new ObjectHandle<GameObjectMemory>(currentPtr, ActorService.Instance.ObjectTable);

		if (!obj.IsValid || s_disableDrawHook == null)
			return false;

		long? result = null;
		try
		{
			result = ControllerService.Instance.InvokeHook<long>(s_disableDrawHook, args: obj.Address, mode: RemoteController.Interop.DispatchMode.FrameworkTick, timeoutMs: 1000);
			if (result == null)
			{
				Log.Warning("Object disable draw invocation returned null");
			}
		}
		catch
		{
			Log.Verbose("Failed to invoke 'DisableDraw' hook.");
		}

		return result.HasValue && result.Value != 0;
	}

	public static bool EnableDraw(int objectIndex)
	{
		IntPtr currentPtr = ActorService.Instance.ObjectTable.GetAddress(objectIndex);
		using var obj = new ObjectHandle<GameObjectMemory>(currentPtr, ActorService.Instance.ObjectTable);

		if (!obj.IsValid || s_enableDrawHook == null)
			return false;

		byte? result = null;
		try
		{
			result = ControllerService.Instance.InvokeHook<byte>(s_enableDrawHook, args: obj.Address, mode: RemoteController.Interop.DispatchMode.FrameworkTick, timeoutMs: 1000);
			if (result == null)
			{
				Log.Warning("Object enable draw invocation returned null");
			}
		}
		catch
		{
			Log.Verbose("Failed to invoke 'EnableDraw' hook.");
		}

		return result.HasValue && result.Value != 0;
	}

	public static async Task WaitForDrawing(int objectIndex)
	{
		if (ControllerService.Instance == null || ControllerService.Instance.Framework?.Active != true)
			return;

		IntPtr currentPtr = ActorService.Instance.ObjectTable.GetAddress(objectIndex);
		using var obj = new ObjectHandle<GameObjectMemory>(currentPtr, ActorService.Instance.ObjectTable);

		await ControllerService.Instance.Framework.RunOnTickUntilAsync(
			() => obj.Do(o => o.ModelObject?.IsVisible == true) == true,
			deferTicks: 5,
			timeoutTicks: 100);
	}

	public async Task Redraw(ObjectHandle<GameObjectMemory> obj)
	{
		string name = obj.DoRef(a => a.DisplayName) ?? "Unknown";
		Log.Debug($"Redrawing game object \"{name}\": 0x{obj.Address:X}");

		ActorService.Instance.ObjectTable.Refresh();
		int objectIndex = ActorService.Instance.ObjectTable.GetIndexOf(obj.Address);
		if (objectIndex == -1)
		{
			Log.Error("Could not find the object index for the actor \"{name}\"");
			return;
		}

		try
		{
			bool disableResult = DisableDraw(objectIndex);
			if (!disableResult)
			{
				// TODO: Perhaps instead of skipping the redraw, retry?
				// TOOD: To the same with EnableDraw below.
				Log.Warning($"Failed to disable draw for object \"{name}\". Skipping redraw.");
				return;
			}

			this.OnRedraw?.Invoke(obj, RedrawStage.Before);

			try
			{
				await DrawWhenReady(objectIndex);
			}
			catch (OperationCanceledException)
			{
				Log.Warning("DrawWhenReady timed out. Forcing EnableDraw.");
				ControllerService.Instance.Framework.RunAfterTicks(3, () => EnableDraw(objectIndex));
				return;
			}

			await WaitForDrawing(objectIndex);

			this.OnRedraw?.Invoke(obj, RedrawStage.After);
			Log.Debug($"Redrew object \"{name}\".");
		}
		catch (Exception ex)
		{
			Log.Error(ex, $"Failed to redraw object \"{name}\".");
		}
	}
}
