// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Rules;

// negative days late means the client usually pays early
public sealed record PaymentHabit(int AverageDaysLate, int OnTimeCount, int Sample);

public static class PaymentHabits
{
    // one invoice is chance, two starts to look like a pattern
    public const int MinSample = 2;

    public static DateOnly? PaidDate(long totalCents, IEnumerable<(DateOnly Date, long AmountCents)> payments)
    {
        long sum = 0;
        foreach (var (date, amount) in payments.OrderBy(p => p.Date))
        {
            sum += amount;
            if (sum >= totalCents) return date;
        }
        return null;
    }

    public static PaymentHabit? From(IEnumerable<(DateOnly Due, DateOnly Paid)> paid)
    {
        var days = paid.Select(p => p.Paid.DayNumber - p.Due.DayNumber).ToList();
        if (days.Count < MinSample) return null;
        var average = (int)Math.Round(days.Average(), MidpointRounding.AwayFromZero);
        return new PaymentHabit(average, days.Count(d => d <= 0), days.Count);
    }

    public static DateOnly ExpectedDate(DateOnly due, PaymentHabit? habit, DateOnly today)
    {
        var expected = due.AddDays(habit?.AverageDaysLate ?? 0);
        return expected < today ? today : expected;
    }
}
