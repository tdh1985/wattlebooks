// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Domain;

// one row per chase-up email, so the history shows each one and the next gets firmer
public class Reminder
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public ReminderTone Tone { get; set; }
    public DateTime CreatedAt { get; set; }
}
