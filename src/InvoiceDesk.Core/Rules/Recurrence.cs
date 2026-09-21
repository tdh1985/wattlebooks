// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;

namespace InvoiceDesk.Core.Rules;

// dates are counted from the start each time so the 31st never drifts to the 28th
public static class Recurrence
{
    // after a long break only the latest year or so of drafts is worth making
    public const int CatchUpLimit = 12;

    public static DateOnly Occurrence(DateOnly start, RepeatEvery every, int n) => every switch
    {
        RepeatEvery.Weekly => start.AddDays(7 * n),
        RepeatEvery.Fortnightly => start.AddDays(14 * n),
        RepeatEvery.Monthly => start.AddMonths(n),
        RepeatEvery.Quarterly => start.AddMonths(3 * n),
        _ => start.AddYears(n),
    };

    public static List<(int N, DateOnly Date)> Due(DateOnly start, RepeatEvery every, int created, DateOnly? end, DateOnly today)
    {
        var due = new List<(int, DateOnly)>();
        for (var n = created; due.Count < CatchUpLimit; n++)
        {
            var date = Occurrence(start, every, n);
            if (date > today || (end is { } last && date > last)) break;
            due.Add((n, date));
        }
        return due;
    }

    public static int FirstFuture(DateOnly start, RepeatEvery every, int created, DateOnly today)
    {
        var n = created;
        while (Occurrence(start, every, n) <= today) n++;
        return n;
    }

    public static DateOnly? Next(DateOnly start, RepeatEvery every, int created, DateOnly? end)
    {
        var next = Occurrence(start, every, created);
        return end is { } last && next > last ? null : next;
    }
}
