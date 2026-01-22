// © Anamnesis.
// Licensed under the MIT license.

namespace RemoteController.Interop.Delegates;

using System.Numerics;
using System.Runtime.InteropServices;

public static class Camera
{
	[FunctionBind("E8 ?? ?? ?? ?? 0F 28 C7 0F 28 CE")]
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public unsafe delegate Vector2* WorldToScreenPoint(Vector2* screenPoint, Vector3* worldPoint);
}

public static class Character
{
	[FunctionBind("E8 ?? ?? ?? ?? 84 C0 8B CF")]
	[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
	public delegate bool IsWanderer(nint charPtr);
}

public static class Framework
{
	[FunctionBind("48 8D 05 ?? ?? ?? ?? 66 C7 41 ?? ?? ?? 48 89 01 48 8B F1", offset: 0x20)]
	[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
	public delegate byte Tick(nint fPtr);

	[FunctionBind("40 53 55 57 41 55 48 83 EC ?? ?? 48 ?? ?? ?? ?? ?? ?? ?? 48")]
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate void RenderGraphics(long a1);
}

public static class GameObject
{
	[FunctionBind("0F B6 81 ?? ?? ?? ?? 84 C0 78 2F")]
	[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
	public delegate byte EnableDraw(nint objPtr);

	[FunctionBind("40 53 48 83 EC 20 80 B9 ?? ?? ?? ?? ?? 48 8B D9 7D 1F")]
	[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
	public delegate long DisableDraw(nint objPtr);

	[FunctionBind("E8 ?? ?? ?? ?? 84 C0 74 ?? 48 8B 17 45 33 C9")]
	[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
	public delegate byte IsReadyToDraw(nint objPtr);
}

public static class GameMain
{
	[FunctionBind("E8 ?? ?? ?? ?? 83 7F ?? ?? 4C 8D 3D")]
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate bool IsInGPose();
}
