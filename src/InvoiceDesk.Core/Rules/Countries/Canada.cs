// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace InvoiceDesk.Core.Rules;

public static partial class Canada
{
    // nova scotia's hst dropped to 14% on 1 april 2025
    public static readonly IReadOnlyList<Province> Provinces =
    [
        new("AB", "Alberta", 50_000, "GST"),
        new("BC", "British Columbia", 50_000, "GST"),
        new("MB", "Manitoba", 50_000, "GST"),
        new("NB", "New Brunswick", 150_000, "HST"),
        new("NL", "Newfoundland and Labrador", 150_000, "HST"),
        new("NS", "Nova Scotia", 140_000, "HST"),
        new("NT", "Northwest Territories", 50_000, "GST"),
        new("NU", "Nunavut", 50_000, "GST"),
        new("ON", "Ontario", 130_000, "HST"),
        new("PE", "Prince Edward Island", 150_000, "HST"),
        new("QC", "Quebec", 50_000, "GST"),
        new("SK", "Saskatchewan", 50_000, "GST"),
        new("YT", "Yukon", 50_000, "GST"),
    ];

    public static readonly CountryRules Rules = new()
    {
        Code = "CA",
        Name = "Canada",
        Currency = "CAD",
        HtmlLang = "en-CA",
        ShortDate = "yyyy-MM-dd",
        LongDate = "MMMM d, yyyy",
        DayMonth = "MMM d",
        MonthFirstDates = false,
        TaxName = "GST/HST",
        TaxWord = "GST/HST",
        StandardRatePpm = 50_000,
        ZeroLabel = "Zero-rated",
        RegisteredLabel = "I'm registered for GST/HST",
        RegisteredByDefault = true,
        TaxedHeading = "INVOICE",
        TaxedSubject = "Invoice",
        BusinessIntro = "Shown at the top of each invoice. Invoices that charge GST/HST must include your business name and GST/HST number.",
        TaxIntro = "When you're registered, new invoices start with GST/HST added at your province's rate, or your client's rate when they're in another province. You can still turn it off on any single invoice.",
        ReturnTitle = "GST/HST return",
        ReturnShortName = "GST/HST return",
        TaxNumber = new TaxNumberRule
        {
            Label = "GST/HST number",
            Placeholder = "123456789 RT0001",
            EmptyHint = "Your business number with RT0001. Needed when you charge GST/HST.",
            BadHint = "This doesn't pass the business number check. Check each digit.",
            InvalidMessage = "GST/HST number must be your 9-digit business number followed by RT and 4 digits, like 123456789 RT0001.",
            MissingMessage = "Add your GST/HST number in Settings. Invoices that charge GST/HST must show it.",
            RequiredWhenTaxed = true,
            Normalise = NumberChecks.CanadaGstCompact,
            IsValid = NumberChecks.CanadaGst,
            Display = NumberChecks.CanadaGstDisplay,
        },
        BankCode = new BankCodeRule
        {
            Label = "Transit and institution",
            Placeholder = "12345-003",
            InvalidMessage = "Transit and institution numbers must be 5 and 3 digits, like 12345-003.",
            Normalise = s => NumberChecks.Group(s, 5, 3),
            IsValid = s => Transit().IsMatch(s),
        },
        AccountPlaceholder = "7 to 12 digits",
        AccountInvalidMessage = "Account number must be 7 to 12 digits.",
        NormaliseAccount = NumberChecks.DigitsOnly,
        AccountIsValid = s => NumberChecks.DigitCount(s, 7, 12),
        TaxYearStartMonth = 1,
        YearLabel = YearLabelStyle.Calendar,
        DefaultPeriodMonths = 12,
        DefaultPeriodEndMonth = 12,
        ExpensesCarryTax = true,
        Return = ReturnKind.CaGstHst,
        TaxOffice = "the CRA",
        OverseasNote = "Zero-rated export: no GST/HST charged.",
        ReceiptTaxCapPpm = 150_000,
        Provinces = Provinces,
        DefaultRegion = "ON",
    };

    [GeneratedRegex(@"^\d{5}-\d{3}$")]
    private static partial Regex Transit();
}
