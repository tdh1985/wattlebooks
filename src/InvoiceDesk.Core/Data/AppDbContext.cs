// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BusinessProfile> Profiles => Set<BusinessProfile>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<RecurringSchedule> RecurringSchedules => Set<RecurringSchedule>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<BusinessProfile>(e =>
        {
            e.Property(p => p.Id).ValueGeneratedNever();
            e.HasOne(p => p.LogoAttachment).WithMany().HasForeignKey(p => p.LogoAttachmentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Client>(e => e.HasIndex(c => c.Name));

        b.Entity<Invoice>(e =>
        {
            e.HasIndex(i => i.Number).IsUnique();
            e.HasIndex(i => i.IssueDate);
            e.HasOne(i => i.Client).WithMany(c => c.Invoices).HasForeignKey(i => i.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(i => i.Attachments).WithOne().HasForeignKey(a => a.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(i => i.Reminders).WithOne().HasForeignKey(r => r.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RecurringSchedule>().WithMany().HasForeignKey(i => i.RecurringScheduleId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Invoice>().WithMany().HasForeignKey(i => i.ConvertedFromId).OnDelete(DeleteBehavior.SetNull);
            e.Ignore(i => i.PaidCents);
        });

        b.Entity<Transaction>(e =>
        {
            e.HasIndex(t => t.Date);
            e.HasOne(t => t.Invoice).WithMany(i => i.Payments).HasForeignKey(t => t.InvoiceId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.Category).WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(t => t.Attachments).WithOne().HasForeignKey(a => a.TransactionId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(t => t.ExTaxCents);
        });

        b.Entity<Attachment>(e =>
        {
            e.Ignore(a => a.IsImage);
        });
    }
}
