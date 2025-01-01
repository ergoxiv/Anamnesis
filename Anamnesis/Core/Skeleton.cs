// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Core;

using Anamnesis.Actor;
using Anamnesis.Actor.Posing;
using Anamnesis.Memory;
using Anamnesis.Posing;
using Anamnesis.Services;
using PropertyChanged;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

[AddINotifyPropertyChangedInterface]
public class Skeleton : INotifyPropertyChanged
{
	public readonly Dictionary<string, Bone> Bones = new();

	private readonly List<BoneVisual3d> rootBones = new();

	private readonly Dictionary<string, Tuple<string, string>> hairNameToSuffixMap = new()
	{
		{ "HairAutoFrontLeft", new("l", "j_kami_f_l") },	// Hair, Front Left
		{ "HairAutoFrontRight", new("r", "j_kami_f_r") },	// Hair, Front Right
		{ "HairAutoA", new("a", "j_kami_a") },				// Hair, Back Up
		{ "HairAutoB", new("b", "j_kami_b") },				// Hair, Back Down
		{ "HairFront", new("f", string.Empty) },			// Hair, Front (Custom Bone Name)
	};

	public Skeleton()
	{
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public ActorMemory? Actor { get; private set; }

	public bool HasEquipmentBones => this.Bones.Any(x => x.Value.Category == BoneCategory.Met || x.Value.Category == BoneCategory.Top);
	public bool HasWeaponBones => this.Bones.Any(x => x.Value.Category == BoneCategory.MainHand || x.Value.Category == BoneCategory.OffHand);

	public Quaternion RootRotation
	{
		get
		{
			return this.Actor?.ModelObject?.Transform?.Rotation ?? Quaternion.Identity;
		}
	}

	public bool HasTail => this.Actor?.Customize?.Race == ActorCustomizeMemory.Races.Miqote
		|| this.Actor?.Customize?.Race == ActorCustomizeMemory.Races.AuRa
		|| this.Actor?.Customize?.Race == ActorCustomizeMemory.Races.Hrothgar
		|| this.IsIVCS;

	public bool IsStandardFace => this.Actor == null || (!this.IsMiqote && !this.IsHrothgar && !this.IsViera);
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
			// If the skeleton is not initialized, we can't determine if it's a pre-DT face.
			if (this.Bones.Count == 0)
				return false;

			// We can determine if we have a DT-updated face if we have a tongue bone.
			// EW faces don't have this bone, where as all updated faces in DT have it.
			// It would be better to enumerate all of the faces and be more specific.
			Bone? tongueABone = this.GetBone("j_f_bero_01");
			if (tongueABone == null)
				return true;
			return false;
		}
	}

	private static ILogger Log => Serilog.Log.ForContext<Skeleton>();

	public static List<Bone> SortBonesByHierarchy(IEnumerable<Bone> bones)
	{
		return bones.OrderBy(bone => GetBoneDepth(bone)).ToList();
	}

	public static int GetBoneDepth(Bone bone)
	{
		int depth = 0;
		while (bone.Parent != null)
		{
			depth++;
			bone = bone.Parent;
		}

		return depth;
	}

	public virtual void Clear()
	{
		this.Bones.Clear();
	}

	public Bone? GetBone(string name)
	{
		if (this.Actor?.ModelObject?.Skeleton == null)
			return null;

		// only show actors that have atleast one partial skeleton
		if (this.Actor.ModelObject.Skeleton.Length <= 0)
			return null;

		string? modernName = LegacyBoneNameConverter.GetModernName(name);
		if (modernName != null)
			name = modernName;

		Bone? bone;

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

	public async Task SetActor(ActorMemory actor)
	{
		if (this.Actor != null && this.Actor.ModelObject?.Transform != null)
			this.Actor.ModelObject.Transform.PropertyChanged -= this.OnTransformPropertyChanged;

		this.Actor = actor;

		if (actor.ModelObject?.Transform != null)
			actor.ModelObject.Transform.PropertyChanged += this.OnTransformPropertyChanged;

		this.Clear();

		try
		{
			if (!GposeService.Instance.IsGpose)
				return;

			if (this.Actor?.ModelObject?.Skeleton == null)
				return;

			// Get all bones
			this.AddBones(this.Actor.ModelObject.Skeleton);

			if (this.Actor.MainHand?.Model?.Skeleton != null)
				this.AddBones(this.Actor.MainHand.Model.Skeleton, "mh_");

			if (this.Actor.OffHand?.Model?.Skeleton != null)
				this.AddBones(this.Actor.OffHand.Model.Skeleton, "oh_");

			// Create Bone links from the link database
			foreach ((string name, Bone bone) in this.Bones)
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

						Bone? linkedBone = this.GetBone(linkedBoneName);

						if (linkedBone == null)
							continue;

						bone.LinkedBones.Add(linkedBone);
					}
				}
			}

			// Read the initial transforms of all bones.
			// TODO: You can do reads in parallel
			foreach ((string name, Bone bone) in this.Bones)
			{
				bone.ReadTransform();
			}

			// Check for ivcs bones
			this.IsIVCS = false;
			foreach ((string name, Bone bone) in this.Bones)
			{
				if (name.StartsWith("iv_"))
				{
					this.IsIVCS = true;
					break;
				}
			}

			// Notify that the skeleton has changed.
			// All properties that depend on the skeleton are prompted to update.
			this.RaisePropertyChanged(string.Empty);
		}
		catch (Exception)
		{
			throw;
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
					BoneCategory category = BoneCategory.GetCategory(name);

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

	private void OnTransformPropertyChanged(object? sender, PropertyChangedEventArgs? e)
	{
		this.RaisePropertyChanged(nameof(this.RootRotation));
	}

	private Bone? FindHairBoneByPattern(string suffix)
	{
		string pattern = $@"j_ex_h\d{{4}}_ke_{suffix}";
		Regex regex = new(pattern);

		foreach (var (boneName, bone) in this.Bones)
		{
			if (regex.IsMatch(boneName))
				return bone;
		}

		return null;
	}

	private void RaisePropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
