// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;

namespace InvoiceDesk.Core.Rules;

public readonly record struct DateRange(DateOnly Start, DateOnly End)
{
    public bool Contains(DateOnly date) => date >= Start && date <= End;
}

public static class FinancialPeriods
{
    // month names stay invariant because en-AU abbreviates september as "sept"
    static readonly CultureInfo Months = CultureInfo.InvariantCulture;

    public static DateRange TaxYear(DateOnly d, CountryRules c)
    {
        var thisYear = new DateOnly(d.Year, c.TaxYearStartMonth, c.TaxYearStartDay);
        var start = d >= thisYear ? thisYear : thisYear.AddYears(-1);
        return new(start, start.AddYears(1).AddDays(-1));
    }

    public static string TaxYearLabel(DateOnly d, CountryRules c)
    {
        var start = TaxYear(d, c).Start.Year;
        return c.YearLabel switch
        {
            YearLabelStyle.FinancialYear => $"FY {start}–{(start + 1) % 100:00}",
            YearLabelStyle.TaxYear => $"Tax year {start}–{(start + 1) % 100:00}",
            _ => start.ToString(CultureInfo.InvariantCulture),
        };
    }

    // months is 1/2/3/6/12 and one period ends in endMonth
    public static DateRange ReturnPeriod(DateOnly d, int months, int endMonth)
    {
        var index = d.Year * 12 + d.Month - 1;
        var ahead = ((endMonth - 1 - index) % months + months) % months;
        var startIndex = index + ahead - months + 1;
        var start = new DateOnly(startIndex / 12, startIndex % 12 + 1, 1);
        return new(start, start.AddMonths(months).AddDays(-1));
    }

    public static DateRange PreviousReturnPeriod(DateOnly d, int months, int endMonth) =>
        ReturnPeriod(ReturnPeriod(d, months, endMonth).Start.AddDays(-1), months, endMonth);

    public static string ReturnPeriodLabel(DateRange p, CountryRules c)
    {
        if (p == TaxYear(p.Start, c)) return TaxYearLabel(p.Start, c);
        if (p.Start.Year == p.End.Year && p.Start.Month == p.End.Month) return p.End.ToString("MMM yyyy", Months);
        if (p.Start.Year == p.End.Year) return $"{p.Start.ToString("MMM", Months)}–{p.End.ToString("MMM", Months)} {p.End.Year}";
        return $"{p.Start.ToString("MMM yyyy", Months)} – {p.End.ToString("MMM yyyy", Months)}";
    }

    public static string PeriodNoun(int months) => months switch
    {
        1 => "month",
        3 => "quarter",
        12 => "year",
        _ => "period",
    };

    public static string YearNoun(CountryRules c) => c.YearLabel switch
    {
        YearLabelStyle.FinancialYear => "financial year",
        YearLabelStyle.TaxYear => "tax year",
        _ => "year",
    };

    public static DateRange Month(DateOnly d)
    {
        var start = new DateOnly(d.Year, d.Month, 1);
        return new(start, start.AddMonths(1).AddDays(-1));
    }
}
