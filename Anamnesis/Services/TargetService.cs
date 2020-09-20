﻿// Concept Matrix 3.
// Licensed under the MIT license.

namespace Anamnesis
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Forms.VisualStyles;
	using Anamnesis;
	using Anamnesis.Core.Memory;
	using Anamnesis.GUI.Dialogs;
	using Anamnesis.GUI.Services;
	using Anamnesis.Memory;
	using Anamnesis.Services;
	using SimpleLog;

	public delegate void SelectionEvent(ActorViewModel? actor);

	public class TargetService : ServiceBase<TargetService>
	{
		public static event SelectionEvent? ActorSelected;

		public static ActorViewModel? SelectedActor { get; private set; }

		public override Task Start()
		{
			////gposeMem = MemoryService.GetMarshaler(Offsets.Main.GposeCheck);
			////gposeMem2 = MemoryService.GetMarshaler(Offsets.Main.GposeCheck2);

			Task.Run(this.Watch);

			return base.Start();
		}

		public void SelectActor(ActorViewModel? actor)
		{
			SelectedActor = actor;

			/*using IMarshaler<int> territoryMem = MemoryService.GetMarshaler(Offsets.Main.TerritoryAddress, Offsets.Main.Territory);

			int territoryId = territoryMem.Value;

			bool isBarracks = false;
			isBarracks |= territoryId == 534; // Twin adder barracks
			isBarracks |= territoryId == 535; // Immortal Flame barracks
			isBarracks |= territoryId == 536; // Maelstrom barracks

			// Mannequins and housing NPC's get actor type changed, but squadron members do not.
			if (!isBarracks && actor.Type == ActorTypes.EventNpc)
			{
				bool? result = await GenericDialog.Show($"The Actor: \"{actor.Name}\" appears to be a humanoid NPC. Do you want to change them to a player to allow for posing and appearance changes?", "Actor Selection", MessageBoxButton.YesNo);

				if (result == null)
					return;

				if (result == true)
				{
					actor.SetValue(Offsets.Main.ActorType, ActorTypes.Player);
					actor.Type = ActorTypes.Player;
					await actor.ActorRefreshAsync();

					if (actor.GetValue(Offsets.Main.ModelType) != 0)
					{
						actor.SetValue(Offsets.Main.ModelType, 0);
						await actor.ActorRefreshAsync();
					}
				}
			}

			// Carbuncles get model type set to player (but not actor type!)
			if (actor.Type == ActorTypes.BattleNpc)
			{
				int modelType = actor.GetValue(Offsets.Main.ModelType);
				if (modelType == 409 || modelType == 410 || modelType == 412)
				{
					bool? result = await GenericDialog.Show($"The Actor: \"{actor.Name}\" appears to be a Carbuncle. Do you want to change them to a player to allow for posing and appearance changes?", "Actor Selection", MessageBoxButton.YesNo);

					if (result == null)
						return;

					if (result == true)
					{
						actor.SetValue(Offsets.Main.ModelType, 0);
						await actor.ActorRefreshAsync();
					}
				}
			}*/

			ActorSelected?.Invoke(actor);
		}

		private async Task Watch()
		{
			try
			{
				await Task.Delay(500);

				IntPtr lastTargetAddress = IntPtr.Zero;

				while (this.IsAlive)
				{
					await Task.Delay(50);

					while (ActorRefreshService.Instance.IsRefreshing)
						await Task.Delay(250);

					IntPtr newTargetAddress;
					if (GposeService.Instance.IsGpose)
					{
						newTargetAddress = MemoryService.ReadPtr(AddressService.GPoseTargetManager);
					}
					else
					{
						newTargetAddress = MemoryService.ReadPtr(AddressService.TargetManager);
					}

					if (newTargetAddress != lastTargetAddress)
					{
						lastTargetAddress = newTargetAddress;

						try
						{
							if (newTargetAddress == IntPtr.Zero)
							{
								////this.SelectActor(null);
							}
							else
							{
								ActorViewModel vm = new ActorViewModel(newTargetAddress);
								this.SelectActor(vm);
							}
						}
						catch (Exception ex)
						{
							Log.Write(Severity.Warning, new Exception("Failed to select current target", ex));
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Write(ex);
			}
		}
	}
}