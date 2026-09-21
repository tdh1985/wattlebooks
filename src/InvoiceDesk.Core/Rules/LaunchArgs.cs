// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Rules;

// the taskbar jump list starts the app with one of these to open a page straight away
public static class LaunchArgs
{
    public const string NewInvoice = "--new-invoice";
    public const string AddExpense = "--add-expense";
    public const string AddIncome = "--add-income";

    public static string? RouteFor(IEnumerable<string> args) =>
        args.Select(a => a.Trim().ToLowerInvariant() switch
            {
                NewInvoice => "invoices/new",
                AddExpense => "money?new=out",
                AddIncome => "money?new=in",
                _ => null,
            })
            .FirstOrDefault(r => r is not null);
}
