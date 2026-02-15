// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Converters;

using Anamnesis.Actor.Refresh;
using System;
using System.Globalization;
using System.Windows.Data;

/// <summary>
/// Converts a <see cref="RefreshBlockedReason"/> to a localization key.
/// </summary>
[ValueConversion(typeof(RefreshBlockedReason), typeof(string))]
public class RefreshBlockedReasonToKeyConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value switch
		{
			RefreshBlockedReason.WorldFrozen => "Character_WarningGposeWorldPosFrozen",
			RefreshBlockedReason.PoseEnabled => "Character_WarningPoseEnabled",
			RefreshBlockedReason.OverworldInGpose => "Character_WarningOverworldInGpose",
			RefreshBlockedReason.IntegrationDisabled => "Character_WarningNoRefresher",
			_ => string.Empty,
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
