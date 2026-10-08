// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;

namespace Wattlebooks.Core.Rules;

public static class InvoiceNumbering
{
    public static string Format(string prefix, int number, int padding) =>
        prefix + number.ToString(CultureInfo.InvariantCulture).PadLeft(Math.Max(1, padding), '0');
}
