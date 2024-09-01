// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Brio;

using System.Threading.Tasks;

public static class Brio
{
	public static async Task<string> Redraw(int targetIndex)
	{
		RedrawData data = new() { ObjectIndex = targetIndex };
		var result = await BrioApi.Post("/redraw", data);
		await Task.Delay(500); // TODO: Figure out what these delays are for
		return result;
	}

	public static async Task<int> Spawn()
	{
		var resultRaw = await BrioApi.Post("/spawn");
		var resultId = int.Parse(resultRaw);
		await Task.Delay(500); // TODO: Figure out what these delays are for
		return resultId;
	}

	public static async Task<bool> Despawn(int actorIndex)
	{
		DespawnData data = new() { ObjectIndex = actorIndex };
		var resultRaw = await BrioApi.Post("/despawn", data);
		var result = bool.Parse(resultRaw);
		return result;
	}
}

public enum RedrawResult
{
	NoChange,
	Optimized,
	Full,
	Failed,
}

public class RedrawData
{
	public int ObjectIndex { get; set; }
}

public class DespawnData
{
	public int ObjectIndex { get; set; } = -1;
}
