// © Anamnesis.
// Licensed under the MIT license.

namespace RemoteController;

// Hooks should be able to:
// - Intercept function calls and establish a trampoline to the original function.
// - It should allow for custom code to run before and/or after the original function call.
// - It should also be possible to stop the original function from running entirely if needed.
// - Hooks should serialize the function's arguments, including resolving pointers to their actual values when present (the hook should read
// the memory at the pointer's address and include the actual data in the serialized message contents).
//   - The main application will deserialize the message and modify the data if necessary (by sending back a write request to the controller).
//   - Upon receiving the response, the hook writes the modified data back to the original memory address via the pointer.

// TODO:
// - Figure out how to capture the function's arguments and return value.
// - Interface to support multiple types of hooks in the future.
// - Enable/disable hooks at runtime using methods invoked by the controller intercepting commands from the main application.

// Notes:
// - Hooks should be be managed by a central HookManager class that keeps track of all active hooks.
// - With a specified callback/delegate, we should be able to tell the hook at what point in the function we wan to call the delegate (before, after, or instead of the original function).
// - It seems that Dalamud doesn't support hook chaining. Instead, the hook overwrites any existing hook on the same address.

// If my hook is made before Dalamud's, Dalamud will follow the JMP to my hook and treat my hook as the original function. If that is the case, then I can't overwrite Dalamud's hook,
// only call before or after. Otherwise, I risk breaking Dalamud's functionality. (if the "original function" starts with a JMP instruction, disallow stopping the "original" function from running).
// If my hook is made after Dalamud's, then I will be hooking into Dalamud's hook function. That is not an issue, as long as I call the JMP to their hook trampoline.
// We can avoid data races using a global mutex?

class FunctionHook : IHook
{
	private bool isDisposed;

	public uint Id { get; private set; }

	public nint Address { get; private set; }

	public bool IsEnabled { get; private set; } = false;

	public bool IsValid { get; private set; } = false;

	public FunctionHook(uint id, nint address, Delegate detour)
	{
		this.Id = id;
		this.Address = address;
	}

	public void Disable()
	{
		throw new NotImplementedException();
	}

	public void Enable()
	{
		throw new NotImplementedException();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!this.isDisposed)
		{
			if (disposing)
			{
				// TODO: dispose managed state (managed objects)
			}

			// TODO: free unmanaged resources (unmanaged objects) and override finalizer
			// TODO: set large fields to null
			this.isDisposed = true;
		}

		this.IsValid = false;
	}

	// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
	// ~FunctionHook()
	// {
	//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
	//     Dispose(disposing: false);
	// }

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		this.Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
