// © Anamnesis.
// Licensed under the MIT license.

namespace RemoteController;
public class HookManager
{
	private static readonly Lazy<HookManager> instance = new(() => new HookManager());
	private uint nextHookID = 1; // Id 0 is reserved for "invalid" hooks.
	private readonly HashSet<uint> activeHookIds = [];
	private HookManager() { }

	public static HookManager Instance => instance.Value;

	private uint AllocateHookId()
	{
		for (uint i = 0; i < uint.MaxValue; i++)
		{
			uint candidate = this.nextHookID++;
			if (this.nextHookID == 0)
				this.nextHookID = 1; // Skip 0 (reserved)

			if (this.activeHookIds.Add(candidate))
				return candidate;
		}

		throw new InvalidOperationException("No more hook IDs available.");
	}

	private void ReleaseHookId(uint id) => this.activeHookIds.Remove(id);
}
