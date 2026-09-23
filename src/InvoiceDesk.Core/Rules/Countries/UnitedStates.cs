// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Rules;

public static class UnitedStates
{
    public static readonly CountryRules Rules = new()
    {
        Code = "US",
        Name = "United States",
        Currency = "USD",
        HtmlLang = "en-US",
        ShortDate = "MM/dd/yyyy",
        LongDate = "MMMM d, yyyy",
        DayMonth = "MMM d",
        MonthFirstDates = true,
        TaxName = "Sales tax",
        TaxWord = "sales tax",
        // no national rate, so owner types combined state and local one
        StandardRatePpm = 0,
        ZeroLabel = "Non-taxable",
        RegisteredLabel = "I collect sales tax",
        RegisteredByDefault = false,
        TaxedHeading = "INVOICE",
        TaxedSubject = "Invoice",
        BusinessIntro = "Shown at the top of each invoice.",
        TaxIntro = "When you collect sales tax, new invoices start with it added at your rate. You can still turn it off on any single invoice, such as for a client in another state.",
        ReturnTitle = "Sales tax summary",
        ReturnShortName = "sales tax",
        TaxNumber = new TaxNumberRule
        {
            Label = "EIN",
            Placeholder = "12-3456789",
            EmptyHint = "Optional. Printed on your invoices when it's filled in.",
            BadHint = "An EIN is 9 digits, like 12-3456789.",
            InvalidMessage = "EIN must be 9 digits, like 12-3456789.",
            MissingMessage = "",
            RequiredWhenTaxed = false,
            Normalise = NumberChecks.DigitsOnly,
            IsValid = NumberChecks.Ein,
            Display = NumberChecks.EinDisplay,
        },
        BankCode = new BankCodeRule
        {
            Label = "Routing number",
            Placeholder = "9 digits",
            InvalidMessage = "Routing number must be 9 digits and pass the bank check.",
            Normalise = NumberChecks.DigitsOnly,
            IsValid = NumberChecks.AbaRouting,
        },
        AccountPlaceholder = "4 to 17 digits",
        AccountInvalidMessage = "Account number must be 4 to 17 digits.",
        NormaliseAccount = NumberChecks.DigitsOnly,
        AccountIsValid = s => NumberChecks.DigitCount(s, 4, 17),
        TaxYearStartMonth = 1,
        YearLabel = YearLabelStyle.Calendar,
        DefaultPeriodMonths = 3,
        DefaultPeriodEndMonth = 3,
        // sales tax paid on purchases isn't claimed back, so it's part of the cost
        ExpensesCarryTax = false,
        Return = ReturnKind.UsSalesTax,
        TaxOffice = "the IRS or your state",
        OverseasNote = "No sales tax charged.",
        ReceiptTaxCapPpm = 120_000,
        Paper = PaperSize.Letter,
    };
}
