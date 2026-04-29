// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor.Utilities;

using Anamnesis.Memory;
using Anamnesis.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using cmColor = Anamnesis.Memory.Color;
using wpfColor = System.Windows.Media.Color;

public static class ColorData
{
	// Compile-time assertions to ensure that enums have expected starting value for correct address calculation.
	private const int GENDER_ASSERT = 1 / (ActorCustomizeMemory.Genders.Masculine == 0 ? 1 : 0);
	private const int TRIBE_ASSERT = 1 / (ActorCustomizeMemory.Tribes.Midlander == (ActorCustomizeMemory.Tribes)1 ? 1 : 0);

	private const int COLOR_BYTE_SIZE = 4;
	private const int UNIQUE_BASE_OFFSET = 0x4800;
	private const int CHUNK_BYTE_SIZE = 0x1400;
	private const int PALETTE_BYTE_SIZE = 0x400;

	private const int UNIQUE_BASE_INDEX = UNIQUE_BASE_OFFSET / COLOR_BYTE_SIZE;
	private const int CHUNK_COLORS_SIZE = CHUNK_BYTE_SIZE / COLOR_BYTE_SIZE;
	private const int COLORS_PER_PALETTE = PALETTE_BYTE_SIZE / COLOR_BYTE_SIZE;

	private static readonly Entry[] s_colors = [];
	private static readonly Dictionary<CustomizeColorOption, OptionConfig> s_configs = new()
	{
		{ CustomizeColorOption.Skin,           new() { OptionsCount = 192, PaletteIndex = 3,  IsUnique = true,  IsPaletteSplit = false } },
		{ CustomizeColorOption.Hair,           new() { OptionsCount = 208, PaletteIndex = 4,  IsUnique = true,  IsPaletteSplit = false } },
		{ CustomizeColorOption.HairHighlights, new() { OptionsCount = 192, PaletteIndex = 1,  IsUnique = false, IsPaletteSplit = false } },
		{ CustomizeColorOption.Eyes,           new() { OptionsCount = 192, PaletteIndex = 0,  IsUnique = false, IsPaletteSplit = false } },
		{ CustomizeColorOption.FacialFeature,  new() { OptionsCount = 192, PaletteIndex = 0,  IsUnique = false, IsPaletteSplit = false } },
		{ CustomizeColorOption.FacePaint,      new() { OptionsCount = 96,  PaletteIndex = 13, IsUnique = false, IsPaletteSplit = true } },
		{ CustomizeColorOption.Lips,           new() { OptionsCount = 96,  PaletteIndex = 13, IsUnique = false, IsPaletteSplit = true } },
	};

	static ColorData()
	{
		var colors = new List<Entry>();

		try
		{
			byte[]? buffer = GameDataService.GetFileData("chara/xls/charamake/human.cmp");
			if (buffer != null)
			{
				int at = 0;
				while (at < buffer.Length)
				{
					byte r = buffer[at];
					byte g = buffer[at + 1];
					byte b = buffer[at + 2];
					byte a = buffer[at + 3];

					colors.Add(new Entry
					{
						CmColor = new cmColor(r / 255.0f, g / 255.0f, b / 255.0f),
						WpfColor = wpfColor.FromArgb(a, r, g, b),
					});

					at += COLOR_BYTE_SIZE;
				}
			}
			else
			{
				Log.Warning("Game color data file is empty or missing. Character color data will not be available.");
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to read game color data.");
		}

		s_colors = colors.ToArray();
	}

	public enum CustomizeColorOption
	{
		Skin,
		Hair,
		HairHighlights,
		Eyes,
		FacialFeature,
		FacePaint,
		Lips,
	}

	private enum PaletteCategories : uint
	{
		Unk1 = 1,
		Unk2 = 2,
		Skin = 3,
		Hair = 4,
		Unk3 = 5,
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Entry[] GetSkin(ActorCustomizeMemory.Tribes tribe, ActorCustomizeMemory.Genders gender)
		=> GetColors(CustomizeColorOption.Skin, tribe, gender);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Entry[] GetHair(ActorCustomizeMemory.Tribes tribe, ActorCustomizeMemory.Genders gender)
		=> GetColors(CustomizeColorOption.Hair, tribe, gender);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Entry[] GetHairHighlightColors() => GetColors(CustomizeColorOption.HairHighlights);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Entry[] GetEyeColors() => GetColors(CustomizeColorOption.Eyes);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Entry[] GetFacialFeatureColor() => GetColors(CustomizeColorOption.FacialFeature);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Entry[] GetFacePaintColor() => GetColors(CustomizeColorOption.FacePaint);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Entry[] GetLipColors() => GetColors(CustomizeColorOption.Lips);

	internal static Entry[] GetColors(CustomizeColorOption option, ActorCustomizeMemory.Tribes tribe = default, ActorCustomizeMemory.Genders gender = default)
	{
		var config = s_configs[option];

		if (config.IsPaletteSplit)
		{
			var entries = new List<Entry>((config.OptionsCount * 2) + 32);

			int paintBaseIndex = config.PaletteIndex * COLORS_PER_PALETTE;
			entries.AddRange(Span(paintBaseIndex, config.OptionsCount));

			for (int i = 0; i < 32; i++)
			{
				entries.Add(new Entry { Skip = true });
			}

			int secondPaintIndex = paintBaseIndex + (COLORS_PER_PALETTE / 2);
			entries.AddRange(Span(secondPaintIndex, config.OptionsCount));

			return entries.ToArray();
		}

		int startIndex = config.IsUnique
			? GetEntryIndex(tribe, gender, (uint)config.PaletteIndex)
			: config.PaletteIndex * COLORS_PER_PALETTE;

		if (config.IsUnique)
		{
			Log.Verbose("Getting colors for Option: {Option}, Tribe: {Tribe}, Gender: {Gender}", option, tribe, gender);
			Log.Verbose("Calculated index: {Index}", startIndex);
		}

		return Span(startIndex, config.OptionsCount);
	}

	private static Entry[] Span(int from, int count)
	{
		if (s_colors.Length <= from)
			return new Entry[count];

		Entry[] entries = new Entry[count];
		int actualCount = Math.Min(count, s_colors.Length - from);
		Array.Copy(s_colors, from, entries, 0, actualCount);
		return entries;
	}

	private static int GetEntryIndex(ActorCustomizeMemory.Tribes tribe, ActorCustomizeMemory.Genders gender, uint paletteIndex)
	{
		int tribeGenderIndex = Math.Max(0, (((int)tribe - 1) * 2) + (int)gender);
		return (int)(UNIQUE_BASE_INDEX + (tribeGenderIndex * CHUNK_COLORS_SIZE) + (paletteIndex * COLORS_PER_PALETTE));
	}

	public struct Entry
	{
		public readonly string Hex => $"#{this.WpfColor.R:X2}{this.WpfColor.G:X2}{this.WpfColor.B:X2}";
		public cmColor CmColor { get; set; }
		public wpfColor WpfColor { get; set; }
		public bool Skip { get; set; }
	}

	private readonly struct OptionConfig
	{
		public int OptionsCount { get; init; }
		public int PaletteIndex { get; init; }
		public bool IsUnique { get; init; } // If true, then the palette is tribe-gender specific. Otherwise, it's a shared palette.
		public bool IsPaletteSplit { get; init; } // If true, palette is split into light/dark halves.
	}
}
