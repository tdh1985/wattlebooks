// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;

namespace InvoiceDesk.Core.Rules;

public readonly record struct DateRange(DateOnly Start, DateOnly End)
{
    public bool Contains(DateOnly date) => date >= Start && date <= End;
}

public static class FinancialPeriods
{
    // en-AU abbreviates september as "sept", ato quarter labels use three letters
    static readonly CultureInfo Months = CultureInfo.InvariantCulture;

    public static DateRange FinancialYear(DateOnly d)
    {
        var start = d.Month >= 7 ? d.Year : d.Year - 1;
        return new(new DateOnly(start, 7, 1), new DateOnly(start + 1, 6, 30));
    }

    public static DateRange BasQuarter(DateOnly d)
    {
        var start = new DateOnly(d.Year, (d.Month - 1) / 3 * 3 + 1, 1);
        return new(start, start.AddMonths(3).AddDays(-1));
    }

    public static DateRange PreviousBasQuarter(DateOnly d) => BasQuarter(BasQuarter(d).Start.AddDays(-1));

    public static IReadOnlyList<DateRange> BasQuartersIn(DateRange financialYear) =>
        Enumerable.Range(0, 4).Select(i => BasQuarter(financialYear.Start.AddMonths(i * 3))).ToList();

    public static DateRange Month(DateOnly d)
    {
        var start = new DateOnly(d.Year, d.Month, 1);
        return new(start, start.AddMonths(1).AddDays(-1));
    }

    public static string FinancialYearLabel(DateOnly d)
    {
        var start = FinancialYear(d).Start.Year;
        return $"FY {start}–{(start + 1) % 100:00}";
    }

    public static string BasQuarterLabel(DateOnly d)
    {
        var q = BasQuarter(d);
        return $"{q.Start.ToString("MMM", Months)}–{q.End.ToString("MMM", Months)} {q.End.Year}";
    }
}
