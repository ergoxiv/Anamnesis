// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor.Refresh;

using Anamnesis.Brio;
using Anamnesis.Files;
using Anamnesis.Memory;
using Anamnesis.Services;
using Serilog;
using System.Threading.Tasks;
using XivToolsWpf;

public class BrioActorRefresher : IActorRefresher
{
	public bool CanRefresh(ActorMemory actor)
	{
		// Only if Brio integration is really enabled
		if (!SettingsService.Current.UseExternalRefreshBrio)
			return false;

		return true;
	}

	public async Task RefreshActor(ActorMemory actor)
	{
		await Dispatch.MainThread();

		if (PoseService.Instance.IsEnabled)
		{
			// Save the current pose
			PoseFile poseFile = new PoseFile();
			SkeletonVisual3d skeletonVisual3D = new SkeletonVisual3d();
			await skeletonVisual3D.SetActor(actor);
			poseFile.WriteToFile(actor, skeletonVisual3D, null);

			var result = await Brio.Redraw(actor.ObjectIndex);
			Log.Verbose($"Brio redraw result: {result}");

			if (result == "\"Full\"")
			{
				new Task(async () =>
				{
					await Task.Delay(500);
					await Dispatch.MainThread();

					// Restore current pose
					skeletonVisual3D = new SkeletonVisual3d();
					await skeletonVisual3D.SetActor(actor);
					await poseFile.Apply(actor, skeletonVisual3D, null, PoseFile.Mode.All, true);
				}).Start();
			}
		}
		else
		{
			var result = await Brio.Redraw(actor.ObjectIndex);
			Log.Verbose($"Brio redraw result: {result}");
		}
	}
}
