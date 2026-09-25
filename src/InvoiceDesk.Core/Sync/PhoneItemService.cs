// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using System.Text.Json.Nodes;
using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Sync;

public sealed record PhoneTrayItem(int Id, PhoneItemKind Kind, string StoredPath, string Note, DateTime TakenAt, string Problem);

// turns what the phone sent into local rows, the cloud side lives in InboxPuller
public sealed class PhoneItemService(
    IDbContextFactory<AppDbContext> factory, AppPaths paths, AttachmentStore store, TimeProvider clock,
    InvoiceService invoices, ClientService clients)
{
    const int MaxLines = 100;

    public event Action? Changed;

    public async Task<bool> HasAsync(string inboxId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.PhoneItems.AnyAsync(p => p.InboxId == inboxId);
    }

    public async Task ImportReceiptAsync(string inboxId, byte[] photo, JsonNode? payload)
    {
        var relative = $"phone/{inboxId}.jpg";
        var full = paths.FullPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, photo);

        await using var db = await factory.CreateDbContextAsync();
        db.PhoneItems.Add(new PhoneItem
        {
            InboxId = inboxId, Kind = PhoneItemKind.Receipt, StoredPath = relative,
            Note = Text.Clean(Read(payload, "note")), TakenAt = ReadTime(payload) ?? Now(), ReceivedAt = Now(),
        });
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    // a crash between the two saves can leave a spare draft, which is harmless to delete
    public async Task ImportDraftAsync(string inboxId, JsonNode? payload)
    {
        int? invoiceId = null;
        var problem = "";
        try
        {
            var draft = await BuildDraftAsync(payload);
            invoiceId = (await invoices.SaveAsync(draft)).Id;
        }
        catch (ValidationException ex)
        {
            problem = ex.Message;
        }

        await using var db = await factory.CreateDbContextAsync();
        db.PhoneItems.Add(new PhoneItem
        {
            InboxId = inboxId, Kind = PhoneItemKind.DraftInvoice, InvoiceId = invoiceId, Problem = problem,
            TakenAt = Now(), ReceivedAt = Now(), DoneAt = invoiceId is null ? null : Now(),
        });
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task RecordProblemAsync(string inboxId, string problem)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.PhoneItems.Add(new PhoneItem
        {
            InboxId = inboxId, Kind = PhoneItemKind.Receipt, Problem = problem, TakenAt = Now(), ReceivedAt = Now(),
        });
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task<List<PhoneTrayItem>> ListTrayAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.PhoneItems.AsNoTracking()
            .Where(p => p.DoneAt == null)
            .OrderBy(p => p.TakenAt)
            .Select(p => new PhoneTrayItem(p.Id, p.Kind, p.StoredPath, p.Note, p.TakenAt, p.Problem))
            .ToListAsync();
    }

    public async Task<int> CountTrayAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.PhoneItems.CountAsync(p => p.DoneAt == null);
    }

    // the drawer takes staged files, so the photo is copied in like a dropped one
    public async Task<StagedFile> StageAsync(int id)
    {
        var item = await FindAsync(id);
        var full = paths.FullPath(item.StoredPath);
        if (!File.Exists(full)) throw new ValidationException("That photo is missing from the data folder.");
        await using var stream = File.OpenRead(full);
        var name = $"Phone receipt {item.TakenAt.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}.jpg";
        return await store.StageAsync(stream, name, stream.Length);
    }

    // rows stay so a pull that lost its delete still knows this one arrived
    public async Task FinishAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var item = await db.PhoneItems.SingleOrDefaultAsync(p => p.Id == id);
        if (item is null || item.DoneAt is not null) return;
        item.DoneAt = Now();
        if (item.StoredPath.Length > 0)
        {
            try { File.Delete(paths.FullPath(item.StoredPath)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            item.StoredPath = "";
        }
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    async Task<PhoneItem> FindAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.PhoneItems.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id)
               ?? throw new ValidationException("That phone item is gone.");
    }

    async Task<Invoice> BuildDraftAsync(JsonNode? payload)
    {
        if (payload?["lines"] is not JsonArray rawLines || rawLines.Count == 0)
            throw new ValidationException("A quick invoice from your phone had no lines.");
        if (rawLines.Count > MaxLines) throw new ValidationException($"A quick invoice from your phone had more than {MaxLines} lines.");

        var clientId = await ResolveClientAsync(payload["client"]);
        var draft = await invoices.NewDraftAsync(clientId);
        var taxCode = draft.Lines.FirstOrDefault()?.TaxCode ?? default;
        draft.Lines = rawLines.Select((line, i) => ReadLine(line, i, taxCode)).ToList();
        var notes = Text.Clean(Read(payload, "notes"));
        if (notes.Length > 0) draft.Notes = draft.Notes.Length > 0 ? notes + "\n\n" + draft.Notes : notes;
        return draft;
    }

    async Task<int> ResolveClientAsync(JsonNode? client)
    {
        if (client?["local_id"] is JsonValue idNode && idNode.TryGetValue<int>(out var localId)
            && await clients.GetAsync(localId) is { } existing)
            return existing.Id;

        var name = Text.Clean(Read(client, "name"));
        if (name.Length == 0) name = "Client from phone";
        var match = (await clients.ListAsync(name, includeArchived: true))
            .Select(c => c.Client).FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match.Id;
        return (await clients.SaveAsync(new Client { Name = name, Email = Text.Clean(Read(client, "email")) })).Id;
    }

    static InvoiceLine ReadLine(JsonNode? line, int index, TaxCode taxCode)
    {
        var description = Text.Clean(Read(line, "description"));
        var quantity = line?["quantity"] is JsonValue q && q.TryGetValue<decimal>(out var qty) ? qty : 0m;
        // a negative price is a discount line, which the editor allows too
        long? unit = line?["unit_cents"] is JsonValue u && u.TryGetValue<long>(out var cents) ? cents : null;
        if (description.Length == 0 || quantity <= 0 || unit is null)
            throw new ValidationException($"Line {index + 1} of a quick invoice from your phone wasn't complete.");
        return new InvoiceLine { SortOrder = index, Description = description, Quantity = quantity, UnitPriceCents = unit.Value, TaxCode = taxCode };
    }

    static string Read(JsonNode? node, string key) =>
        node is JsonObject obj && obj[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    // the phone sends utc and every other time in the app is local
    DateTime? ReadTime(JsonNode? payload) =>
        DateTimeOffset.TryParse(Read(payload, "taken_at"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? TimeZoneInfo.ConvertTime(t, clock.LocalTimeZone).DateTime : null;

    DateTime Now() => clock.Now();
}
