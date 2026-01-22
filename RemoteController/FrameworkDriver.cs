// © Anamnesis.
// Licensed under the MIT license.

namespace RemoteController;

using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using RemoteController.Interop.Delegates;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A wrapper of framework object from the target process
/// that drives synchronization with the host application.
/// </summary>
[RequiresUnreferencedCode("This class is not trimming-safe")]
[RequiresDynamicCode("This class requires dynamic code due to hook reflection")]
public class FrameworkDriver : IDisposable
{
	private static readonly ConcurrentQueue<WorkItem> s_marshaledWork = new();

	private readonly IHook<Framework.Tick> tickHook;
	private bool disposedValue = false;
	private bool isSyncEnabled = false;
	private int requestVersion = 0;
	private long tickCounter = 0;

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
		set
		{
			if (value)
			{
				Interlocked.Increment(ref this.requestVersion);
			}

			Interlocked.Exchange(ref this.isSyncEnabled, value);
		}
	}

	public static byte[] EnqueueAndWait(Func<byte[]> func, int timeoutMs = 2000)
	{
		using var completion = new ManualResetEventSlim(false);
		byte[]? result = null;
		Exception? capturedEx = null;

		s_marshaledWork.Enqueue(new WorkItem
		{
			Action = () => {
				try { result = func(); }
				catch (Exception ex) { capturedEx = ex; }
			},
			Completion = completion
		});

		if (completion.Wait(timeoutMs))
		{
			if (capturedEx != null) throw capturedEx;
			return result ?? [];
		}

		throw new TimeoutException("Framework thread did not process wrapper invoke request within timeout.");
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	private byte DetourTick(nint fPtr)
	{
		byte result = this.tickHook!.OriginalFunction(fPtr);

		this.tickCounter++;

		if (this.tickCounter % 1000 == 0)
		{
			Log.Debug("Framework tick #{TickCount}", this.tickCounter);
		}

		while (s_marshaledWork.TryDequeue(out var work))
		{
			try { work.Action(); }
			finally { work.Completion.Set(); }
		}

		// FAST EXIT: If there's no pending work, run the original function directly
		if (!Volatile.Read(ref this.isSyncEnabled))
			return result;

		long measuredTime = 0;
		int versionBefore = Volatile.Read(ref this.requestVersion);
		bool continueProcessing = false;

		try
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();
			continueProcessing = Controller.SendFrameworkRequest();
			sw.Stop();
			measuredTime = sw.ElapsedTicks * 1_000_000 / System.Diagnostics.Stopwatch.Frequency;
			Log.Information("Framework sync completed in {ElapsedMicroseconds} µs.", measuredTime);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Encountered error during framework sync.");
			this.isSyncEnabled = false;
		}

		if (!continueProcessing)
		{
			int versionAfter = Volatile.Read(ref this.requestVersion);
			if (versionBefore == versionAfter)
			{
				Interlocked.Exchange(ref this.isSyncEnabled, false);
			}
		}

		return result;
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

	private struct WorkItem
	{
		public Action Action;
		public ManualResetEventSlim Completion;
	}
}
