// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Rules;

public enum AgeBucket { Current, Late1To30, Late31To60, Late61To90, LateOver90 }

// the usual accounts-receivable columns, by days past the due date
public sealed record AgedTotals(long Current, long Late1To30, long Late31To60, long Late61To90, long LateOver90)
{
    public long Total => Current + Late1To30 + Late31To60 + Late61To90 + LateOver90;
}

public static class Aging
{
    public static AgeBucket Bucket(DateOnly due, DateOnly asOf) => (asOf.DayNumber - due.DayNumber) switch
    {
        <= 0 => AgeBucket.Current,
        <= 30 => AgeBucket.Late1To30,
        <= 60 => AgeBucket.Late31To60,
        <= 90 => AgeBucket.Late61To90,
        _ => AgeBucket.LateOver90,
    };

    public static AgedTotals Totals(IEnumerable<(DateOnly Due, long Cents)> balances, DateOnly asOf)
    {
        var sums = new long[5];
        foreach (var (due, cents) in balances) sums[(int)Bucket(due, asOf)] += cents;
        return new AgedTotals(sums[0], sums[1], sums[2], sums[3], sums[4]);
    }
}
