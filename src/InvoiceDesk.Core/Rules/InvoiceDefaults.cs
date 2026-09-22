// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;

namespace InvoiceDesk.Core.Rules;

// one place decides what a draft starts with, so drafts and editor agree
public static class InvoiceDefaults
{
    public static string CountryOf(Client client, BusinessProfile profile) =>
        client.Country.Length > 0 ? client.Country : profile.Country;

    public static bool IsOverseas(Client? client, BusinessProfile profile) =>
        client is { Country.Length: > 0 } && !string.Equals(client.Country, profile.Country, StringComparison.OrdinalIgnoreCase);

    // canada charges by where the client is, everyone else has one rate
    public static int StandardRateFor(Client? client, BusinessProfile profile)
    {
        var rules = Countries.For(profile.Country);
        if (rules.Provinces.Count == 0) return profile.TaxRatePpm;
        var region = client is { Region.Length: > 0 } && !IsOverseas(client, profile) ? client.Region : profile.Region;
        return rules.FindProvince(region)?.RatePpm ?? profile.TaxRatePpm;
    }

    public static void Apply(Invoice draft, Client? client, BusinessProfile profile)
    {
        draft.Currency = client is { Currency.Length: > 0 } ? client.Currency : Countries.For(profile.Country).Currency;
        draft.TaxEnabled = profile.TaxRegistered && !IsOverseas(client, profile);
        draft.TaxRatePpm = StandardRateFor(client, profile);
        draft.ReducedRatePpm = profile.ReducedRatePpm;
    }

    public static TaxNumberRule? TaxNumberRuleFor(Client client, BusinessProfile profile)
    {
        var code = CountryOf(client, profile);
        return Countries.IsSupported(code) ? Countries.For(code).TaxNumber : null;
    }
}
