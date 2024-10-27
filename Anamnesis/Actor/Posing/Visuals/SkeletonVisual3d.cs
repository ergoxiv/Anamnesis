﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor;

using Anamnesis.Actor.Posing;
using Anamnesis.Memory;
using Anamnesis.Posing;
using Anamnesis.Services;
using PropertyChanged;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Enumeration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using XivToolsWpf;
using XivToolsWpf.Math3D.Extensions;
using AnQuaternion = System.Numerics.Quaternion;

[AddINotifyPropertyChangedInterface]
public class SkeletonVisual3d : ModelVisual3D, INotifyPropertyChanged
{
	public readonly Dictionary<string, BoneVisual3d> Bones = new();
	public readonly List<BoneVisual3d> SelectedBones = new();
	public readonly HashSet<BoneVisual3d> HoverBones = new();

	private readonly QuaternionRotation3D rootRotation;
	private readonly List<BoneVisual3d> rootBones = new();
	private readonly List<BoneVisual3d> hairBones = new();
	private readonly List<BoneVisual3d> metBones = new();
	private readonly List<BoneVisual3d> topBones = new();
	private readonly List<BoneVisual3d> mainHandBones = new();
	private readonly List<BoneVisual3d> offHandBones = new();

	private readonly Dictionary<string, Tuple<string, string>> hairNameToSuffixMap = new()
	{
		{ "HairAutoFrontLeft", new("l", "j_kami_f_l") },	// Hair, Front Left
		{ "HairAutoFrontRight", new("r", "j_kami_f_r") },	// Hair, Front Right
		{ "HairAutoA", new("a", "j_kami_a") },				// Hair, Back Up
		{ "HairAutoB", new("b", "j_kami_b") },				// Hair, Back Down
		{ "HairFront", new("f", string.Empty) },			// Hair, Front (Custom Bone Name)
	};

	public SkeletonVisual3d()
	{
		this.rootRotation = new QuaternionRotation3D();
		this.Transform = new RotateTransform3D(this.rootRotation);
		this.OnTransformPropertyChanged(null, null);
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public enum SelectMode
	{
		Override,
		Add,
		Toggle,
	}

	public ActorMemory? Actor { get; private set; }
	public int SelectedCount => this.SelectedBones.Count;
	public bool CanEditBone => this.SelectedBones.Count == 1;
	public bool HasSelection => this.SelectedBones.Count > 0;
	public bool HasHover => this.HoverBones.Count > 0;
	public bool HasEquipmentBones => this.metBones.Count > 0 || this.topBones.Count > 0;
	public bool HasWeaponBones => this.mainHandBones.Count > 0 || this.offHandBones.Count > 0;

	public IEnumerable<BoneVisual3d> AllBones => this.Bones.Values;
	public IEnumerable<BoneVisual3d> HairBones => this.hairBones;
	public IEnumerable<BoneVisual3d> MetBones => this.metBones;
	public IEnumerable<BoneVisual3d> TopBones => this.topBones;
	public IEnumerable<BoneVisual3d> MainHandBones => this.mainHandBones;
	public IEnumerable<BoneVisual3d> OffHandBones => this.offHandBones;

	public string BoneSearch { get; set; } = string.Empty;

	public IEnumerable<BoneVisual3d> BoneSearchResult => string.IsNullOrWhiteSpace(this.BoneSearch) ? this.AllBones : this.AllBones.Where(b => FileSystemName.MatchesSimpleExpression($"*{this.BoneSearch}*", b.BoneName) || FileSystemName.MatchesSimpleExpression($"*{this.BoneSearch}*", b.Tooltip));

	public bool FlipSides
	{
		get => SettingsService.Current.FlipPoseGuiSides;
		set => SettingsService.Current.FlipPoseGuiSides = value;
	}

	public BoneVisual3d? CurrentBone
	{
		get
		{
			if (this.SelectedBones.Count <= 0)
				return null;

			return this.SelectedBones[this.SelectedBones.Count - 1];
		}

		set
		{
			throw new NotSupportedException();
		}
	}

	public bool HasTail => this.Actor?.Customize?.Race == ActorCustomizeMemory.Races.Miqote
		|| this.Actor?.Customize?.Race == ActorCustomizeMemory.Races.AuRa
		|| this.Actor?.Customize?.Race == ActorCustomizeMemory.Races.Hrothgar
		|| this.IsIVCS;

	public bool IsStandardFace => this.Actor == null ? true : !this.IsMiqote && !this.IsHrothgar && !this.IsViera;
	public bool IsMiqote => this.Actor?.Customize?.Race == ActorCustomizeMemory.Races.Miqote;
	public bool IsViera => this.Actor?.Customize?.Race == ActorCustomizeMemory.Races.Viera;
	public bool IsElezen => this.Actor?.Customize?.Race == ActorCustomizeMemory.Races.Elezen;
	public bool IsHrothgar => this.Actor?.Customize?.Race == ActorCustomizeMemory.Races.Hrothgar;
	public bool HasTailOrEars => this.IsViera || this.HasTail;

	public bool IsEars01 => this.IsViera && this.Actor?.Customize?.TailEarsType <= 1;
	public bool IsEars02 => this.IsViera && this.Actor?.Customize?.TailEarsType == 2;
	public bool IsEars03 => this.IsViera && this.Actor?.Customize?.TailEarsType == 3;
	public bool IsEars04 => this.IsViera && this.Actor?.Customize?.TailEarsType == 4;

	public bool IsIVCS { get; private set; }

	public bool IsVieraEarsFlop
	{
		get
		{
			if (!this.IsViera)
				return false;

			ActorCustomizeMemory? customize = this.Actor?.Customize;

			if (customize == null)
				return false;

			if (customize.Gender == ActorCustomizeMemory.Genders.Feminine && customize.TailEarsType == 3)
				return true;

			if (customize.Gender == ActorCustomizeMemory.Genders.Masculine && customize.TailEarsType == 2)
				return true;

			return false;
		}
	}

	public bool HasPreDTFace
	{
		get
		{
			// We can determine if we have a DT-updated face if we have a tongue bone.
			// EW faces don't have this bone, where as all updated faces in DT have it.
			// It would be better to enumerate all of the faces and be more specific.
			BoneVisual3d? tongueABone = this.GetBone("j_f_bero_01");
			if (tongueABone == null)
				return true;
			return false;
		}
	}

	public AnQuaternion RootRotation
	{
		get
		{
			return this.Actor?.ModelObject?.Transform?.Rotation ?? AnQuaternion.Identity;
		}
	}

	private static ILogger Log => Serilog.Log.ForContext<SkeletonVisual3d>();

	public void Clear()
	{
		this.ClearSelection();
		this.ClearBones();
		this.Children.Clear();
	}

	public void Select(IBone bone)
	{
		if (bone.Visual == null)
			return;

		SkeletonVisual3d.SelectMode mode = SkeletonVisual3d.SelectMode.Override;

		if (Keyboard.IsKeyDown(Key.LeftCtrl))
			mode = SkeletonVisual3d.SelectMode.Toggle;

		if (Keyboard.IsKeyDown(Key.LeftShift))
			mode = SkeletonVisual3d.SelectMode.Add;

		if (mode == SelectMode.Override)
			this.SelectedBones.Clear();

		if (this.SelectedBones.Contains(bone.Visual))
		{
			if (mode == SelectMode.Toggle)
			{
				this.SelectedBones.Remove(bone.Visual);
			}
		}
		else
		{
			this.SelectedBones.Add(bone.Visual);
		}

		PoseService.SelectedBoneName = this.CurrentBone?.Tooltip;

		this.RaisePropertyChanged(nameof(SkeletonVisual3d.CurrentBone));
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.HasSelection));
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.SelectedCount));
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.CanEditBone));
	}

	public void Select(IEnumerable<IBone> bones)
	{
		SkeletonVisual3d.SelectMode mode = SkeletonVisual3d.SelectMode.Override;

		if (Keyboard.IsKeyDown(Key.LeftCtrl))
			mode = SkeletonVisual3d.SelectMode.Toggle;

		if (Keyboard.IsKeyDown(Key.LeftShift))
			mode = SkeletonVisual3d.SelectMode.Add;

		if (mode == SelectMode.Override)
		{
			this.SelectedBones.Clear();
			mode = SelectMode.Add;
		}

		foreach (IBone bone in bones)
		{
			if (bone.Visual == null)
				continue;

			if (this.SelectedBones.Contains(bone.Visual))
			{
				if (mode == SelectMode.Toggle)
				{
					this.SelectedBones.Remove(bone.Visual);
				}
			}
			else
			{
				this.SelectedBones.Add(bone.Visual);
			}
		}

		PoseService.SelectedBoneName = this.CurrentBone?.Tooltip;

		this.RaisePropertyChanged(nameof(SkeletonVisual3d.CurrentBone));
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.HasSelection));
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.SelectedCount));
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.CanEditBone));
	}

	public void Select(List<BoneVisual3d> bones, SelectMode mode)
	{
		if (mode == SelectMode.Override)
			this.SelectedBones.Clear();

		foreach (BoneVisual3d bone in bones)
		{
			if (this.SelectedBones.Contains(bone))
			{
				if (mode == SelectMode.Toggle)
				{
					this.SelectedBones.Remove(bone);
				}
			}
			else
			{
				this.SelectedBones.Add(bone);
			}
		}

		PoseService.SelectedBoneName = this.CurrentBone?.Tooltip;

		this.RaisePropertyChanged(nameof(SkeletonVisual3d.CurrentBone));
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.HasSelection));
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.SelectedCount));
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.CanEditBone));
	}

	public void ClearSelection()
	{
		if (this.SelectedBones.Count == 0)
			return;

		this.SelectedBones.Clear();

		Application.Current?.Dispatcher.Invoke(() =>
		{
			PoseService.SelectedBoneName = this.CurrentBone?.Tooltip;

			this.RaisePropertyChanged(nameof(SkeletonVisual3d.CurrentBone));
			this.RaisePropertyChanged(nameof(SkeletonVisual3d.HasSelection));
			this.RaisePropertyChanged(nameof(SkeletonVisual3d.SelectedCount));
			this.RaisePropertyChanged(nameof(SkeletonVisual3d.CanEditBone));
		});
	}

	public void Hover(BoneVisual3d bone, bool hover, bool notify = true)
	{
		if (this.HoverBones.Contains(bone) && !hover)
		{
			this.HoverBones.Remove(bone);
		}
		else if (!this.HoverBones.Contains(bone) && hover)
		{
			this.HoverBones.Add(bone);
		}
		else
		{
			return;
		}

		if (notify)
		{
			this.NotifyHover();
		}
	}

	public void NotifyHover()
	{
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.HasHover));
	}

	public void NotifySkeletonChanged()
	{
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.AllBones));
	}

	public bool GetIsBoneHovered(BoneVisual3d bone)
	{
		return this.HoverBones.Contains(bone);
	}

	public bool GetIsBoneSelected(BoneVisual3d bone)
	{
		return this.SelectedBones.Contains(bone);
	}

	public bool GetIsBoneParentsSelected(BoneVisual3d? bone)
	{
		while (bone != null)
		{
			if (this.GetIsBoneSelected(bone))
				return true;

			bone = bone.Parent;
		}

		return false;
	}

	public bool GetIsBoneParentsHovered(BoneVisual3d? bone)
	{
		while (bone != null)
		{
			if (this.GetIsBoneHovered(bone))
				return true;

			bone = bone.Parent;
		}

		return false;
	}

	public BoneVisual3d? GetBone(string name)
	{
		if (this.Actor?.ModelObject?.Skeleton == null)
			return null;

		// only show actors that have atleast one partial skeleton
		if (this.Actor.ModelObject.Skeleton.Length <= 0)
			return null;

		string? modernName = LegacyBoneNameConverter.GetModernName(name);
		if (modernName != null)
			name = modernName;

		BoneVisual3d? bone;

		// Attempt to find hairstyle-specific bones. If not found, default to the standard hair bones.
		if (this.hairNameToSuffixMap.TryGetValue(name, out Tuple<string, string>? suffixAndDefault))
		{
			bone = this.FindHairBoneByPattern(suffixAndDefault.Item1);
			if (bone != null)
				return bone;
			else
				name = suffixAndDefault.Item2; // If not found, default to the standard hair bones.
		}

		this.Bones.TryGetValue(name, out bone);
		return bone;
	}

	public void SelectBody()
	{
		this.SelectHead();
		this.InvertSelection();

		List<BoneVisual3d> additionalBones = new List<BoneVisual3d>();
		BoneVisual3d? headBone = this.GetBone("j_kao");
		if (headBone != null)
			additionalBones.Add(headBone);
		this.Select(additionalBones, SkeletonVisual3d.SelectMode.Add);
	}

	public void SelectHead()
	{
		this.ClearSelection();

		BoneVisual3d? headBone = this.GetBone("j_kao");
		if (headBone == null)
			return;

		List<BoneVisual3d> headBones = new List<BoneVisual3d>();
		headBones.Add(headBone);

		this.GetBoneChildren(headBone, ref headBones);

		this.Select(headBones, SkeletonVisual3d.SelectMode.Add);
	}

	public void SelectWeapons()
	{
		this.ClearSelection();
		var bonesToSelect = this.mainHandBones.Concat(this.offHandBones).ToList();

		if (this.GetBone("n_buki_l") is BoneVisual3d boneLeft)
			bonesToSelect.Add(boneLeft);

		if (this.GetBone("n_buki_r") is BoneVisual3d boneRight)
			bonesToSelect.Add(boneRight);

		this.Select(bonesToSelect, SkeletonVisual3d.SelectMode.Add);
	}

	public void InvertSelection()
	{
		foreach ((string name, BoneVisual3d bone) in this.Bones)
		{
			bool selected = this.SelectedBones.Contains(bone);

			if (selected)
			{
				this.SelectedBones.Remove(bone);
			}
			else
			{
				this.SelectedBones.Add(bone);
			}
		}

		this.RaisePropertyChanged(nameof(SkeletonVisual3d.CurrentBone));
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.HasSelection));
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.SelectedCount));
		this.RaisePropertyChanged(nameof(SkeletonVisual3d.CanEditBone));
	}

	public void GetBoneChildren(BoneVisual3d bone, ref List<BoneVisual3d> bones)
	{
		foreach (Visual3D child in bone.Children)
		{
			if (child is BoneVisual3d childBone)
			{
				bones.Add(childBone);
				this.GetBoneChildren(childBone, ref bones);
			}
		}
	}

	public void Reselect()
	{
		List<BoneVisual3d> selection = new List<BoneVisual3d>(this.SelectedBones);
		this.ClearSelection();
		this.Select(selection);
	}

	public Dictionary<string, Transform> TakeSnapshot()
	{
		var snapshot = new Dictionary<string, Transform>();

		if (this.Actor?.ModelObject?.Skeleton == null)
			return snapshot;

		this.Actor.ModelObject.Skeleton.EnableReading = false;

		foreach (var bone in this.AllBones)
		{
			snapshot[bone.BoneName] = new Transform
			{
				Position = bone.TransformMemory.Position,
				Rotation = bone.TransformMemory.Rotation,
				Scale = bone.TransformMemory.Scale,
			};
		}

		this.Actor.ModelObject.Skeleton.EnableReading = true;

		return snapshot;
	}

	public void ReadTransforms()
	{
		if (this.Bones == null || this.Actor?.ModelObject?.Skeleton == null)
			return;

		if (!GposeService.GetIsGPose())
			return;

		// Clear bone selection.
		// This method should only be called while the user cannot interact with the bones.
		this.ClearSelection();

		// Take a snapshot of the current transforms
		var snapshot = this.TakeSnapshot();

		// Read skeleton transforms, starting from the root bones
		foreach (var rootBone in this.rootBones)
		{
			rootBone.ReadTransform(true, snapshot);
		}
	}

	public void ClearBones()
	{
		foreach (BoneVisual3d bone in this.Bones.Values)
		{
			bone.Dispose();
		}

		this.Bones.Clear();

		this.hairBones.Clear();
		this.metBones.Clear();
		this.topBones.Clear();
		this.mainHandBones.Clear();
		this.offHandBones.Clear();

		this.SelectedBones.Clear();
		this.HoverBones.Clear();
	}

	public async Task SetActor(ActorMemory actor)
	{
		if (this.Actor != null && this.Actor.ModelObject?.Transform != null)
			this.Actor.ModelObject.Transform.PropertyChanged -= this.OnTransformPropertyChanged;

		this.Actor = actor;

		if (actor.ModelObject?.Transform != null)
			actor.ModelObject.Transform.PropertyChanged += this.OnTransformPropertyChanged;

		this.Clear();

		await Dispatch.MainThread();

		this.ClearSelection();

		try
		{
			await Dispatch.MainThread();

			if (!GposeService.Instance.IsGpose)
				return;

			this.ClearBones();
			this.Children.Clear();

			if (this.Actor?.ModelObject?.Skeleton == null)
				return;

			// Get all bones
			this.AddBones(this.Actor.ModelObject.Skeleton);

			if (this.Actor.MainHand?.Model?.Skeleton != null)
				this.AddBones(this.Actor.MainHand.Model.Skeleton, "mh_");

			if (this.Actor.OffHand?.Model?.Skeleton != null)
				this.AddBones(this.Actor.OffHand.Model.Skeleton, "oh_");

			this.RaisePropertyChanged(nameof(SkeletonVisual3d.AllBones));
			this.RaisePropertyChanged(nameof(SkeletonVisual3d.HairBones));
			this.RaisePropertyChanged(nameof(SkeletonVisual3d.MetBones));
			this.RaisePropertyChanged(nameof(SkeletonVisual3d.TopBones));
			this.RaisePropertyChanged(nameof(SkeletonVisual3d.MainHandBones));
			this.RaisePropertyChanged(nameof(SkeletonVisual3d.OffHandBones));
			this.RaisePropertyChanged(nameof(SkeletonVisual3d.HasEquipmentBones));
			this.RaisePropertyChanged(nameof(SkeletonVisual3d.HasWeaponBones));

			if (!GposeService.Instance.IsGpose)
				return;

			// Create Bone links from the link database
			foreach ((string name, BoneVisual3d bone) in this.Bones)
			{
				foreach (LinkedBones.LinkSet links in LinkedBones.Links)
				{
					if (links.Tribe != null && this.Actor?.Customize?.Tribe != links.Tribe)
						continue;

					if (links.Gender != null && this.Actor?.Customize?.Gender != links.Gender)
						continue;

					if (!links.Contains(name))
						continue;

					foreach (string linkedBoneName in links.Bones)
					{
						if (linkedBoneName == name)
							continue;

						BoneVisual3d? linkedBone = this.GetBone(linkedBoneName);

						if (linkedBone == null)
							continue;

						bone.LinkedBones.Add(linkedBone);
					}
				}
			}

			// Read the initial transforms of all bones.
			foreach ((string name, BoneVisual3d bone) in this.Bones)
			{
				bone.ReadTransform();
			}

			// Check for ivcs bones
			this.IsIVCS = false;
			foreach ((string name, BoneVisual3d bone) in this.Bones)
			{
				if (name.StartsWith("iv_"))
				{
					this.IsIVCS = true;
					break;
				}
			}
		}
		catch (Exception)
		{
			throw;
		}
	}

	public void WriteSkeleton()
	{
		if (this.Actor == null || this.Actor.ModelObject?.Skeleton == null)
			return;

		if (this.CurrentBone != null && PoseService.Instance.IsEnabled)
		{
			lock (HistoryService.Instance.LockObject)
			{
				try
				{
					this.Actor.PauseSynchronization = true;
					this.CurrentBone.WriteTransform(this);
					this.Actor.PauseSynchronization = false;
				}
				catch (Exception ex)
				{
					Log.Error(ex, $"Failed to write bone transform: {this.CurrentBone.BoneName}");
					this.ClearSelection();
				}
			}
		}
	}

	private void AddBones(SkeletonMemory skeleton, string? namePrefix = null)
	{
		for (int partialSkeletonIndex = 0; partialSkeletonIndex < skeleton.Length; partialSkeletonIndex++)
		{
			PartialSkeletonMemory partialSkeleton = skeleton[partialSkeletonIndex];

			HkaPoseMemory? bestHkaPose = partialSkeleton.Pose1;

			if (bestHkaPose == null || bestHkaPose.Skeleton?.Bones == null || bestHkaPose.Skeleton?.ParentIndices == null || bestHkaPose.Transforms == null)
			{
				Log.Warning("Failed to find best HkaSkeleton for partial skeleton");
				continue;
			}

			int count = bestHkaPose.Transforms.Length;

			// Load all bones first
			for (int boneIndex = partialSkeletonIndex == 0 ? 0 : 1; boneIndex < count; boneIndex++)
			{
				string originalName = bestHkaPose.Skeleton.Bones[boneIndex].Name.ToString();
				string name = this.ConvertBoneName(namePrefix, originalName);

				TransformMemory? transform = bestHkaPose.Transforms[boneIndex];

				BoneVisual3d visual;
				if (this.Bones.ContainsKey(name))
				{
					visual = this.Bones[name];
				}
				else
				{
					// new bone
					visual = new BoneVisual3d(this, name);
					this.Bones.Add(name, visual);
				}

				// Do not allow modification of the root bone, things get weird.
				if (originalName == "n_root")
					visual.IsTransformLocked = true;

				// Ugh this whole mess here is /just/ for the pose matrix categories.
				if (namePrefix == "mh_")
				{
					this.mainHandBones.Add(visual);
				}
				else if (namePrefix == "oh_")
				{
					this.offHandBones.Add(visual);
				}
				else
				{
					if (originalName != "j_kao")
					{
						// Special logic to get the Hair, Met, and Helm bones for pose matrix.
						if (partialSkeletonIndex == 2)
						{
							this.hairBones.Add(visual);
						}
						else if (partialSkeletonIndex == 3)
						{
							this.metBones.Add(visual);
						}
						else if (partialSkeletonIndex == 4)
						{
							this.topBones.Add(visual);
						}
					}
				}

				visual.TransformMemories.Insert(0, transform);
			}

			// Set parents now all the bones are loaded
			for (int boneIndex = 0; boneIndex < count; boneIndex++)
			{
				int parentIndex = bestHkaPose.Skeleton.ParentIndices[boneIndex];
				string boneName = bestHkaPose.Skeleton.Bones[boneIndex].Name.ToString();
				boneName = this.ConvertBoneName(namePrefix, boneName);

				BoneVisual3d bone = this.Bones[boneName];

				if (bone.Parent != null || this.Children.Contains(bone))
					continue;

				try
				{
					if (parentIndex < 0)
					{
						// this bone has no parent, is root.
						this.Children.Add(bone);
					}
					else
					{
						string parentBoneName = bestHkaPose.Skeleton.Bones[parentIndex].Name.ToString();
						parentBoneName = this.ConvertBoneName(namePrefix, parentBoneName);
						bone.Parent = this.Bones[parentBoneName];
					}
				}
				catch (Exception ex)
				{
					Log.Error(ex, $"Failed to parent bone: {boneName}");
				}
			}

			// Find all root bones (bones without parents)
			this.rootBones.Clear();
			this.rootBones.AddRange(this.Bones.Values.Where(bone => bone.Parent == null));
		}
	}

	private string ConvertBoneName(string? prefix, string name)
	{
		if (prefix != null)
			name = prefix + name;

		return name;
	}

	private async void OnTransformPropertyChanged(object? sender, PropertyChangedEventArgs? e)
	{
		await Dispatch.MainThread();

		if (Application.Current == null)
			return;

		this.rootRotation.Quaternion = this.RootRotation.ToMedia3DQuaternion();
	}

	private void RaisePropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	private BoneVisual3d? FindHairBoneByPattern(string suffix)
	{
		string pattern = $@"j_ex_h\d{{4}}_ke_{suffix}";
		Regex regex = new Regex(pattern);

		foreach (var (boneName, bone) in this.Bones)
		{
			if (regex.IsMatch(boneName))
				return bone;
		}

		return null;
	}
}

#pragma warning disable SA1201
public interface IBone
{
	BoneVisual3d? Visual { get; }
}
