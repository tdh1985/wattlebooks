// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using System.Text.RegularExpressions;

namespace InvoiceDesk.Core.Rules;

// currency is null when nothing on the receipt pins it down, like a bare $
public sealed record ReceiptGuess(string? Party, DateOnly? Date, long? TotalCents, long? TaxCents, string? Currency = null);

// one word the ocr found and the box it sits in on the page
public readonly record struct PageWord(string Text, double X, double Y, double Width, double Height);

// best guesses from ocr text, so every field stays blank unless it looks right
public static partial class ReceiptParser
{
    const int PartyLinesToCheck = 6;

    public static ReceiptGuess Parse(string text, DateOnly today, IReadOnlyList<string> knownParties, ReceiptRules? rules = null)
    {
        rules ??= Australia.Rules.Receipts;
        var raw = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (raw.Count == 0) return new ReceiptGuess(null, null, null, null);
        var lines = raw.Select(FixDigits).Select(l => SpacedThousands().Replace(l, ",")).ToList();

        var total = FindTotal(lines);
        return new ReceiptGuess(
            FindParty(text, lines, knownParties, rules.MonthFirst),
            FindDate(lines, today, rules.MonthFirst),
            total,
            FindTax(lines, total, rules.TaxCapPpm),
            FindCurrency(raw, lines));
    }

    // ocr reads columns one after another, so rows are rebuilt from where each word sits
    public static string JoinRows(IEnumerable<PageWord> words)
    {
        var list = words.Where(w => w.Text.Trim().Length > 0).ToList();
        if (list.Count == 0) return "";
        var heights = list.Select(w => w.Height).Order().ToList();
        var tolerance = heights[heights.Count / 2] / 2;

        var rows = new List<List<PageWord>>();
        foreach (var word in list.OrderBy(Middle))
        {
            var row = rows.LastOrDefault();
            if (row is not null && Math.Abs(Middle(word) - row.Average(Middle)) <= tolerance) row.Add(word);
            else rows.Add([word]);
        }
        return string.Join("\n", rows.Select(r => string.Join(" ", r.OrderBy(w => w.X).Select(w => w.Text.Trim()))));

        static double Middle(PageWord w) => w.Y + w.Height / 2;
    }

    // ocr often reads a zero as o or e and a one as l, but only inside numbers
    static string FixDigits(string line) => string.Join(' ', line.Split(' ').Select(token =>
    {
        if (token.Count(char.IsAsciiDigit) < 2) return token;
        var rest = token.Where(c => !char.IsAsciiDigit(c) && !"$£€.,/:-".Contains(c)).ToList();
        if (rest.Count == 0 || rest.Count > 2 || rest.Any(c => !"OoDeIl|SB".Contains(c))) return token;
        return new string(token.Select(c => c switch
        {
            'O' or 'o' or 'D' or 'e' => '0',
            'I' or 'l' or '|' => '1',
            'S' => '5',
            'B' => '8',
            _ => c,
        }).ToArray());
    }));

    static long? FindTotal(List<string> lines)
    {
        var candidates = new List<long>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (!IsTotalLine(lines[i])) continue;
            // some receipts put the figure on the line below its label
            var amount = LastAmount(lines[i]) ?? (i + 1 < lines.Count && AmountOnly().IsMatch(lines[i + 1]) ? LastAmount(lines[i + 1]) : null);
            if (amount is { } a) candidates.Add(a);
        }
        if (candidates.Count > 0) return candidates.Max();

        var all = lines.Where(l => !IsTaxLine(l)).Select(LastAmount).OfType<long>().ToList();
        return all.Count > 0 ? all.Max() : null;
    }

    // tax inside a price can't beat the top rate, so bigger is something else
    static long? FindTax(List<string> lines, long? total, int capPpm)
    {
        if (total is not { } t) return null;
        return lines.Where(IsTaxLine).Select(LastAmount).OfType<long>()
            .Where(g => g > 0 && g <= MoneyMath.TaxFromInclusive(t, capPpm) + 1)
            .Cast<long?>()
            .FirstOrDefault();
    }

    // marks that name one currency, a bare $ could be any of them
    static readonly (Regex Mark, string Code)[] CurrencyMarks =
    [
        .. Currencies.All.Select(c => (new Regex($"(?<![A-Za-z]){c.Code}(?![A-Za-z])"), c.Code)),
        (new Regex(@"(?<![A-Za-z])US\$"), "USD"),
        (new Regex(@"(?<![A-Za-z])NZ\$"), "NZD"),
        (new Regex(@"(?<![A-Za-z])CA?\$"), "CAD"),
        (new Regex(@"(?<![A-Za-z])HK\$"), "HKD"),
        (new Regex(@"(?<![A-Za-z])S\$"), "SGD"),
        (new Regex(@"(?<![A-Za-z])A\$"), "AUD"),
        (new Regex("€"), "EUR"),
        (new Regex("£"), "GBP"),
    ];

    static string? FindCurrency(List<string> raw, List<string> lines)
    {
        var marked = Enumerable.Range(0, lines.Count)
            .Select(i => (Code: CurrencyOn(raw[i], lines[i]), IsTotal: IsTotalLine(lines[i])))
            .Where(x => x.Code is not null)
            .ToList();
        var onTotals = marked.Where(x => x.IsTotal).Select(x => x.Code).Distinct().ToList();
        if (onTotals.Count == 1) return onTotals[0];

        var counts = marked.GroupBy(x => x.Code).Select(g => (Code: g.Key, Count: g.Count())).OrderByDescending(x => x.Count).ToList();
        if (counts.Count == 0) return null;
        return counts.Count == 1 || counts[0].Count > counts[1].Count ? counts[0].Code : null;
    }

    // a mark only counts beside a figure, "currency: usd" alone is ignored
    static string? CurrencyOn(string raw, string line)
    {
        if (LastAmount(line) is null) return null;
        var codes = CurrencyMarks.Where(m => m.Mark.IsMatch(raw)).Select(m => m.Code).Distinct().ToList();
        return codes.Count == 1 ? codes[0] : null;
    }

    static DateOnly? FindDate(List<string> lines, DateOnly today, bool monthFirst)
    {
        bool Recent(DateOnly d) => d <= today && d >= today.AddYears(-2);
        // a remittance shows the invoice date too, so the paid date wins
        return lines.Where(l => PaidDateLabel().IsMatch(l)).SelectMany(l => DatesIn(l, monthFirst)).Where(Recent).Cast<DateOnly?>().FirstOrDefault()
               ?? lines.SelectMany(l => DatesIn(l, monthFirst)).Where(Recent).Cast<DateOnly?>().FirstOrDefault();
    }

    static IEnumerable<DateOnly> DatesIn(string line, bool monthFirst)
    {
        foreach (Match m in NumericDate().Matches(line))
        {
            var year = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (year < 100) year += 2000;
            // most receipts put the day first, us ones put the month first
            var first = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var second = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var (month, day) = monthFirst ? (first, second) : (second, first);
            if (Make(year, month, day) is { } d)
                yield return d;
        }
        foreach (Match m in NamedDate().Matches(line))
        {
            var month = MonthNumber(m.Groups[2].Value);
            if (month > 0 && Make(int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), month, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) is { } d)
                yield return d;
        }
        foreach (Match m in IsoDate().Matches(line))
        {
            if (Make(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture)) is { } d)
                yield return d;
        }
    }

    static DateOnly? Make(int year, int month, int day) =>
        month is >= 1 and <= 12 && year is >= 2000 and <= 2100 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;

    static int MonthNumber(string name) => name[..3].ToLowerInvariant() switch
    {
        "jan" => 1, "feb" => 2, "mar" => 3, "apr" => 4, "may" => 5, "jun" => 6,
        "jul" => 7, "aug" => 8, "sep" => 9, "oct" => 10, "nov" => 11, "dec" => 12,
        _ => 0,
    };

    static string? FindParty(string text, List<string> lines, IReadOnlyList<string> knownParties, bool monthFirst)
    {
        // a supplier seen before wins, spelled the way it was saved
        var known = knownParties
            .Select(p => p.Trim())
            .Where(p => p.Length >= 3)
            .Where(p => Regex.IsMatch(text, $@"(?<![\p{{L}}]){Regex.Escape(p)}(?![\p{{L}}])", RegexOptions.IgnoreCase))
            .OrderByDescending(p => p.Length)
            .FirstOrDefault();
        if (known is not null) return known;

        var first = lines.Take(PartyLinesToCheck).FirstOrDefault(l => LooksLikeName(l, monthFirst));
        if (first is null) return null;
        return first.Any(char.IsLower) ? first : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(first.ToLowerInvariant());
    }

    static bool LooksLikeName(string line, bool monthFirst) =>
        line.Count(char.IsLetter) >= 3
        && !char.IsDigit(line[0])
        && !NotAName().IsMatch(line)
        && !Postcode().IsMatch(line)
        && LastAmount(line) is null
        && !DatesIn(line, monthFirst).Any();

    static bool IsTaxLine(string line) => TaxWord().IsMatch(line) && !TotalIncludingTax().IsMatch(line);

    static bool IsTotalLine(string line) =>
        TotalWord().IsMatch(line) && !SubTotal().IsMatch(line) && !IsTaxLine(line);

    // the right-most figure is the price on a receipt line
    static long? LastAmount(string line)
    {
        var cleaned = TimeOfDay().Replace(NumericDate().Replace(IsoDate().Replace(line, " "), " "), " ");
        var matches = Amount().Matches(cleaned);
        if (matches.Count == 0) return null;
        var m = matches[^1];
        var dollars = long.Parse(m.Groups[1].Value.Replace(",", ""), CultureInfo.InvariantCulture);
        return dollars * 100 + int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"(?<![\d.,])(\d{1,3}(?:,\d{3})+|\d+)\s*[.,]\s*(\d{2})(?![\d])")]
    private static partial Regex Amount();

    // ocr reads $1,100.00 as $1 100.00, the sign keeps a quantity from joining a price
    [GeneratedRegex(@"(?<=[$£€]\s*\d{1,3}(?:\s\d{3})*)\s(?=\d{3}(?:\s\d{3})*\s*[.,]\s*\d{2}(?!\d))")]
    private static partial Regex SpacedThousands();

    [GeneratedRegex(@"^[$£€]?\s*[\d,]+\s*[.,]\s*\d{2}$")]
    private static partial Regex AmountOnly();

    [GeneratedRegex(@"\b(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{4}|\d{2})\b")]
    private static partial Regex NumericDate();

    [GeneratedRegex(@"\b(\d{1,2})(?:st|nd|rd|th)?\s+(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\.?,?\s+(\d{4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex NamedDate();

    [GeneratedRegex(@"\b(\d{4})-(\d{2})-(\d{2})\b")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"\b\d{1,2}:\d{2}(?::\d{2})?\b")]
    private static partial Regex TimeOfDay();

    [GeneratedRegex(@"\b(total|amount due|balance due|amount paid|eftpos|paid|visa|mastercard|amex|debit|credit)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TotalWord();

    [GeneratedRegex(@"\bsub\s*-?\s*total", RegexOptions.IgnoreCase)]
    private static partial Regex SubTotal();

    [GeneratedRegex(@"\b(gst|vat|hst|tax|sales\s+tax)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TaxWord();

    // "total inc vat" is the total, "total includes vat" is the tax
    [GeneratedRegex(@"\b(inc|incl|including)\.?\s*(gst|vat|hst|tax)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TotalIncludingTax();

    [GeneratedRegex(@"^(tax\s+)?(invoice|receipt)\b|\babn\b|www\.|https?:|@|\b(ph|phone|tel|date|welcome|thank)\b|\bvat\s*(no|reg)|\bgst\s*(no|reg|#)", RegexOptions.IgnoreCase)]
    private static partial Regex NotAName();

    [GeneratedRegex(@"\b(NSW|VIC|QLD|TAS|SA|WA|NT|ACT)\b\s*\d{4}\b|\b[A-Z]{1,2}\d[A-Z\d]?\s*\d[A-Z]{2}\b|\b[A-Z]\d[A-Z]\s?\d[A-Z]\d\b|\b[A-Z]{2}\s+\d{5}(?:-\d{4})?\b", RegexOptions.IgnoreCase)]
    private static partial Regex Postcode();

    [GeneratedRegex(@"\b(payment\s+date|date\s+paid|paid\s+on)\b|\bpaid\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex PaidDateLabel();
}
