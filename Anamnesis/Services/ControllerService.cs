// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis;

using Anamnesis.Core;
using Anamnesis.Services;
using SharedMemoryIPC;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A service that communicates with the remote controller that we inject into the game process.
/// The service is responsible for sending and receiving messages to and from the controller, including
/// watchdog heartbeats, and function hook communication.
/// </summary>
public class ControllerService : ServiceBase<ControllerService>
{
	private const string BUF_SHMEM_OUTGOING = "Local\\ANAM_SHMEM_MAIN_TO_CTRL";
	private const string BUF_SHMEM_INCOMING = "Local\\ANAM_SHMEM_CTRL_TO_MAIN";
	private const uint BUF_BLK_COUNT = 128;
	private const ulong BUF_BLK_SIZE = 8192;

	private const uint READ_TIMEOUT_MS = 16;
	private const int HEARTBEAT_INTERVAL_MS = 15_000;

	private Endpoint? outgoingEndpoint = null;
	private Endpoint? incomingEndpoint = null;

	/// <inheritdoc/>
	protected override IEnumerable<IService> Dependencies => [GameService.Instance];

	/// <inheritdoc/>
	public override async Task Shutdown()
	{
		this.outgoingEndpoint?.Dispose();
		this.incomingEndpoint?.Dispose();
		await base.Shutdown();
	}

	/// <inheritdoc/>
	protected override async Task OnStart()
	{
		this.CancellationTokenSource = new CancellationTokenSource();

		try
		{
			this.outgoingEndpoint = new Endpoint(BUF_SHMEM_OUTGOING, BUF_BLK_COUNT, BUF_BLK_SIZE);
			this.incomingEndpoint = new Endpoint(BUF_SHMEM_INCOMING, BUF_BLK_COUNT, BUF_BLK_SIZE);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to initialize IPC endpoints.");
			this.outgoingEndpoint?.Dispose();
			this.outgoingEndpoint = null;
			this.incomingEndpoint?.Dispose();
			this.incomingEndpoint = null;
			throw;
		}

		this.BackgroundTask = Task.Run(() => this.Tick(this.CancellationToken));
		await base.OnStart();
	}

	private async Task Tick(CancellationToken cancellationToken)
	{
		var heartbeatPayload = new MessageHeader(type: PayloadType.Heartbeat);
		var lastHeartbeat = Environment.TickCount64;

		while (this.IsInitialized && !cancellationToken.IsCancellationRequested)
		{
			try
			{
				// Check for incoming messages
				if (this.incomingEndpoint != null &&
					this.incomingEndpoint.Read<MessageHeader>(out uint id, out var header, READ_TIMEOUT_MS))
				{
					this.HandleIncomingMessage(id, header);
				}

				// Send heartbeat on HEARTBEAT_INTERVAL_MS
				var now = Environment.TickCount64;
				if (this.outgoingEndpoint != null && now - lastHeartbeat >= HEARTBEAT_INTERVAL_MS)
				{
					// TODO: Verify that the timeout does not lead to stuttering issues.
					this.outgoingEndpoint.Write(heartbeatPayload, READ_TIMEOUT_MS);
					lastHeartbeat = now;
				}
			}
			catch (TaskCanceledException)
			{
				// Task was canceled, exit the loop
				break;
			}
		}
	}

	private void HandleIncomingMessage(uint id, MessageHeader header)
	{
		// TODO: Implement message handling logic based on header, type, etc.
		// Example:
		// switch (header.Type)
		// {
		//     case PayloadType.Heartbeat:
		//         // Handle heartbeat
		//         break;
		//     case PayloadType.Error:
		//         // Handle error
		//         break;
		//     // Add more cases as needed
		// }
	}
}
