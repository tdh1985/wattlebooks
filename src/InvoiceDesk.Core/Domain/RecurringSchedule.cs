// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Domain;

// an invoice that comes round again, such as monthly hosting
public class RecurringSchedule
{
    public int Id { get; set; }
    public RepeatEvery Every { get; set; }
    // the first invoice's issue date, every later date is counted from it
    public DateOnly StartDate { get; set; }
    public int OccurrencesCreated { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsPaused { get; set; }
    public DateTime CreatedAt { get; set; }
}
