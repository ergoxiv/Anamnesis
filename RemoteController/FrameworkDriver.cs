// © Anamnesis.
// Licensed under the MIT license.

namespace RemoteController;

using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using RemoteController.Interop.Delegates;
using Serilog;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A wrapper of framework object from the target process
/// that drives synchronization with the host application.
/// </summary>
[RequiresUnreferencedCode("This class is not trimming-safe")]
[RequiresDynamicCode("This class requires dynamic code due to hook reflection")]
public class FrameworkDriver : IDisposable
{
	private readonly IHook<Framework.Tick> tickHook;
	private bool disposedValue = false;
	private bool isSyncEnabled = false;

	/// <summary>
	/// Initializes a new instance of the <see cref="FrameworkDriver"/> class.
	/// </summary>
	/// <param name="tickFuncAddr">
	/// The target function address of the framework tick method to hook.
	/// </param>
	public FrameworkDriver(IntPtr tickFuncAddr)
	{
		this.tickHook = ReloadedHooks.Instance.CreateHook<Framework.Tick>(this.DetourTick, tickFuncAddr);
		this.tickHook.Activate();
	}

	/// <summary>
	/// Gets or sets a value indicating whether framework synchronization is enabled.
	/// </summary>
	/// <remarks>
	/// When this property is set to true, the framework driver will attempt to
	/// synchronize the framework state with the host application on each tick.
	/// </remarks>
	public bool IsSyncEnabled
	{
		get => Volatile.Read(ref this.isSyncEnabled);
		set => Interlocked.Exchange(ref this.isSyncEnabled, value);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	private byte DetourTick(nint fPtr)
	{
		// FAST EXIT: If there's no pending work, run the original function directly
		if (!this.isSyncEnabled)
			return this.tickHook!.OriginalFunction(fPtr);

		try
		{
			this.isSyncEnabled = Controller.SendFrameworkRequest();
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Encountered error during framework sync.");
			this.isSyncEnabled = false;
		}

		return this.tickHook!.OriginalFunction(fPtr);
	}

	private void Dispose(bool disposing)
	{
		if (!this.disposedValue)
		{
			if (disposing)
			{
				this.tickHook.Disable();
				if (this.tickHook is IDisposable disposable)
				{
					disposable.Dispose();
				}
			}

			this.disposedValue = true;
		}
	}
}
