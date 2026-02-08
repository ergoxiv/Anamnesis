// © Anamnesis.
// Licensed under the MIT license.

namespace RemoteController.Interop.Types;

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Represents a Havok animation pose (hkaPose).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 0x50)]
public unsafe struct HkaPose
{
	/// <summary>
	/// Flags indicating the dirty state of bone transforms.
	/// </summary>
	[Flags]
	public enum BoneFlag : uint
	{
		/// <summary>
		/// The local-space transform is dirty and needs recalculation.
		/// </summary>
		LocalDirty = 1 << 0,

		/// <summary>
		/// The model-space transform is dirty and needs recalculation.
		/// </summary>
		ModelDirty = 1 << 1,
	}

	/// <summary>
	/// Pointer to the skeleton definition.
	/// </summary>
	[FieldOffset(0x00)]
	public nint Skeleton;

	/// <summary>
	/// Array of bone transforms in local (parent-relative) space.
	/// </summary>
	[FieldOffset(0x08)]
	public HkArray<HkaTransform4> LocalPose;

	/// <summary>
	/// Array of bone transforms in model (character-relative) space.
	/// </summary>
	[FieldOffset(0x18)]
	public HkArray<HkaTransform4> ModelPose;

	/// <summary>
	/// Array of flags indicating the dirty state of each bone.
	/// </summary>
	[FieldOffset(0x28)]
	public HkArray<uint> BoneFlags;

	/// <summary>
	/// A value indicating whether the model-space pose is synchronized.
	/// </summary>
	[FieldOffset(0x38)]
	public byte ModelInSync;

	/// <summary>
	/// A value indicating whether the local-space pose is synchronized.
	/// </summary>
	[FieldOffset(0x39)]
	public byte LocalInSync;

	/// <summary>
	/// Clears the model-dirty flag for the specified bone.
	/// </summary>
	/// <param name="boneIndex">The index of the bone.</param>
	public void ClearModelDirty(int boneIndex)
	{
		if (this.BoneFlags.IsValid && boneIndex < this.BoneFlags.Length)
			this.BoneFlags[boneIndex] &= ~(uint)BoneFlag.ModelDirty;
	}

	/// <summary>
	/// Gets a pointer to the model-space transform for the specified bone.
	/// </summary>
	/// <param name="boneIndex">The index of the bone.</param>
	/// <returns>
	/// A pointer to the transform, or null if invalid.
	/// </returns>
	public readonly HkaTransform4* GetModelPoseTransform(int boneIndex)
	{
		if (!this.ModelPose.IsValid || boneIndex < 0 || boneIndex >= this.ModelPose.Length)
			return null;

		return this.ModelPose.GetPointer(boneIndex);
	}
}
