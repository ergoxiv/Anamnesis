// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.GameData.Sheets;

using Lumina.Excel;
using System.Collections.Generic;
using System.Linq;

public static class ExcelSheetExtensions
{
	public static IEnumerable<object> ToEnumerable<T>(this ExcelSheet<T> sheet)
		where T : struct, IExcelRow<T>
	{
		return sheet.Cast<object>();
	}
}
