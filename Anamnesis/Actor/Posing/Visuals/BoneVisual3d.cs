// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor;

using Anamnesis.Memory;
using Anamnesis.Services;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media.Media3D;
using XivToolsWpf.Meida3D;

using AnQuaternion = Anamnesis.Memory.Quaternion;
using AnVector = Anamnesis.Memory.Vector;
using MediaQuaternion = System.Windows.Media.Media3D.Quaternion;

public enum BoneCategory
{
	Body,
	Hair,
	Clothing,
	Helm,
	MainHand,
	OffHand,
}

public enum BoneState
{
	Unselected,
	Selected,
	Hovered,
}

// TODO: The Bone class will be used to represent a bone in the game's skeleton. It will have no visual representation. (i.e. the under-the-hood data)
[AddINotifyPropertyChangedInterface]
public class Bone : ITransform, INotifyPropertyChanged, IDisposable
{
	/// <summary>
	/// A list of transforms that represent the game's memory of the bone's position, rotation, and scale.
	/// </summary>
	/// <remarks>
	/// A bone can have multiple transform memories only if there are multiple of the same bone in the game's skeleton.
	/// </remarks>
	public readonly List<TransformMemory> TransformMemories = new();

	private static readonly HashSet<string> AttachmentBoneNames = new()
	{
		"n_buki_r",
		"n_buki_l",
		"j_buki_sebo_r",
		"j_buki_sebo_l",
	};

	private static bool scaleLinked = true;

	public Bone(string name, BoneCategory category)
	{
		this.Name = name;
		this.Category = category;
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	public string Name { get; set; }
	public Bone? Parent { get; private set; } = null;
	public bool HasParent => this.Parent != null;
	public HashSet<Bone> Children { get; private set; } = new();
	public HashSet<Bone> LinkedBones { get; set; } = new();
	public BoneCategory Category { get; set; }
	public bool IsTransformLocked { get; set; } = false;

	public bool CanTranslate => PoseService.Instance.FreezePositions && !this.IsTransformLocked;

	/// <summary>Gets or sets the parent-relative bone position.</summary>
	public AnVector Position { get; set; }
	public bool CanRotate => !this.IsTransformLocked && PoseService.Instance.FreezeRotation;

	/// <summary>Gets or sets the parent-relative bone rotation.</summary>
	public AnQuaternion Rotation { get; set; }
	public bool CanScale => !this.IsTransformLocked && PoseService.Instance.FreezeScale;
	public bool CanLinkScale => !this.IsAttachmentBone;
	public bool ScaleLinked
	{
		get
		{
			if (this.IsAttachmentBone)
				return true;

			return scaleLinked;
		}

		set => scaleLinked = value;
	}

	public AnVector Scale { get; set; }
	public bool IsAttachmentBone => AttachmentBoneNames.Contains(this.Name);
	public bool Enabled { get; set; } = true;

	public bool EnableLinkedBones
	{
		get
		{
			if (this.LinkedBones.Count <= 0)
				return false;

			return SettingsService.Current.PosingBoneLinks.Get(this.Name, true);
		}

		set
		{
			SettingsService.Current.PosingBoneLinks.Set(this.Name, value);

			foreach (Bone link in this.LinkedBones)
			{
				SettingsService.Current.PosingBoneLinks.Set(link.Name, value);
			}
		}
	}

	public virtual bool UpdateTransforms(bool updateChildren = false)
	{
		if (!this.Enabled)
			return false;

		// The transforms should be identical, so we can just use the first one's data
		var transform = this.TransformMemories[0];

		// Convert the character-relative transform into a parent-relative transform
		Point3D charRelPos = transform.Position.ToMedia3DPoint();
		MediaQuaternion charRelRot = transform.Rotation.ToMedia3DQuaternion();

		Point3D parentRelPos = default;
		MediaQuaternion parentRelRot = default;
		if (this.Parent != null)
		{
			var parentTransform = this.Parent.TransformMemories[0];
			Point3D parentCharRelPos = parentTransform.Position.ToMedia3DPoint();
			MediaQuaternion parentCharRelRot = parentTransform.Rotation.ToMedia3DQuaternion();
			parentCharRelRot.Invert();

			// Parent-relative position
			parentRelPos = (Point3D)(charRelPos - parentCharRelPos);

			// Parent-relative rotation
			parentRelRot = parentCharRelRot * charRelRot;

			// Unrotate bones, since we will transform them ourselves.
			RotateTransform3D rotTrans = new RotateTransform3D(new QuaternionRotation3D(parentCharRelRot));
			parentRelPos = rotTrans.Transform(parentRelPos);
		}

		// Store the new parent-relative transform data
		this.Position = parentRelPos.ToCmVector();
		this.Rotation = parentRelRot.ToCmQuaternion();

		if (!updateChildren)
			return true;

		// Propagate the transform update to the bone's children
		foreach (Bone child in this.Children)
		{
			child.UpdateTransforms(true);
		}

		return true;
	}

	public virtual void AddChild(Bone child)
	{
		child.Parent = this;
		this.Children.Add(child);
	}

	public virtual void RemoveChild(Bone child)
	{
		child.Parent = null;
		this.Children.Remove(child);
	}

	public virtual void AddLinkedBone(Bone bone)
	{
		this.LinkedBones.Add(bone);
		bone.LinkedBones.Add(this);
	}

	public virtual void RemoveLinkedBone(Bone bone)
	{
		this.LinkedBones.Remove(bone);
		bone.LinkedBones.Remove(this);
	}

	public Bone GetChild(string name)
	{
		foreach (Bone child in this.Children)
		{
			if (child.Name == name)
				return child;
		}

		throw new Exception("Bone not found: " + name);
	}

	public Bone? GetDescendant(string name)
	{
		Bone? descendant = null;
		foreach (Bone child in this.Children)
		{
			if (child.Name == name)
				return child;

			descendant = child.GetDescendant(name);
		}

		return descendant;
	}

	public bool HasDescendant(string name)
	{
		foreach (Bone child in this.Children)
		{
			if (child.Name == name)
				return true;

			if (child.HasDescendant(name))
				return true;
		}

		return false;
	}

	public bool HasDescendant(Bone target)
	{
		foreach (Bone child in this.Children)
		{
			if (child == target)
				return true;

			if (child.HasDescendant(target))
				return true;
		}

		return false;
	}

	public bool HasAncestor(string name)
	{
		if (this.Parent == null)
			return false;

		if (this.Parent.Name == name)
			return true;

		return this.Parent.HasAncestor(name);
	}

	public bool HasAncestor(Bone target)
	{
		if (this.Parent == null)
			return false;

		if (this.Parent == target)
			return true;

		return this.Parent.HasAncestor(target);
	}

	public virtual void Dispose()
	{
		foreach (Bone child in this.Children)
		{
			child.Dispose();
		}

		this.Children.Clear();
		this.LinkedBones.Clear();
		this.Parent = null;
	}

	public override string ToString()
	{
		return base.ToString() + "(" + this.Name + ")";
	}
}

// TODO: The BoneVisual3D class will the visual component used in the 3D view of the posing tab.
public class BoneVisual3d : ModelVisual3D
{
	private readonly System.Windows.Media.Color lineColor = System.Windows.Media.Colors.Gray;

	private BoneVisual3d? parent;
	private Line? lineToParent;

	// TODO: Link to parent bone (Line)
	public BoneVisual3d(Bone bone)
	{
		this.Bone = bone;
		this.Bone.PropertyChanged += this.OnBonePropertyChanged;
	}

	public Bone Bone { get; private set; }

	public BoneVisual3d? Parent
	{
		get => this.parent;

		private set
		{
			if (this.parent == value)
				return;

			// Remove the old line if it exists to prevent memory leaks
			this.parent?.Children.Remove(this.lineToParent);

			this.parent = value;

			// Initialize the line if it hasn't been created yet
			if (this.parent != null)
			{
				if (this.lineToParent == null)
				{
					this.lineToParent = new Line
					{
						Color = this.lineColor,
					};
					this.lineToParent.Points.Add(new Point3D(0, 0, 0));
					this.lineToParent.Points.Add(new Point3D(0, 0, 0));
				}

				// Add the line to the new parent's children
				this.parent.Children.Add(this.lineToParent);
			}
		}
	}

	public BoneState State { get; set; } = BoneState.Unselected;

	public virtual string TooltipKey => "Pose_" + this.Bone.Name;

	public string Tooltip
	{
		get
		{
			string? customName = CustomBoneNameService.GetBoneName(this.Bone.Name);
			if (!string.IsNullOrEmpty(customName))
				return customName;

			string str = LocalizationService.GetString(this.TooltipKey, true);
			return string.IsNullOrEmpty(str) ? this.Bone.Name : str;
		}

		set
		{
			if (string.IsNullOrEmpty(value) || LocalizationService.GetString(this.TooltipKey, true) == value)
			{
				CustomBoneNameService.SetBoneName(this.Bone.Name, null);
			}
			else
			{
				CustomBoneNameService.SetBoneName(this.Bone.Name, value);
			}
		}
	}

	public void AddChild(BoneVisual3d child)
	{
		child.Parent = this;
		this.Children.Add(child);
	}

	public void RemoveChild(BoneVisual3d child)
	{
		child.Parent = null;
		this.Children.Remove(child);
	}

	public bool HasAncestor(BoneVisual3d target)
	{
		if (this.Parent == null)
			return false;

		if (this.Parent == target)
			return true;

		return this.Parent.HasAncestor(target);
	}

	private void OnBonePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// TODO: Update Media3D transform hierarchy
		if (e.PropertyName == nameof(Actor.Bone.Position))
		{
			// TODO: Update line position
			// Draw a line for visualization
			/* if (this.Parent != null && this.lineToParent != null)
			{
				Point3D p = this.lineToParent.Points[1];
				p.X = position.X;
				p.Y = position.Y;
				p.Z = position.Z;
				this.lineToParent.Points[1] = p;
			} */
			throw new NotImplementedException();
		}
		else if (e.PropertyName == nameof(Actor.Bone.Rotation))
		{
			throw new NotImplementedException();
		}
		else if (e.PropertyName == nameof(Actor.Bone.Scale))
		{
			throw new NotImplementedException();
		}
	}
}

/*
using System;
using Anamnesis.Actor.Extensions;
using Anamnesis.Posing.Visuals;
using MaterialDesignThemes.Wpf;

using Quaternion = System.Windows.Media.Media3D.Quaternion;

[AddINotifyPropertyChangedInterface]
public class BoneVisual3d : ModelVisual3D, ITransform, IBone, IDisposable
{
	private readonly QuaternionRotation3D rotation;
	private readonly TranslateTransform3D position;
	private BoneTargetVisual3d? target;

	public BoneVisual3d(SkeletonVisual3d skeleton, string name)
	{
		this.Skeleton = skeleton;

		this.rotation = new QuaternionRotation3D();

		RotateTransform3D rot = new RotateTransform3D();
		rot.Rotation = this.rotation;
		this.position = new TranslateTransform3D();

		Transform3DGroup transformGroup = new Transform3DGroup();
		transformGroup.Children.Add(rot);
		transformGroup.Children.Add(this.position);

		this.Transform = transformGroup;

		this.target = new BoneTargetVisual3d(this);
		this.Children.Add(this.target);

		this.BoneName = name;
	}

	public TransformMemory TransformMemory => this.TransformMemories[0];

	public BoneVisual3d? Visual => this;

	public void Dispose()
	{
		this.Children.Clear();
		this.target?.Dispose();
		this.target = null;

		this.parent?.Children.Remove(this);

		if (this.lineToParent != null)
		{
			this.parent?.Children.Remove(this.lineToParent);
			this.lineToParent.Dispose();
			this.lineToParent = null;
		}

		this.parent = null;
	}

	public virtual void Tick()
	{
		foreach (TransformMemory transformMemory in this.TransformMemories)
		{
			transformMemory.Tick();
		}
	}

	public virtual void WriteTransform(ModelVisual3D root, bool writeChildren = true, bool writeLinked = true)
	{
		if (!this.IsEnabled)
			return;

		if (HistoryService.IsRestoring)
			return;

		foreach (TransformMemory transformMemory in this.TransformMemories)
		{
			transformMemory.EnableReading = false;
		}

		// Apply the current values to the visual tree
		this.rotation.Quaternion = this.Rotation.ToMedia3DQuaternion();
		this.position.OffsetX = this.Position.X;
		this.position.OffsetY = this.Position.Y;
		this.position.OffsetZ = this.Position.Z;

		// convert the values in the tree to character-relative space
		MatrixTransform3D transform;

		try
		{
			transform = (MatrixTransform3D)this.TransformToAncestor(root);
		}
		catch (Exception ex)
		{
			throw new Exception($"Failed to transform bone: {this.BoneName} to root", ex);
		}

		Quaternion rotation = transform.Matrix.ToQuaternion();
		rotation.Invert();

		CmVector position = default;
		position.X = (float)transform.Matrix.OffsetX;
		position.Y = (float)transform.Matrix.OffsetY;
		position.Z = (float)transform.Matrix.OffsetZ;

		// and push those values to the game memory
		bool changed = false;
		foreach (TransformMemory transformMemory in this.TransformMemories)
		{
			if (this.CanTranslate && !transformMemory.Position.IsApproximately(position))
			{
				transformMemory.Position = position;
				changed = true;
			}

			if (this.CanScale && !transformMemory.Scale.IsApproximately(this.Scale))
			{
				transformMemory.Scale = this.Scale;
				changed = true;
			}

			if (this.CanRotate)
			{
				CmQuaternion newRot = rotation.ToCmQuaternion();
				if (!transformMemory.Rotation.IsApproximately(newRot))
				{
					transformMemory.Rotation = newRot;
					changed = true;
				}
			}
		}

		if (changed)
		{
			if (writeLinked && this.EnableLinkedBones)
			{
				foreach (BoneVisual3d link in this.LinkedBones)
				{
					link.Rotation = this.Rotation;
					link.WriteTransform(root, writeChildren, false);
				}
			}

			if (writeChildren)
			{
				foreach (Visual3D child in this.Children)
				{
					if (child is BoneVisual3d childBone)
					{
						if (PoseService.Instance.EnableParenting)
						{
							childBone.WriteTransform(root);
						}
						else
						{
							childBone.ReadTransform(true);
						}
					}
				}
			}
		}

		foreach (TransformMemory transformMemory in this.TransformMemories)
		{
			transformMemory.EnableReading = true;
		}
	}

	public void GetChildren(ref List<BoneVisual3d> bones)
	{
		foreach (Visual3D? child in this.Children)
		{
			if (child is BoneVisual3d childBoneVisual)
			{
				bones.Add(childBoneVisual);
				childBoneVisual.GetChildren(ref bones);
			}
		}
	}
} */
