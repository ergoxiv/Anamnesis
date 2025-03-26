﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Core.Memory;

using Anamnesis.Memory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XivToolsWpf;

#pragma warning disable SA1027, SA1025
public class AddressService : ServiceBase<AddressService>
{
	private static IntPtr weather;
	private static IntPtr cameraManager;

	// Static offsets
	public static IntPtr ActorTable { get; private set; }
	public static IntPtr GPoseFilters { get; private set; }
	public static IntPtr SkeletonFreezeRotation { get; private set; }   // HkaPose::SyncModelSpace
	public static IntPtr SkeletonFreezeRotation2 { get; private set; }  // HkaPose::CalculateBoneModelSpace
	public static IntPtr SkeletonFreezeRotation3 { get; private set; }  // HkaLookAtIkSolver::Solve
	public static IntPtr SkeletonFreezeScale { get; private set; }      // HkaPose::SyncModelSpace
	public static IntPtr SkeletonFreezeScale2 { get; private set; }     // HkaPose::CalculateBoneModelSpace
	public static IntPtr SkeletonFreezePosition { get; private set; }   // HkaPose::SyncModelSpace
	public static IntPtr SkeletonFreezePosition2 { get; private set; }  // HkaPose::CalculateBoneModelSpace
	public static IntPtr SkeletonFreezePhysics { get; private set; }    // PhysicsOffset (Rotation)
	public static IntPtr SkeletonFreezePhysics2 { get; private set; }   // PhysicsOffset2 (Position)
	public static IntPtr SkeletonFreezePhysics3 { get; private set; }   // PhysicsOffset3 (Scale)
	public static IntPtr WorldPositionFreeze { get; set; }
	public static IntPtr WorldRotationFreeze { get; set; }
	public static IntPtr GPoseCameraTargetPositionFreeze { get; set; }
	public static IntPtr GposeCheck { get; private set; }               // GPoseCheckOffset
	public static IntPtr GposeCheck2 { get; private set; }              // GPoseCheck2Offset
	public static IntPtr Territory { get; private set; }
	public static IntPtr GPose { get; private set; }
	public static IntPtr TimeAsm { get; private set; }
	public static IntPtr Framework { get; set; }
	public static IntPtr PlayerTargetSystem { get; set; }
	public static IntPtr AnimationSpeedPatch { get; set; }

	// The kinematic driver is used as an additional measure in freezing the player's position, rotation, and scale.
	// It is used in conjunction with the skeleton freeze to ensure the player is completely frozen.
	// Otherwise, you won't be able to move the actor's visor and top bones.
	public static IntPtr KineDriverPosition { get; private set; }
	public static IntPtr KineDriverRotation { get; private set; }
	public static IntPtr KineDriverScale { get; private set; }

	public static IntPtr Camera
	{
		get
		{
			IntPtr address = MemoryService.ReadPtr(cameraManager);

			if (address == IntPtr.Zero)
				throw new Exception("Failed to read camera address");

			return address;
		}
	}

	public static IntPtr Weather
	{
		get
		{
			IntPtr address = MemoryService.ReadPtr(weather);

			if (address == IntPtr.Zero)
				throw new Exception("Failed to read weather address");

			// CMtools Weather offset
			address += 0x20;
			return address;
		}
		private set
		{
			weather = value;
		}
	}

	public static IntPtr GPoseWeather
	{
		get
		{
			IntPtr address = MemoryService.ReadPtr(GPoseFilters);

			if (address == IntPtr.Zero)
				throw new Exception("Failed to read gpose filters address");

			// CMTools ForceWeather offset
			address += 0x27;
			return address;
		}
	}

	public static IntPtr GPoseCamera
	{
		get
		{
			IntPtr address = MemoryService.ReadPtr(GPose);

			if (address == IntPtr.Zero)
				throw new Exception("Failed to read gpose address");

			// CMTools CamX offset
			address += 0xA0;
			return address;
		}
	}

	public override async Task Initialize()
	{
		await base.Initialize();
		await this.Scan();
	}

	private async Task Scan()
	{
		if (MemoryService.Process == null)
			return;

		if (MemoryService.Process.MainModule == null)
			throw new Exception("Process has no main module");

		await Dispatch.NonUiThread();

		var tasks = new List<Task>
		{
			// Scan for all static addresses
			// Some signatures taken from Dalamud: https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Game/ClientState/ClientStateAddressResolver.cs
			this.GetAddressFromSignature("ActorTable", "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 44 0F B6 83", 0, (p) => { ActorTable = p; }),
			this.GetAddressFromTextSignature("SkeletonFreezeRotation", "41 0F 29 5C 12 10", (p) => { SkeletonFreezeRotation = p; }),
			this.GetAddressFromTextSignature("SkeletonFreezeRotation2", "43 0F 29 5C 18 10", (p) => { SkeletonFreezeRotation2 = p; }),
			this.GetAddressFromTextSignature("SkeletonFreezeRotation3", "0F 29 5E 10 49 8B 73 28", (p) => { SkeletonFreezeRotation3 = p; }),
			this.GetAddressFromTextSignature("SkeletonFreezeScale", "41 0F 29 44 12 20", (p) => { SkeletonFreezeScale = p; }),
			this.GetAddressFromTextSignature("SkeletonFreezeScale2", "43 0F 29 44 18 20", (p) => { SkeletonFreezeScale2 = p; }),
			this.GetAddressFromTextSignature("SkeletonFreezePosition", "41 0F 29 24 12", (p) => { SkeletonFreezePosition = p; }),
			this.GetAddressFromTextSignature("SkeletonFreezePosition2", "43 0f 29 24 18", (p) => { SkeletonFreezePosition2 = p; }),
			this.GetAddressFromTextSignature("WorldPositionFreeze", "F3 0F 11 ?? ?? F3 0F 11 ?? ?? F3 44 0F 11 ?? ?? 48 8B 8B ?? 00", (p) => { WorldPositionFreeze = p; }),
			this.GetAddressFromTextSignature("WorldRotationFreeze", "0F 11 40 60 48 8B 8B f0 00 00 00 0F B6 81 89 00 00 00 24 0F 3C 03 75 08 48 8B 01 B2 01 FF 50 38 ?? ?? ?? ?? C8", (p) => { WorldRotationFreeze = p; }),
			this.GetAddressFromTextSignature("GPoseCameraTargetPositionFreeze", "F3 0F 10 4D 00 E8 ?? ?? ?? ?? 48 8B 74 24", (p) => { GPoseCameraTargetPositionFreeze = p + 4; }),
			this.GetAddressFromTextSignature("AnimationSpeedPatch", "F3 0F 11 94 ?? ?? ?? ?? ?? 80 89 ?? ?? ?? ?? 01", (p) => { AnimationSpeedPatch = p; }),
			this.GetAddressFromSignature("Territory", "8B 1D ?? ?? ?? ?? 0F 45 D8 39 1D", 2, (p) => { Territory = p; }),
			this.GetAddressFromSignature("Weather", "48 8B 9F ?? ?? ?? ?? 48 8D 0D", 0, (p) => { Weather = p + 0x8; }),
			this.GetAddressFromSignature("GPoseFilters", "48 85 D2 4C 8B 05 ?? ?? ?? ??", 0, (p) => { GPoseFilters = p; }),
			this.GetAddressFromSignature("GposeCheck", "0F 84 ?? ?? ?? ?? 8B 15 ?? ?? ?? ?? 48 89 6C 24 ??", 0, (p) => { GposeCheck = p; }),
			this.GetAddressFromSignature("GposeCheck2", "8D 48 FF 48 8D 05 ?? ?? ?? ?? 8B 0C 88 48 8B 02 83 F9 04 49 8B CA", 0, (p) => { GposeCheck2 = p; }),
			this.GetAddressFromSignature("GPose", "74 35 48 39 0D ?? ?? ?? ??", 0, (p) => { GPose = p + 0x20; }),
			this.GetAddressFromSignature("Camera", "48 8D 35 ?? ?? ?? ?? 48 8B 09", 0, (p) => { cameraManager = p; }),
			this.GetAddressFromSignature("PlayerTargetSystem", "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 3B C3 74 08", 0, (p) => { PlayerTargetSystem = p; }),
			this.GetAddressFromTextSignature("TimeAsm", "48 89 87 ?? ?? ?? ?? 48 69 C0", (p) => TimeAsm = p),
			this.GetAddressFromTextSignature("Framework", "48 C7 05 ?? ?? ?? ?? 00 00 00 00 E8 ?? ?? ?? ?? 48 8D ?? ?? ?? 00 00 E8 ?? ?? ?? ?? 48 8D", (p) =>
				{
					int frameworkOffset = MemoryService.Read<int>(p + 3);
					IntPtr frameworkPtr = MemoryService.ReadPtr(p + 11 + frameworkOffset);
					Framework = frameworkPtr;
				}),
			this.GetAddressFromTextSignature("SkeletonFreezePhysics (1/2/3)", "0F 11 00 41 0F 10 4C 24 ?? 0F 11 48 10 41 0F 10 44 24 ?? 0F 11 40 20 48 8B 46 28", (p) =>
				{
					SkeletonFreezePhysics2 = p;
					SkeletonFreezePhysics = p + 0x9;
					SkeletonFreezePhysics3 = p + 0x13;
				}),
			this.GetAddressFromTextSignature("KineDriverPosition", "41 0F 11 04 07", (p) => { KineDriverPosition = p; }),
			this.GetAddressFromTextSignature("KineDriverRotation", "41 0F 11 4C 07 ?? 0F 10 41 20", (p) => { KineDriverRotation = p; }),
			this.GetAddressFromTextSignature("KineDriverScale", "41 0F 11 44 07 ?? 48 8B 47 28", (p) => { KineDriverScale = p; }),
		};

		await Task.WhenAll(tasks.ToArray());

		Log.Information($"Scanned for {tasks.Count} addresses");
	}

	private Task GetAddressFromSignature(string name, string signature, int offset, Action<IntPtr> callback)
	{
		if (MemoryService.Scanner == null)
			throw new Exception("No memory scanner");

		return Task.Run(() =>
		{
			try
			{
				IntPtr ptr = MemoryService.Scanner.GetStaticAddressFromSig(signature, offset);
				callback.Invoke(ptr);
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"Failed to scan memory for signature: {name} (Have you tried restarting FFXIV?)");
			}
		});
	}

	private Task GetAddressFromTextSignature(string name, string signature, Action<IntPtr> callback)
	{
		if (MemoryService.Scanner == null)
			throw new Exception("No memory scanner");

		return Task.Run(() =>
		{
			try
			{
				IntPtr ptr = MemoryService.Scanner.ScanText(signature);
				callback.Invoke(ptr);
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"Failed to scan memory for text signature: {name} (Have you tried restarting FFXIV?)");
			}
		});
	}

	private Task GetBaseAddressFromSignature(string name, string signature, int skip, bool moduleBase, Action<IntPtr> callback)
	{
		if (MemoryService.Scanner == null)
			throw new Exception("No memory scanner");

		return Task.Run(() =>
		{
			if (MemoryService.Process?.MainModule == null)
				return;

			try
			{
				IntPtr ptr = MemoryService.Scanner.ScanText(signature);

				ptr += skip;
				int offset = MemoryService.Read<int>(ptr);

				if (moduleBase)
				{
					ptr = MemoryService.Process.MainModule.BaseAddress + offset;
				}
				else
				{
					ptr += offset + 4;
				}

				callback.Invoke(ptr);
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"Failed to scan memory for base address from signature: {name} (Have you tried restarting FFXIV?)");
				callback.Invoke(IntPtr.Zero);
			}
		});
	}
}
