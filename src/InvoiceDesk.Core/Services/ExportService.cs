// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Text;
using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed class ExportService(IDbContextFactory<AppDbContext> factory, TimeProvider clock)
{
    public async Task WriteTransactionsCsvAsync(DateRange range, Stream output)
    {
        await using var db = await factory.CreateDbContextAsync();
        var txs = (await db.Transactions.AsNoTracking().AsSplitQuery()
                .Include(t => t.Category).Include(t => t.Invoice).Include(t => t.Attachments)
                .ToListAsync())
            .Where(t => range.Contains(t.Date))
            .OrderBy(t => t.Date).ThenBy(t => t.Id);

        await WriteAsync(output,
            Csv.Line("Date", "Type", "Party", "Description", "Category", "Amount inc GST", "GST", "Amount ex GST",
                "Invoice", "Method", "Receipt attached"),
            txs.Select(t => Csv.Line(
                Csv.Date(t.Date), Labels.Direction(t.Direction), Csv.Text(t.Party), Csv.Text(t.Description),
                Csv.Text(t.Category?.Name), Csv.Money(t.AmountCents), Csv.Money(t.GstCents), Csv.Money(t.ExGstCents),
                Csv.Text(t.Invoice?.Number), Labels.Method(t.Method), t.Attachments.Count > 0 ? "Yes" : "No")));
    }

    // drafts and quotes aren't real invoices, so an accountant never needs them
    public async Task WriteInvoicesCsvAsync(DateRange range, Stream output)
    {
        await using var db = await factory.CreateDbContextAsync();
        var today = clock.Today();
        var invoices = (await db.Invoices.AsNoTracking().AsSplitQuery()
                .Include(i => i.Client).Include(i => i.Lines).Include(i => i.Payments)
                .ToListAsync())
            .Where(i => i.Kind == InvoiceKind.Invoice && i.Status != InvoiceStatus.Draft && range.Contains(i.IssueDate))
            .OrderBy(i => i.IssueDate).ThenBy(i => i.Id);

        await WriteAsync(output,
            Csv.Line("Number", "Client", "Issued", "Due", "Status", "Subtotal ex GST", "GST", "Total", "Paid", "Balance"),
            invoices.Select(i =>
            {
                var s = InvoiceSummary.From(i, today);
                return Csv.Line(
                    Csv.Text(s.Number), Csv.Text(s.ClientName), Csv.Date(s.IssueDate), Csv.Date(s.DueDate),
                    Labels.Status(s.Status), Csv.Money(s.TotalCents - s.GstCents), Csv.Money(s.GstCents),
                    Csv.Money(s.TotalCents), Csv.Money(s.PaidCents),
                    Csv.Money(s.Status == DisplayStatus.Void ? 0 : s.BalanceCents));
            }));
    }

    public static Task WriteBasCsvAsync(BasSummary bas, Stream output) =>
        WriteAsync(output, Csv.Line("Label", "What it is", "Amount"),
        [
            Csv.Line("G1", "Total sales including GST", Csv.Money(bas.TotalSalesCents)),
            Csv.Line("1A", "GST on sales", Csv.Money(bas.GstOnSalesCents)),
            Csv.Line("1B", "GST on purchases", Csv.Money(bas.GstOnPurchasesCents)),
            Csv.Line("", bas.NetGstCents >= 0 ? "GST to pay" : "GST refund", Csv.Money(Math.Abs(bas.NetGstCents))),
        ]);

    public static Task WriteProfitAndLossCsvAsync(ProfitAndLoss pl, Stream output) =>
        WriteAsync(output, Csv.Line("Section", "Category", "Amount ex GST"),
            pl.Income.Select(c => Csv.Line("Income", Csv.Text(c.Category), Csv.Money(c.ExGstCents)))
                .Append(Csv.Line("Income", "Total income", Csv.Money(pl.IncomeCents)))
                .Concat(pl.Expenses.Select(c => Csv.Line("Expenses", Csv.Text(c.Category), Csv.Money(c.ExGstCents))))
                .Append(Csv.Line("Expenses", "Total expenses", Csv.Money(pl.ExpensesCents)))
                .Append(Csv.Line("Profit", "Net profit", Csv.Money(pl.ProfitCents))));

    static async Task WriteAsync(Stream output, string header, IEnumerable<string> rows)
    {
        // the bom is what makes excel read the file as utf-8
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true)
        {
            NewLine = "\r\n",
        };
        await writer.WriteLineAsync(header);
        foreach (var row in rows) await writer.WriteLineAsync(row);
    }
}
