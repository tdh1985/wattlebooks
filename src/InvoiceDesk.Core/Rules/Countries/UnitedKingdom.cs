// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace InvoiceDesk.Core.Rules;

public static partial class UnitedKingdom
{
    public static readonly CountryRules Rules = new()
    {
        Code = "GB",
        Name = "United Kingdom",
        Currency = "GBP",
        HtmlLang = "en-GB",
        ShortDate = "dd/MM/yyyy",
        LongDate = "d MMMM yyyy",
        DayMonth = "d MMM",
        MonthFirstDates = false,
        TaxName = "VAT",
        TaxWord = "VAT",
        StandardRatePpm = 200_000,
        ReducedRatePpm = 50_000,
        ZeroLabel = "Zero-rated",
        RegisteredLabel = "I'm registered for VAT",
        RegisteredByDefault = true,
        TaxedHeading = "VAT INVOICE",
        TaxedSubject = "VAT invoice",
        BusinessIntro = "Shown at the top of each invoice. VAT invoices must include your business name and VAT number.",
        TaxIntro = "When you're registered, new invoices start as VAT invoices with VAT added at the standard rate. Mark any line at 5%, 0% or exempt. You can still turn VAT off on any single invoice.",
        ReturnTitle = "VAT return",
        ReturnShortName = "VAT return",
        TaxNumber = new TaxNumberRule
        {
            Label = "VAT number",
            Placeholder = "GB 123 4567 89",
            EmptyHint = "Needed for VAT invoices.",
            BadHint = "This doesn't pass the HMRC VAT number check. Check each digit.",
            InvalidMessage = "VAT number must be 9 digits, or 12 for a branch, and pass the HMRC check.",
            MissingMessage = "Add your VAT number in Settings. VAT invoices must show it.",
            RequiredWhenTaxed = true,
            Normalise = NumberChecks.UkVatCompact,
            IsValid = NumberChecks.UkVat,
            Display = NumberChecks.UkVatDisplay,
        },
        BankCode = new BankCodeRule
        {
            Label = "Sort code",
            Placeholder = "00-00-00",
            InvalidMessage = "Sort code must be 6 digits.",
            Normalise = s => NumberChecks.Group(s, 2, 2, 2),
            IsValid = s => SortCode().IsMatch(s),
        },
        AccountPlaceholder = "8 digits",
        AccountInvalidMessage = "Account number must be 8 digits.",
        NormaliseAccount = NumberChecks.DigitsOnly,
        AccountIsValid = s => NumberChecks.DigitCount(s, 8, 8),
        TaxYearStartMonth = 4,
        TaxYearStartDay = 6,
        YearLabel = YearLabelStyle.TaxYear,
        DefaultPeriodMonths = 3,
        DefaultPeriodEndMonth = 3,
        ExpensesCarryTax = true,
        Return = ReturnKind.UkVat,
        TaxOffice = "HMRC",
        OverseasNote = "No UK VAT charged: the customer is outside the UK.",
        ReceiptTaxCapPpm = 200_000,
    };

    [GeneratedRegex(@"^\d{2}-\d{2}-\d{2}$")]
    private static partial Regex SortCode();
}
