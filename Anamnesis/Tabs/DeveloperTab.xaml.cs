// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Tabs;

using Anamnesis.Actor;
using Anamnesis.Actor.Utilities;
using Anamnesis.Core;
using Anamnesis.Files;
using Anamnesis.GUI.Dialogs;
using Anamnesis.Memory;
using Anamnesis.Serialization;
using Anamnesis.Services;
using Anamnesis.Utils;
using Anamnesis.Views;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using XivToolsWpf;

/// <summary>
/// Interaction logic for DeveloperTab.xaml.
/// </summary>
public partial class DeveloperTab : UserControl
{
	// Replace the problematic line with a proper dictionary initialization and assignment
	private readonly Dictionary<(ActorCustomizeMemory.Races, ActorCustomizeMemory.Tribes, ActorCustomizeMemory.Genders), int> endIndexMap = new()
	{
		{ (ActorCustomizeMemory.Races.Hyur, ActorCustomizeMemory.Tribes.Midlander, ActorCustomizeMemory.Genders.Masculine), 7 },
		{ (ActorCustomizeMemory.Races.Hyur, ActorCustomizeMemory.Tribes.Midlander, ActorCustomizeMemory.Genders.Feminine), 5 },
		{ (ActorCustomizeMemory.Races.Hyur, ActorCustomizeMemory.Tribes.Highlander, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.Hyur, ActorCustomizeMemory.Tribes.Highlander, ActorCustomizeMemory.Genders.Feminine), 4 },
		{ (ActorCustomizeMemory.Races.Elezen, ActorCustomizeMemory.Tribes.Wildwood, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.Elezen, ActorCustomizeMemory.Tribes.Wildwood, ActorCustomizeMemory.Genders.Feminine), 4 },
		{ (ActorCustomizeMemory.Races.Elezen, ActorCustomizeMemory.Tribes.Duskwight, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.Elezen, ActorCustomizeMemory.Tribes.Duskwight, ActorCustomizeMemory.Genders.Feminine), 4 },
		{ (ActorCustomizeMemory.Races.Lalafel, ActorCustomizeMemory.Tribes.Plainsfolk, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.Lalafel, ActorCustomizeMemory.Tribes.Plainsfolk, ActorCustomizeMemory.Genders.Feminine), 4 },
		{ (ActorCustomizeMemory.Races.Lalafel, ActorCustomizeMemory.Tribes.Dunesfolk, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.Lalafel, ActorCustomizeMemory.Tribes.Dunesfolk, ActorCustomizeMemory.Genders.Feminine), 4 },
		{ (ActorCustomizeMemory.Races.Miqote, ActorCustomizeMemory.Tribes.SeekerOfTheSun, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.Miqote, ActorCustomizeMemory.Tribes.SeekerOfTheSun, ActorCustomizeMemory.Genders.Feminine), 4 },
		{ (ActorCustomizeMemory.Races.Miqote, ActorCustomizeMemory.Tribes.KeeperOfTheMoon, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.Miqote, ActorCustomizeMemory.Tribes.KeeperOfTheMoon, ActorCustomizeMemory.Genders.Feminine), 4 },
		{ (ActorCustomizeMemory.Races.Roegadyn, ActorCustomizeMemory.Tribes.SeaWolf, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.Roegadyn, ActorCustomizeMemory.Tribes.SeaWolf, ActorCustomizeMemory.Genders.Feminine), 4 },
		{ (ActorCustomizeMemory.Races.Roegadyn, ActorCustomizeMemory.Tribes.Hellsguard, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.Roegadyn, ActorCustomizeMemory.Tribes.Hellsguard, ActorCustomizeMemory.Genders.Feminine), 4 },
		{ (ActorCustomizeMemory.Races.AuRa, ActorCustomizeMemory.Tribes.Raen, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.AuRa, ActorCustomizeMemory.Tribes.Raen, ActorCustomizeMemory.Genders.Feminine), 4 },
		{ (ActorCustomizeMemory.Races.AuRa, ActorCustomizeMemory.Tribes.Xaela, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.AuRa, ActorCustomizeMemory.Tribes.Xaela, ActorCustomizeMemory.Genders.Feminine), 4 },
		{ (ActorCustomizeMemory.Races.Hrothgar, ActorCustomizeMemory.Tribes.Helions, ActorCustomizeMemory.Genders.Masculine), 8 },
		{ (ActorCustomizeMemory.Races.Hrothgar, ActorCustomizeMemory.Tribes.Helions, ActorCustomizeMemory.Genders.Feminine), 8 },
		{ (ActorCustomizeMemory.Races.Hrothgar, ActorCustomizeMemory.Tribes.TheLost, ActorCustomizeMemory.Genders.Masculine), 8 },
		{ (ActorCustomizeMemory.Races.Hrothgar, ActorCustomizeMemory.Tribes.TheLost, ActorCustomizeMemory.Genders.Feminine), 8 },
		{ (ActorCustomizeMemory.Races.Viera, ActorCustomizeMemory.Tribes.Rava, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.Viera, ActorCustomizeMemory.Tribes.Rava, ActorCustomizeMemory.Genders.Feminine), 4 },
		{ (ActorCustomizeMemory.Races.Viera, ActorCustomizeMemory.Tribes.Veena, ActorCustomizeMemory.Genders.Masculine), 4 },
		{ (ActorCustomizeMemory.Races.Viera, ActorCustomizeMemory.Tribes.Veena, ActorCustomizeMemory.Genders.Feminine), 4 },
	};

	public DeveloperTab()
	{
		this.InitializeComponent();
		this.ContentArea.DataContext = this;
	}

	public static TargetService TargetService => TargetService.Instance;
	public static GposeService GposeService => GposeService.Instance;
	public SceneOptionsValues SceneOptions { get; init; } = new();

#if DEBUG
	public bool IsDebug => true;
#else
	public bool IsDebug => false;
#endif

	private static Vector3 MedianVector3(List<Vector3> vectors)
	{
		float Median(List<float> values)
		{
			values.Sort();
			int count = values.Count;
			if (count % 2 == 1)
				return values[count / 2];
			else
				return (values[(count / 2) - 1] + values[count / 2]) / 2f;
		}

		var xs = vectors.Select(v => v.X).ToList();
		var ys = vectors.Select(v => v.Y).ToList();
		var zs = vectors.Select(v => v.Z).ToList();

		return new Vector3(Median(xs), Median(ys), Median(zs));
	}

	private void OnNpcNameSearchClicked(object sender, RoutedEventArgs e)
	{
		GenericSelectorUtil.Show(GameDataService.BattleNpcNames, (v) =>
		{
			if (v.Description == null)
				return;

			ClipboardUtility.CopyToClipboard(v.Description);
		});
	}

	private void OnFindNpcClicked(object sender, RoutedEventArgs e)
	{
		TargetSelectorView.Show((a) =>
		{
			var actorHandle = ActorService.Instance.ObjectTable.Get<ActorMemory>(a.Address);
			if (actorHandle != null)
			{
				NpcAppearanceSearch.Search(actorHandle);
			}
		});
	}

	private async Task<Skeleton?> TryGetSkeletonAsync(ObjectHandle<ActorMemory> actorHandle, int maxRetries = 50)
	{
		Skeleton? skeleton = null;
		int retries = maxRetries;

		while (retries > 0)
		{
			await Task.Delay(100);

			await Anamnesis.Actor.Pages.PosePage.WorkQueue.Value.Enqueue(() =>
			{
				try
				{
					skeleton = new Skeleton(actorHandle);
				}
				catch (ArgumentOutOfRangeException)
				{
					skeleton = null;
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Failed to create skeleton during face offset generation.");
					skeleton = null;
				}
			});

			if (skeleton != null && skeleton.GetBone("j_kao") != null)
				return skeleton;

			retries--;
		}

		return null;
	}

	private async void OnGenerateFaceOffsets(object sender, RoutedEventArgs e)
	{
		var handle = TargetService.PlayerTargetHandle;
		if (handle == null || !handle.IsValid)
		{
			Log.Warning("Valid actor target is required to generate expression library.");
			return;
		}

		var actorHandle = ActorService.Instance.ObjectTable.Get<ActorMemory>(handle.Address);
		if (actorHandle == null || !actorHandle.IsValid)
		{
			Log.Warning("Basic actor is invalid");
			return;
		}

		if (Application.Current != null)
		{
			await Application.Current.Dispatcher.InvokeAsync(() =>
			{
				_ = new Anamnesis.Actor.Pages.PosePage();
				Anamnesis.Actor.Pages.PosePage.WorkQueue.Value.Enabled = true;
			});
		}

		await actorHandle.DoAsync(async actor =>
		{
			if (actor == null || !actor.IsValid || actor.DrawData.Customize == null)
			{
				Log.Warning("Actor is invalid");
				return;
			}

			if (GposeService.Instance == null || !GposeService.Instance.IsGpose)
			{
				Log.Warning("The actor must be in GPose to generate face offsets.");
				return;
			}

			// Prompt user for output directory location
			string? targetDirectory = null;
			using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
			{
				dialog.Description = "Select a folder to save the face offsets JSON file.";
				dialog.UseDescriptionForTitle = true;
				if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
					targetDirectory = dialog.SelectedPath;
			}

			if (string.IsNullOrEmpty(targetDirectory))
			{
				Log.Warning("No directory selected for saving face offsets.");
				return;
			}

			bool prevAutoRefresh = actor.AutomaticRefreshEnabled;
			actor.AutomaticRefreshEnabled = false;

			try
			{
				// Hyur, Midlander, Male, Face 1 is used as the reference skeleton.
				// The reference skeleton has relative positions of (0,0,0) for all facial bones.

				// Start by getting the reference skeleton.
				PoseService.Instance.IsEnabled = false;

				actor.DrawData.Customize.Race = ActorCustomizeMemory.Races.Hyur;
				actor.DrawData.Customize.Tribe = ActorCustomizeMemory.Tribes.Midlander;
				actor.DrawData.Customize.Gender = ActorCustomizeMemory.Genders.Masculine;
				actor.DrawData.Customize.Head = 1;

				await actor.Refresh(forceReload: true);

				Dictionary<string, List<Vector3>> refBoneSamples = new();

				for (int sample = 0; sample < 5; sample++)
				{
					PoseService.Instance.IsEnabled = false;
					await Task.Delay(100);

					PoseService.Instance.IsEnabled = true;
					await Task.Delay(50);

					var skeleton = await this.TryGetSkeletonAsync(actorHandle);
					if (skeleton == null || skeleton.GetBone("j_kao") is not Bone headBone)
					{
						Log.Warning("Skeleton creation failed or no face bones found after retries for reference skeleton.");
						return;
					}

					Dictionary<string, PoseFile.Bone> currentRefBonePos = new();
					currentRefBonePos["j_kao"] = new PoseFile.Bone(headBone);
					List<Bone> descendants = headBone.GetDescendants();
					foreach (Bone bone in descendants)
						currentRefBonePos[bone.Name] = new PoseFile.Bone(bone);

					foreach (var kvp in currentRefBonePos)
					{
						string boneName = kvp.Key;
						PoseFile.Bone bone = kvp.Value;

						if (bone.Position.HasValue && !boneName.ToLowerInvariant().StartsWith("j_kami_"))
						{
							if (!refBoneSamples.ContainsKey(boneName))
								refBoneSamples[boneName] = new List<Vector3>();
							refBoneSamples[boneName].Add(bone.Position.Value);
						}
					}
				}

				// Compute median for each reference bone
				Dictionary<string, Vector3> refBonePos = new();
				foreach (var kvp in refBoneSamples)
				{
					refBonePos[kvp.Key] = MedianVector3(kvp.Value);
				}

				Dictionary<(ActorCustomizeMemory.Races, ActorCustomizeMemory.Tribes, ActorCustomizeMemory.Genders, int), Dictionary<string, Vector3>> relDiffs = new();

				// Now loop over all other options and save the relative difference to the reference skeleton's positions.
				foreach (var race in Enum.GetValues<ActorCustomizeMemory.Races>())
				{
					foreach (var tribe in Enum.GetValues<ActorCustomizeMemory.Tribes>())
					{
						foreach (var gender in Enum.GetValues<ActorCustomizeMemory.Genders>())
						{
							int startIndex = (race != ActorCustomizeMemory.Races.Hrothgar || gender != ActorCustomizeMemory.Genders.Feminine) ? 1 : 5;

							if (!this.endIndexMap.TryGetValue((race, tribe, gender), out int endIndex))
								Log.Warning($"Could not find end index for: {race}, {tribe}, {gender}");

							for (int i = startIndex; i <= endIndex; i++)
							{
								Dictionary<string, List<Vector3>> boneSamples = new();

								for (int sample = 0; sample < 5; sample++)
								{
									PoseService.Instance.IsEnabled = false;

									actor.DrawData.Customize.Race = race;
									actor.DrawData.Customize.Tribe = tribe;
									actor.DrawData.Customize.Gender = gender;
									actor.DrawData.Customize.Head = (byte)i;

									await actor.Refresh(forceReload: true);

									PoseService.Instance.IsEnabled = true;
									await Task.Delay(50);

									Dictionary<string, PoseFile.Bone> currentBonePos = new();

									var skeletonC = await this.TryGetSkeletonAsync(actorHandle);
									if (skeletonC == null || skeletonC.GetBone("j_kao") is not Bone headBoneCurrent)
									{
										Log.Warning($"Skeleton creation failed or no face bones found after retries for {race} {tribe} {gender} Face {i}.");
										continue;
									}

									currentBonePos["j_kao"] = new PoseFile.Bone(headBoneCurrent);
									List<Bone> descendantsCurent = headBoneCurrent.GetDescendants();
									foreach (Bone bone in descendantsCurent)
										currentBonePos[bone.Name] = new PoseFile.Bone(bone);

									foreach (var boneName in currentBonePos.Keys.Intersect(refBonePos.Keys))
									{
										PoseFile.Bone currentBone = currentBonePos[boneName];
										Vector3 refPos = refBonePos[boneName];

										if (!boneSamples.ContainsKey(boneName))
											boneSamples[boneName] = new List<Vector3>();

										boneSamples[boneName].Add(currentBone.Position.HasValue ? currentBone.Position.Value - refPos : Vector3.Zero);
									}
								}

								// Compute median for each bone and store in relDiffs
								var medianDiffs = new Dictionary<string, Vector3>();
								foreach (var kvp in boneSamples)
								{
									string boneName = kvp.Key;
									List<Vector3> samples = kvp.Value;
									if (samples.Count > 0)
									{
										medianDiffs[boneName] = MedianVector3(samples);
									}
								}

								relDiffs[(race, tribe, gender, i)] = medianDiffs;
							}
						}
					}
				}

				PoseService.Instance.IsEnabled = false;

				// Build serializable structure
				var output = new Dictionary<string, Dictionary<string, Vector3>>();
				foreach (var entry in relDiffs)
				{
					var (race, tribe, gender, face) = entry.Key;
					string genderStr = gender == ActorCustomizeMemory.Genders.Masculine ? "m" : "f";
					string faceStr = $"f{face:000}";
					string key = $"{race.ToString().ToLower()}_{tribe.ToString().ToLower()}_{genderStr}_{faceStr}";

					var boneDict = new Dictionary<string, Vector3>();
					foreach (var bone in entry.Value)
					{
						// Vector3 to [x, y, z]
						boneDict[bone.Key] = new Vector3(
							float.IsNaN(bone.Value.X) || float.IsInfinity(bone.Value.X) ? 0f : bone.Value.X,
							float.IsNaN(bone.Value.Y) || float.IsInfinity(bone.Value.Y) ? 0f : bone.Value.Y,
							float.IsNaN(bone.Value.Z) || float.IsInfinity(bone.Value.Z) ? 0f : bone.Value.Z);
					}

					output[key] = boneDict;
				}

				string json = SerializerService.Serialize(output);

				// Save to file
				string filePath = Path.Combine(targetDirectory, "FaceOffsets.json");
				File.WriteAllText(filePath, json);

				Log.Information($"Face offsets saved to: {filePath}");
			}
			finally
			{
				actor.AutomaticRefreshEnabled = prevAutoRefresh;
				await actor.Refresh();
			}
		});
	}

	private void OnCopyActorAddressClicked(object sender, RoutedEventArgs e)
	{
		var handle = TargetService.PlayerTargetHandle;

		if (handle == null || !handle.IsValid)
		{
			Log.Warning("Actor is invalid");
			return;
		}

		string address = handle.Address.ToString("X");

		ClipboardUtility.CopyToClipboard(address);
	}

	private void OnCopyAssociatedAddressesClick(object sender, RoutedEventArgs e)
	{
		var abm = TargetService.PlayerTargetHandle;

		if (abm == null || !abm.IsValid)
		{
			Log.Warning("Actor is invalid");
			return;
		}

		try
		{
			ActorMemory memory = new();
			memory.SetAddress(abm.Address);

			StringBuilder sb = new();

			sb.AppendLine("Base: " + memory.Address.ToString("X"));
			sb.AppendLine("Model: " + (memory.ModelObject?.Address.ToString("X") ?? "0"));
			sb.AppendLine("Extended Appearance: " + (memory.ModelObject?.ExtendedAppearance?.Address.ToString("X") ?? "0"));
			sb.AppendLine("Skeleton: " + (memory.ModelObject?.Skeleton?.Address.ToString("X") ?? "0"));
			sb.AppendLine("Main Hand Model: " + (memory.DrawData.MainHand?.Model?.Address.ToString("X") ?? "0"));
			sb.AppendLine("Off Hand Model: " + (memory.DrawData.OffHand?.Model?.Address.ToString("X") ?? "0"));
			sb.AppendLine("Mount: " + (memory.Mount?.Address.ToString("X") ?? "0"));
			sb.AppendLine("Companion: " + (memory.Companion?.Address.ToString("X") ?? "0"));
			sb.AppendLine("Ornament: " + (memory.Ornament?.Address.ToString("X") ?? "0"));

			ClipboardUtility.CopyToClipboard(sb.ToString());
		}
		catch
		{
			Log.Warning("Could not read addresses");
		}
	}

	private async void OnSaveSceneClicked(object sender, RoutedEventArgs e)
	{
		try
		{
			SaveResult result = await FileService.Save<SceneFile>(null, FileService.DefaultSceneDirectory);

			if (result.Path == null)
				return;

			SceneFile file = new();
			file.WriteToFile();

			using var stream = new FileStream(result.Path.FullName, FileMode.Create);
			file.Serialize(stream);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to save scene");
		}
	}

	private async void OnLoadSceneClicked(object sender, RoutedEventArgs e)
	{
		try
		{
			Shortcut[]? shortcuts = [FileService.DefaultSceneDirectory];
			Type[] types = [typeof(SceneFile)];

			OpenResult result = await FileService.Open(null, shortcuts, types);

			if (result.File == null)
				return;

			if (result.File is SceneFile sceneFile)
			{
				await sceneFile.Apply(this.SceneOptions.GetMode());
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to load scene");
		}
	}

	public class SceneOptionsValues
	{
		public bool RelativePositions { get; set; } = true;
		public bool WorldPositions { get; set; } = false;
		public bool Poses { get; set; } = true;
		public bool Camera { get; set; } = false;
		public bool Weather { get; set; } = false;
		public bool Time { get; set; } = false;

		public SceneFile.Mode GetMode()
		{
			SceneFile.Mode mode = 0;

			if (this.RelativePositions)
				mode |= SceneFile.Mode.RelativePosition;

			if (this.WorldPositions)
				mode |= SceneFile.Mode.WorldPosition;

			if (this.Poses)
				mode |= SceneFile.Mode.Pose;

			if (this.Camera)
				mode |= SceneFile.Mode.Camera;

			if (this.Weather)
				mode |= SceneFile.Mode.Weather;

			if (this.Time)
				mode |= SceneFile.Mode.Time;

			return mode;
		}
	}
}
