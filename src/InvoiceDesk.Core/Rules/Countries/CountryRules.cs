// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;

namespace InvoiceDesk.Core.Rules;

public enum ReturnKind { Bas, NzGst, UkVat, CaGstHst, UsSalesTax }

public enum YearLabelStyle { FinancialYear, TaxYear, Calendar }

public sealed record TaxNumberRule
{
    public required string Label { get; init; }
    public required string Placeholder { get; init; }
    // shown under the empty field in settings
    public required string EmptyHint { get; init; }
    // shown under the field in settings when the check fails
    public required string BadHint { get; init; }
    // the save error, which the client form prefixes with "Client "
    public required string InvalidMessage { get; init; }
    public required string MissingMessage { get; init; }
    public required bool RequiredWhenTaxed { get; init; }
    public required Func<string, string> Normalise { get; init; }
    public required Func<string, bool> IsValid { get; init; }
    public required Func<string, string> Display { get; init; }
}

// a bank field like a bsb or sort code, which some countries don't have
public sealed record BankCodeRule
{
    public required string Label { get; init; }
    public required string Placeholder { get; init; }
    public required string InvalidMessage { get; init; }
    public required Func<string, string> Normalise { get; init; }
    public required Func<string, bool> IsValid { get; init; }
}

public sealed record Province(string Code, string Name, int RatePpm, string TaxName);

public sealed record ReceiptRules(bool MonthFirst, int TaxCapPpm);

public sealed record CountryRules
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Currency { get; init; }
    public required string HtmlLang { get; init; }
    public required string ShortDate { get; init; }
    public required string LongDate { get; init; }
    public required string DayMonth { get; init; }
    public required bool MonthFirstDates { get; init; }
    public required string TaxName { get; init; }
    public required string TaxWord { get; init; }
    public required int StandardRatePpm { get; init; }
    public int ReducedRatePpm { get; init; }
    public required string ZeroLabel { get; init; }
    public string ExemptLabel { get; init; } = "Exempt";
    public required string RegisteredLabel { get; init; }
    public required bool RegisteredByDefault { get; init; }
    public required string TaxedHeading { get; init; }
    public required string TaxedSubject { get; init; }
    // the first line of the business section in settings
    public required string BusinessIntro { get; init; }
    // the first line of the tax section in settings
    public required string TaxIntro { get; init; }
    public required string ReturnTitle { get; init; }
    public required string ReturnShortName { get; init; }
    public required TaxNumberRule TaxNumber { get; init; }
    public BankCodeRule? BankCode { get; init; }
    public required string AccountPlaceholder { get; init; }
    public required string AccountInvalidMessage { get; init; }
    public required Func<string, string> NormaliseAccount { get; init; }
    public required Func<string, bool> AccountIsValid { get; init; }
    public required int TaxYearStartMonth { get; init; }
    public int TaxYearStartDay { get; init; } = 1;
    public required YearLabelStyle YearLabel { get; init; }
    public required int DefaultPeriodMonths { get; init; }
    public required int DefaultPeriodEndMonth { get; init; }
    public required bool ExpensesCarryTax { get; init; }
    public required ReturnKind Return { get; init; }
    public required string TaxOffice { get; init; }
    public required string OverseasNote { get; init; }
    public required int ReceiptTaxCapPpm { get; init; }
    public IReadOnlyList<Province> Provinces { get; init; } = [];
    public string DefaultRegion { get; init; } = "";

    public bool HasReducedRate => ReducedRatePpm > 0;

    public int HighestRatePpm => Provinces.Count > 0 ? Provinces.Max(p => p.RatePpm) : Math.Max(StandardRatePpm, ReducedRatePpm);

    public ReceiptRules Receipts => new(MonthFirstDates, ReceiptTaxCapPpm);

    public Province? FindProvince(string? code) =>
        Provinces.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));

    // what a business in this country starts with, before the owner changes anything
    public void ApplyDefaults(BusinessProfile p)
    {
        p.Country = Code;
        p.TaxRegistered = RegisteredByDefault;
        p.Region = DefaultRegion;
        p.TaxRatePpm = FindProvince(DefaultRegion)?.RatePpm ?? StandardRatePpm;
        p.ReducedRatePpm = ReducedRatePpm;
        p.TaxPeriodMonths = DefaultPeriodMonths;
        p.TaxPeriodEndMonth = DefaultPeriodEndMonth;
        p.OverseasNote = OverseasNote;
    }
}
