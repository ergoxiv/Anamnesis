// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Services;

using Anamnesis.Core;
using PropertyChanged;
using RemoteController.IPC;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A service that continously checks for changes to the game's GPose state.
/// </summary>
[AddINotifyPropertyChangedInterface]
public class GposeService : ServiceBase<GposeService>
{
	private EventSubscription? gposeEventSubsription;

	/// <summary>
	/// The delegate object for the <see cref="GposeService.GposeStateChanged"/> event.
	/// </summary>
	/// <param name="newState">The new GPose state (i.e. true if player actor is in GPose, false otherwise).</param>
	public delegate void GposeEvent(bool newState);

	/// <summary>
	/// Event that is triggered when the GPose state changes.
	/// </summary>
	public static event GposeEvent? GposeStateChanged;

	/// <summary>
	/// Gets a value indicating whether the signed-in character is currently in the GPose photo mode.
	/// </summary>
	/// <remarks>
	/// This is a cached value that is updated by a continuous background task.
	/// </remarks>
	public bool IsGpose { get; private set; } = false;

	/// <inheritdoc/>
	protected override IEnumerable<IService> Dependencies => [AddressService.Instance, GameService.Instance, ControllerService.Instance];

	/// <summary>
	/// Checks if the user is in GPose photo mode by probing the game process' memory.
	/// </summary>
	/// <returns>True if the user is in GPose, false otherwise.</returns>
	public static bool? IsInGpose()
	{
		bool? result = null;
		try
		{
			result = ControllerService.Instance.SendDriverCommand<bool>(DriverCommand.GetIsInGpose);
			if (result == null)
			{
				Log.Warning("GPose driver command did not return a result.");
			}
		}
		catch
		{
			Log.Verbose("Failed to query GPose state via driver command.");
		}

		return result;
	}

	/// <inheritdoc/>
	public override Task Shutdown()
	{
		this.gposeEventSubsription?.Dispose();
		this.gposeEventSubsription = null;
		return base.Shutdown();
	}

	/// <inheritdoc/>
	protected override async Task OnStart()
	{
		this.gposeEventSubsription = ControllerService.Instance.SubscribeEvent<GposeStateChangedPayload>(
			EventId.GposeStateChanged,
			this.OnGposeStateChanged);

		await base.OnStart();
	}

	private void OnGposeStateChanged(GposeStateChangedPayload payload)
	{
		if (payload.IsInGpose != this.IsGpose)
		{
			this.IsGpose = payload.IsInGpose;
			GposeStateChanged?.Invoke(payload.IsInGpose);
		}
	}
}
