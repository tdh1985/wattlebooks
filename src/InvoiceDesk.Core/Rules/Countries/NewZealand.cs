// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace InvoiceDesk.Core.Rules;

public static partial class NewZealand
{
    public static readonly CountryRules Rules = new()
    {
        Code = "NZ",
        Name = "New Zealand",
        Currency = "NZD",
        HtmlLang = "en-NZ",
        ShortDate = "dd/MM/yyyy",
        LongDate = "d MMMM yyyy",
        DayMonth = "d MMM",
        MonthFirstDates = false,
        TaxName = "GST",
        TaxWord = "GST",
        StandardRatePpm = 150_000,
        ZeroLabel = "Zero-rated",
        RegisteredLabel = "I'm registered for GST",
        RegisteredByDefault = true,
        TaxedHeading = "TAX INVOICE",
        TaxedSubject = "Tax invoice",
        BusinessIntro = "Shown at the top of each invoice. Tax invoices must include your business name and GST number.",
        TaxIntro = "When you're registered, new invoices start as tax invoices with GST added. You can still turn GST off on any single invoice.",
        ReturnTitle = "GST return",
        ReturnShortName = "GST return",
        TaxNumber = new TaxNumberRule
        {
            Label = "GST number",
            Placeholder = "123-456-789",
            EmptyHint = "Your IRD number. Needed for tax invoices.",
            BadHint = "This doesn't pass the IRD number check. Check each digit.",
            InvalidMessage = "GST number must be 8 or 9 digits and pass the IRD check.",
            MissingMessage = "Add your GST number in Settings. Tax invoices must show it.",
            RequiredWhenTaxed = true,
            Normalise = NumberChecks.DigitsOnly,
            IsValid = NumberChecks.Ird,
            Display = NumberChecks.IrdDisplay,
        },
        AccountPlaceholder = "12-3456-7890123-00",
        AccountInvalidMessage = "Account number must look like 12-3456-7890123-00.",
        NormaliseAccount = NormaliseAccount,
        AccountIsValid = s => Account().IsMatch(s),
        TaxYearStartMonth = 4,
        YearLabel = YearLabelStyle.TaxYear,
        DefaultPeriodMonths = 2,
        DefaultPeriodEndMonth = 3,
        ExpensesCarryTax = true,
        Return = ReturnKind.NzGst,
        TaxOffice = "IRD",
        OverseasNote = "Zero-rated: supplied to a client outside New Zealand.",
        ReceiptTaxCapPpm = 150_000,
    };

    // bank and branch then account and a two or three digit suffix
    static string NormaliseAccount(string s) => NumberChecks.Digits(s).Length switch
    {
        15 => NumberChecks.Group(s, 2, 4, 7, 2),
        16 => NumberChecks.Group(s, 2, 4, 7, 3),
        _ => s,
    };

    [GeneratedRegex(@"^\d{2}-\d{4}-\d{7}-\d{2,3}$")]
    private static partial Regex Account();
}
