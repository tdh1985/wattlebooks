// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed record RepeatInfo(int Id, RepeatEvery Every, bool IsPaused, DateOnly? NextDate, DateOnly? EndDate);

public sealed record RepeatSummary(
    int Id, RepeatEvery Every, bool IsPaused, DateOnly? NextDate, DateOnly? EndDate,
    string ClientName, int LatestInvoiceId, string LatestNumber, long LatestTotalCents, string Currency);

// repeating invoices only ever make drafts, so nothing goes to a client unseen
public sealed class RecurringService(IDbContextFactory<AppDbContext> factory, InvoiceService invoices, TimeProvider clock)
{
    readonly SemaphoreSlim _gate = new(1, 1);

    public event Action? Changed;

    public async Task<RecurringSchedule> CreateAsync(int invoiceId, RepeatEvery every, DateOnly? end)
    {
        await using var db = await factory.CreateDbContextAsync();
        var inv = await db.Invoices.FindAsync(invoiceId) ?? throw new ValidationException("This invoice no longer exists.");
        if (inv.Kind == InvoiceKind.Quote) throw new ValidationException("Quotes can't repeat. Turn it into an invoice first.");
        if (inv.Status == InvoiceStatus.Void) throw new ValidationException("Void invoices can't repeat.");
        if (inv.RecurringScheduleId is not null) throw new ValidationException("This invoice already repeats.");

        // the invoice it starts from counts as the first one made
        var schedule = new RecurringSchedule
        {
            Every = every, StartDate = inv.IssueDate, OccurrencesCreated = 1, EndDate = end, CreatedAt = clock.Now(),
        };
        // repeating an older invoice starts from the next date, not a pile of back-dated drafts
        SkipPast(schedule);
        if (NextOf(schedule) is null) throw new ValidationException("The end date is before the next draft, so nothing would repeat.");
        db.Add(schedule);
        await db.SaveChangesAsync();
        inv.RecurringScheduleId = schedule.Id;
        await db.SaveChangesAsync();
        Changed?.Invoke();
        return schedule;
    }

    public async Task<RepeatInfo?> GetForInvoiceAsync(int invoiceId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var id = await db.Invoices.Where(i => i.Id == invoiceId).Select(i => i.RecurringScheduleId).SingleOrDefaultAsync();
        if (id is null) return null;
        var s = await db.Set<RecurringSchedule>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);
        return s is null ? null : new RepeatInfo(s.Id, s.Every, s.IsPaused, NextOf(s), s.EndDate);
    }

    public async Task<List<RepeatSummary>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var schedules = await db.Set<RecurringSchedule>().AsNoTracking().ToListAsync();
        var linked = await db.Invoices.AsNoTracking().AsSplitQuery()
            .Include(i => i.Client).Include(i => i.Lines)
            .Where(i => i.RecurringScheduleId != null)
            .ToListAsync();

        return schedules
            .Select(s => (s, latest: linked.Where(i => i.RecurringScheduleId == s.Id)
                .OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Id).FirstOrDefault()))
            .Where(x => x.latest is not null)
            .Select(x => new RepeatSummary(x.s.Id, x.s.Every, x.s.IsPaused, NextOf(x.s), x.s.EndDate,
                x.latest!.Client?.Name ?? "", x.latest.Id, x.latest.Number, x.latest.Totals().TotalCents, x.latest.Currency))
            .OrderBy(r => r.IsPaused).ThenBy(r => r.NextDate ?? DateOnly.MaxValue)
            .ToList();
    }

    public Task PauseAsync(int id) => UpdateAsync(id, s => s.IsPaused = true);

    // dates missed while paused are skipped, since a pause usually means work stopped
    public Task ResumeAsync(int id) => UpdateAsync(id, s =>
    {
        s.IsPaused = false;
        SkipPast(s);
    });

    void SkipPast(RecurringSchedule s) =>
        s.OccurrencesCreated = Recurrence.FirstFuture(s.StartDate, s.Every, s.OccurrencesCreated, clock.Today());

    public async Task StopAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var schedule = await db.Set<RecurringSchedule>().FindAsync(id);
        if (schedule is null) return;
        // the invoices stay, they just stop pointing at a schedule
        await db.Invoices.Where(i => i.RecurringScheduleId == id).ExecuteUpdateAsync(u => u.SetProperty(i => i.RecurringScheduleId, (int?)null));
        db.Remove(schedule);
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task<int> GenerateDueAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var today = clock.Today();
            var made = 0;
            List<RecurringSchedule> schedules;
            await using (var db = await factory.CreateDbContextAsync())
                schedules = await db.Set<RecurringSchedule>().AsNoTracking().Where(s => !s.IsPaused).ToListAsync();

            foreach (var schedule in schedules)
            {
                foreach (var (n, date) in Recurrence.Due(schedule.StartDate, schedule.Every, schedule.OccurrencesCreated, schedule.EndDate, today))
                {
                    if (await MakeDraftAsync(schedule.Id, date)) made++;
                    await using var db = await factory.CreateDbContextAsync();
                    await db.Set<RecurringSchedule>().Where(s => s.Id == schedule.Id)
                        .ExecuteUpdateAsync(u => u.SetProperty(s => s.OccurrencesCreated, n + 1));
                }
            }
            if (made > 0) Changed?.Invoke();
            return made;
        }
        finally
        {
            _gate.Release();
        }
    }

    // a crash between saving a draft and counting it must not make a second copy
    async Task<bool> MakeDraftAsync(int scheduleId, DateOnly date)
    {
        int latestId;
        await using (var db = await factory.CreateDbContextAsync())
        {
            if (await db.Invoices.AnyAsync(i => i.RecurringScheduleId == scheduleId && i.IssueDate == date)) return false;
            latestId = await db.Invoices.Where(i => i.RecurringScheduleId == scheduleId)
                .OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Id)
                .Select(i => i.Id).FirstOrDefaultAsync();
        }
        if (latestId == 0) return false;

        // copying the latest one carries forward any price changes made along the way
        var latest = (await invoices.GetAsync(latestId))!;
        var copy = await invoices.DuplicateAsync(latestId);
        copy.IssueDate = date;
        copy.DueDate = date.AddDays(latest.DueDate.DayNumber - latest.IssueDate.DayNumber);
        copy.RecurringScheduleId = scheduleId;
        await invoices.SaveAsync(copy);
        return true;
    }

    async Task UpdateAsync(int id, Action<RecurringSchedule> change)
    {
        await using var db = await factory.CreateDbContextAsync();
        var schedule = await db.Set<RecurringSchedule>().FindAsync(id) ?? throw new ValidationException("That repeating invoice no longer exists.");
        change(schedule);
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    static DateOnly? NextOf(RecurringSchedule s) => Recurrence.Next(s.StartDate, s.Every, s.OccurrencesCreated, s.EndDate);
}
