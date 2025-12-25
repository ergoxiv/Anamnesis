// © Anamnesis.
// Licensed under the MIT license.

namespace RemoteController;

/// <summary>
/// A generic interface for hooks.
/// </summary>
public interface IHook : IDisposable
{
	/// <summary>
	/// Gets the unique identifier of this hook.
	/// </summary>
	/// <remarks>
	/// The <see cref="HookManager"/> singleton is responsible for ensuring the
	/// uniqueness of each hook's identifier.
	/// </remarks>
	public uint Id { get; }

	/// <summary>
	/// Gets the address at which the hook has been set up.
	/// </summary>
	public IntPtr Address { get; }

	/// <summary>
	/// Gets a value indicating whether this hook is currently active.
	/// </summary>
	public bool IsEnabled { get; }

	/// <summary>
	/// Gets a value indicating whether this hook has been created and ready for use.
	/// </summary>
	/// <remarks>
	/// If a hook has been disposed of, this geter will return false.
	/// </remarks>
	public bool IsValid { get; }

	/// <summary>
	/// Enables an installed hook.
	/// </summary>
	public void Enable();

	/// <summary>
	/// Disables a hook without disposing it.
	/// </summary>
	/// <remarks>
	/// Disabled hooks can be re-enabled with <see cref="Enable"/> as long as they
	/// have not been disposed of.
	/// </remarks>
	public void Disable();
}
