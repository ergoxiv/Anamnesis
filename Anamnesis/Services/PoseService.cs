// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Services;

using Anamnesis;
using Anamnesis.Core;
using Anamnesis.Files;
using PropertyChanged;
using RemoteController.IPC;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

public enum ParentingMode
{
	None,
	PositionOnly,
	Full,
}

[AddINotifyPropertyChangedInterface]
public class PoseService : ServiceBase<PoseService>
{
	private bool isEnabled;

	public delegate void PoseEvent(bool value);

	public static event PoseEvent? EnabledChanged;
	public static event PoseEvent? FreezeWorldStateEnabledChanged;

	public static string? SelectedBonesText { get; set; }

	public static Dictionary<string, Dictionary<string, Vector3>>? BonePosTranslations { get; private set; }

	public bool IsEnabled
	{
		get => this.isEnabled;
		set
		{
			if (this.isEnabled == value)
				return;

			this.SetEnabled(value);
		}
	}

	public bool FreezePhysics
	{
		get => ControllerService.Instance.SendDriverCommand<bool>(DriverCommand.GetFreezePhysics) ?? false;
		set => ControllerService.Instance.SendDriverCommand<bool>(DriverCommand.SetFreezePhysics, args: value);
	}

	public bool WorldStateNotFrozen => !this.FreezeWorldState;

	public bool FreezeWorldState
	{
		get => ControllerService.Instance.SendDriverCommand<bool>(DriverCommand.GetFreezeWorldVisualState) ?? false;
		set
		{
			ControllerService.Instance.SendDriverCommand<bool>(DriverCommand.SetFreezeWorldVisualState, args: value);
			this.RaisePropertyChanged(nameof(this.FreezeWorldState));
			this.RaisePropertyChanged(nameof(this.WorldStateNotFrozen));
			FreezeWorldStateEnabledChanged?.Invoke(value);
		}
	}

	public ParentingMode ParentingMode { get; set; } = ParentingMode.Full;

	public bool CanEdit { get; set; }

	/// <inheritdoc/>
	protected override IEnumerable<IService> Dependencies => [AddressService.Instance, ControllerService.Instance, GposeService.Instance];

	public static Dictionary<string, Vector3> ConvertToTarget(string source, string target)
	{
		if (BonePosTranslations == null)
			throw new Exception("BonePosTranslations not loaded.");

		if (!BonePosTranslations.ContainsKey(source))
			throw new Exception($"No translation found for source: {source}");

		if (!BonePosTranslations.ContainsKey(target))
			throw new Exception($"No translation found for target: {target}");

		var sourceOffsets = BonePosTranslations[source];
		var targetOffsets = BonePosTranslations[target];

		var allBones = new HashSet<string>(sourceOffsets.Keys);
		allBones.UnionWith(targetOffsets.Keys);

		var result = new Dictionary<string, Vector3>();

		foreach (var bone in allBones)
		{
			// Default to Vector3.Zero if a bone is missing in either dictionary
			sourceOffsets.TryGetValue(bone, out var sourceOffset);
			targetOffsets.TryGetValue(bone, out var targetOffset);

			// The difference to apply to the source pose to get the target pose
			var delta = targetOffset - sourceOffset;
			result[bone] = delta;
		}

		return result;
	}

	/// <inheritdoc/>
	public override async Task Initialize()
	{
		await base.Initialize();

		// Load translation data for bones
		try
		{
			BonePosTranslations = EmbeddedFileUtility.Load<Dictionary<string, Dictionary<string, Vector3>>>("Data/FaceOffsets.json");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to load bone translations");
		}
	}

	public override async Task Shutdown()
	{
		GposeService.GposeStateChanged -= this.OnGposeStateChanged;
		await base.Shutdown();
	}

	public void SetEnabled(bool enabled)
	{
		// Don't try to enable posing unless we are in gpose
		if (enabled && !GposeService.Instance.IsGpose)
			throw new Exception("Attempt to enable posing outside of gpose");

		if (this.isEnabled == enabled)
			return;

		// Send command to remote controller
		bool? result = ControllerService.Instance.SendDriverCommand<bool>(DriverCommand.SetPosingEnabled, args: enabled);
		if (result != true)
		{
			Log.Warning($"Failed to {(enabled ? "enable" : "disable")} posing via remote controller.");
			return;
		}

		this.isEnabled = enabled;

		// Freeze physics when posing is enabled
		this.FreezePhysics = enabled;
		this.ParentingMode = ParentingMode.Full;

		EnabledChanged?.Invoke(enabled);
		this.RaisePropertyChanged(nameof(this.IsEnabled));
	}

	protected override async Task OnStart()
	{
		await base.OnStart();
		GposeService.GposeStateChanged += this.OnGposeStateChanged;
	}

	private void OnGposeStateChanged(bool isGPose)
	{
		if (!isGPose)
		{
			this.isEnabled = false;
			EnabledChanged?.Invoke(false);
			this.RaisePropertyChanged(nameof(this.IsEnabled));
			this.RaisePropertyChanged(nameof(this.FreezeWorldState));
			this.RaisePropertyChanged(nameof(this.WorldStateNotFrozen));
		}
	}
}
