// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace InvoiceDesk.Core.Rules;

public static partial class Australia
{
    public static readonly CountryRules Rules = new()
    {
        Code = "AU",
        Name = "Australia",
        Currency = "AUD",
        HtmlLang = "en-AU",
        ShortDate = "dd/MM/yyyy",
        LongDate = "d MMMM yyyy",
        DayMonth = "d MMM",
        MonthFirstDates = false,
        TaxName = "GST",
        TaxWord = "GST",
        StandardRatePpm = 100_000,
        ZeroLabel = "GST-free",
        RegisteredLabel = "I'm registered for GST",
        RegisteredByDefault = true,
        TaxedHeading = "TAX INVOICE",
        TaxedSubject = "Tax invoice",
        BusinessIntro = "Shown at the top of each invoice. Tax invoices must include your business name and ABN.",
        TaxIntro = "When you're registered, new invoices start as tax invoices with GST added. You can still turn GST off on any single invoice.",
        ReturnTitle = "BAS worksheet",
        ReturnShortName = "BAS",
        TaxNumber = new TaxNumberRule
        {
            Label = "ABN",
            Placeholder = "11 digits",
            EmptyHint = "Needed for tax invoices.",
            BadHint = "This doesn't pass the ATO's ABN check. Check each digit.",
            InvalidMessage = "ABN must be 11 digits and pass the ATO check.",
            MissingMessage = "Add your ABN in Settings. Tax invoices must show it.",
            RequiredWhenTaxed = true,
            Normalise = s => s.Replace(" ", ""),
            IsValid = Abn.IsValid,
            Display = Abn.Format,
        },
        BankCode = new BankCodeRule
        {
            Label = "BSB",
            Placeholder = "000-000",
            InvalidMessage = "BSB must be 6 digits.",
            Normalise = NormaliseBsb,
            IsValid = s => Bsb().IsMatch(s),
        },
        AccountPlaceholder = "",
        AccountInvalidMessage = "",
        NormaliseAccount = s => s,
        AccountIsValid = _ => true,
        TaxYearStartMonth = 7,
        YearLabel = YearLabelStyle.FinancialYear,
        DefaultPeriodMonths = 3,
        DefaultPeriodEndMonth = 3,
        ExpensesCarryTax = true,
        Return = ReturnKind.Bas,
        TaxOffice = "ATO",
        OverseasNote = "No GST has been charged, as this is a supply to a client outside Australia.",
        ReceiptTaxCapPpm = 100_000,
    };

    static string NormaliseBsb(string s)
    {
        var digits = new string(s.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 6 ? $"{digits[..3]}-{digits[3..]}" : s;
    }

    [GeneratedRegex(@"^\d{3}-\d{3}$")]
    private static partial Regex Bsb();
}
