// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Core;

using Anamnesis.Actor;
using Anamnesis.Memory;
using Anamnesis.Services;
using PropertyChanged;
using System.Collections.Generic;
using System.Numerics;
using XivToolsWpf.Math3D.Extensions;

public enum BoneCategory
{
	Root,
	Hair,
	Met,
	Top,
	MainHand,
	OffHand,
}

/// <summary>
/// Represents a bone in a skeleton, providing mechanisms for reading and writing transforms,
/// synchronizing with memory, and handling linked bones.
/// </summary>
[AddINotifyPropertyChangedInterface]
public class Bone : ITransform
{
	private const float EqualityTolerance = 0.00001f;
	private static readonly HashSet<string> AttachmentBoneNames = new() { "n_buki_r", "n_buki_l", "j_buki_sebo_r", "j_buki_sebo_l" };
	private static bool scaleLinked = true;

	private readonly object transformLock = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="Bone"/> class.
	/// </summary>
	/// <param name="skeleton">The skeleton to which this bone belongs.</param>
	/// <param name="name">The bone's internal name.</param>
	/// <param name="category">The bone's category.</param>
	/// <param name="parent">The parent bone, if any.</param>
	public Bone(Skeleton skeleton, string name, BoneCategory category, Bone? parent = null)
	{
		this.Skeleton = skeleton;
		this.Name = name;
		this.Category = category;
		this.Parent = parent;
	}

	/// <summary>Gets the bone's category.</summary>
	public BoneCategory Category { get; }

	/// <summary>Gets the skeleton to which this bone belongs.</summary>
	public Skeleton Skeleton { get; private set; }

	/// <summary>Gets all transform memory objects linked to this bone.</summary>
	public List<TransformMemory> TransformMemories { get; } = new();

	/// <summary>Gets the primary transform memory object for this bone.</summary>
	public TransformMemory TransformMemory => this.TransformMemories[0];

	/// <summary> Gets or sets the parent bone of this bone.</summary>
	public Bone? Parent { get; protected set; }

	/// <summary>Gets or sets the list of child bones of this bone.</summary>
	public List<Bone> Children { get; protected set; } = new();

	/// <summary>Gets or sets the bone's internal name.</summary>
	public string Name { get; set; }

	/// <summary>Gets or sets the list of bones linked to this bone.</summary>
	public List<Bone> LinkedBones { get; set; } = new();

	/// <summary> Gets or sets a value indicating whether the transform of this bone is locked.</summary>
	public bool IsTransformLocked { get; set; } = false;

	/// <inheritdoc/>
	public bool CanTranslate => PoseService.Instance.FreezePositions && !this.IsTransformLocked;

	/// <summary>
	/// Gets or sets the parent-relative position of the bone.
	/// If the bone has no parent, this value will be relative to the root of the skeleton.
	/// </summary>
	public Vector3 Position { get; set; }

	/// <inheritdoc/>
	public bool CanRotate => PoseService.Instance.FreezeRotation && !this.IsTransformLocked;

	/// <summary>
	/// Gets or sets the parent-relative rotation of the bone.
	/// If the bone has no parent, this value will be relative to the root of the skeleton.
	/// </summary>
	public Quaternion Rotation { get; set; }

	/// <inheritdoc/>
	public bool CanScale => PoseService.Instance.FreezeScale && !this.IsTransformLocked;

	/// <summary>Gets or sets the scale of the bone.</summary>
	public Vector3 Scale { get; set; }

	/// <summary>Gets a value indicating whether this bone is an attachment bone.</summary>
	/// <remarks>
	/// Attachment bones are bones that are used to attach items to a character, such as weapons or shields.
	/// </remarks>
	public bool IsAttachmentBone => AttachmentBoneNames.Contains(this.Name);

	/// <inheritdoc/>
	public bool CanLinkScale => !this.IsAttachmentBone;

	/// <inheritdoc/>
	public bool ScaleLinked
	{
		get => this.IsAttachmentBone || scaleLinked;
		set => scaleLinked = value;
	}

	/// <summary>Gets or sets a value indicating whether linked bones are enabled.</summary>
	public bool EnableLinkedBones
	{
		get => this.LinkedBones.Count > 0 && SettingsService.Current.PosingBoneLinks.Get(this.Name, true);
		set
		{
			SettingsService.Current.PosingBoneLinks.Set(this.Name, value);
			foreach (var link in this.LinkedBones)
			{
				SettingsService.Current.PosingBoneLinks.Set(link.Name, value);
			}
		}
	}

	/// <summary>Synchronizes the bone with its transform memories.</summary>
	public virtual void Synchronize()
	{
		foreach (TransformMemory transformMemory in this.TransformMemories)
			transformMemory.Synchronize();

		this.ReadTransform();
	}

	/// <summary>Reads the transform of the bone from game memory or a snapshot.</summary>
	/// <remarks>
	/// Snapshots are primarily used by the skeleton object to optimize memory reads.
	/// </remarks>
	/// <param name="readChildren">Whether to read the transforms of child bones.</param>
	/// <param name="snapshot">An optional snapshot of transforms to use instead of memory.</param>
	public virtual void ReadTransform(bool readChildren = false, Dictionary<string, Transform>? snapshot = null)
	{
		lock (this.transformLock)
		{
			// Use snapshot if available, otherwise use values from memory
			Transform newTransform;
			if (snapshot != null && snapshot.TryGetValue(this.Name, out var transform))
			{
				newTransform = transform;
			}
			else
			{
				newTransform = new Transform
				{
					Position = this.TransformMemory.Position,
					Rotation = this.TransformMemory.Rotation,
					Scale = this.TransformMemory.Scale,
				};
			}

			// Convert the character-relative transform into a parent-relative transform
			if (this.Parent != null)
			{
				Transform parentTransform;
				if (snapshot != null && snapshot.TryGetValue(this.Parent.Name, out var parentSnapshot))
				{
					parentTransform = parentSnapshot;
				}
				else
				{
					parentTransform = new Transform
					{
						Position = this.Parent.TransformMemory.Position,
						Rotation = this.Parent.TransformMemory.Rotation,
						Scale = this.Parent.TransformMemory.Scale,
					};
				}

				Vector3 parentPosition = parentTransform.Position;
				Quaternion parentRot = Quaternion.Normalize(parentTransform.Rotation);
				parentRot = Quaternion.Inverse(parentRot);

				// Relative position
				newTransform.Position -= parentPosition;

				// Unrotate bones, since we will transform them ourselves.
				Matrix4x4 rotMatrix = Matrix4x4.CreateFromQuaternion(parentRot);
				newTransform.Position = Vector3.Transform(newTransform.Position, rotMatrix);

				// Relative rotation
				newTransform.Rotation = Quaternion.Normalize(Quaternion.Multiply(parentRot, newTransform.Rotation));
			}

			this.Position = newTransform.Position;
			this.Rotation = newTransform.Rotation;
			this.Scale = newTransform.Scale;

			if (readChildren)
			{
				foreach (var child in this.Children)
				{
					child.ReadTransform(true, snapshot);
				}
			}
		}
	}

	/// <summary>Writes the transform of the bone to game memory.</summary>
	/// <param name="root">The root bone of the skeleton.</param>
	/// <param name="writeChildren">Whether to write the transforms of child bones.</param>
	/// <param name="writeLinked">Whether to write the transforms of linked bones.</param>
	public virtual void WriteTransform(Bone root, bool writeChildren = true, bool writeLinked = true)
	{
		lock (this.transformLock)
		{
			foreach (TransformMemory transformMemory in this.TransformMemories)
			{
				transformMemory.EnableReading = false;
			}

			bool changed = false;
			foreach (TransformMemory transformMemory in this.TransformMemories)
			{
				if (this.CanTranslate && !transformMemory.Position.IsApproximately(this.Position, EqualityTolerance))
				{
					transformMemory.Position = this.Position;
					changed = true;
				}

				if (this.CanScale && !transformMemory.Scale.IsApproximately(this.Scale, EqualityTolerance))
				{
					transformMemory.Scale = this.Scale;
					changed = true;
				}

				if (this.CanRotate && !transformMemory.Rotation.IsApproximately(this.Rotation, EqualityTolerance))
				{
					transformMemory.Rotation = this.Rotation;
					changed = true;
				}
			}

			if (changed)
			{
				if (writeLinked && this.EnableLinkedBones)
				{
					foreach (var link in this.LinkedBones)
					{
						link.Rotation = this.Rotation;
						link.WriteTransform(root, writeChildren, false);
					}
				}

				if (writeChildren)
				{
					foreach (var child in this.Children)
					{
						if (PoseService.Instance.EnableParenting)
							child.WriteTransform(root);
						else
							child.ReadTransform(true);
					}
				}
			}

			foreach (TransformMemory transformMemory in this.TransformMemories)
			{
				transformMemory.EnableReading = true;
			}
		}
	}

	/// <summary>Determines whether the bone has the specified target bone as an ancestor.</summary>
	/// <param name="target">The target bone to check.</param>
	/// <returns>True if the target bone is an ancestor of this bone; otherwise, false.</returns>
	public bool HasAncestor(Bone target) => this.Parent != null && (this.Parent == target || this.Parent.HasAncestor(target));

	/// <summary>Returns a string that represents the current object.</summary>
	/// <returns>A string that represents the current object.</returns>
	public override string ToString() => base.ToString() + "(" + this.Name + ")";
}
