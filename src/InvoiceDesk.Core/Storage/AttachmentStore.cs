// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Core.Storage;

public sealed class AttachmentStore(AppPaths paths, TimeProvider clock)
{
    public const long MaxBytes = 25L * 1024 * 1024;

    // the logo lands in every pdf, so a huge one would bloat each invoice
    public const long MaxLogoBytes = 5L * 1024 * 1024;
    public const string AcceptList = ".pdf,.jpg,.jpeg,.png,.webp";
    public const string ImageAcceptList = ".jpg,.jpeg,.png,.webp";

    static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
    };

    public static string? ValidationError(string fileName, long sizeBytes)
    {
        if (!Types.ContainsKey(Path.GetExtension(fileName)))
            return $"{fileName} isn't a supported file. Use PDF, JPG, PNG or WebP.";
        if (sizeBytes <= 0) return $"{fileName} is empty.";
        if (sizeBytes > MaxBytes) return $"{fileName} is larger than 25 MB.";
        return null;
    }

    public static string ContentTypeFor(string fileName) =>
        Types.GetValueOrDefault(Path.GetExtension(fileName), "application/octet-stream");

    public async Task<StagedFile> StageAsync(Stream content, string originalFileName, long sizeBytes, CancellationToken ct = default)
    {
        if (ValidationError(originalFileName, sizeBytes) is { } error) throw new ValidationException(error);

        Directory.CreateDirectory(paths.Staging);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(originalFileName).ToLowerInvariant();
        var full = Path.Combine(paths.Staging, name);
        try
        {
            await using (var target = File.Create(full)) await content.CopyToAsync(target, ct);
            var actual = new FileInfo(full).Length;
            if (ValidationError(originalFileName, actual) is { } sizeError) throw new ValidationException(sizeError);
            return new StagedFile(full, Path.GetFileName(originalFileName), "staging/" + name);
        }
        catch
        {
            TryDelete(full);
            throw;
        }
    }

    public Attachment Import(string sourcePath, string originalFileName, AttachmentKind kind)
    {
        var size = new FileInfo(sourcePath).Length;
        if (ValidationError(originalFileName, size) is { } error) throw new ValidationException(error);

        var now = clock.GetLocalNow();
        var year = now.Year.ToString(CultureInfo.InvariantCulture);
        var relative = $"attachments/{year}/{Guid.NewGuid():N}{Path.GetExtension(originalFileName).ToLowerInvariant()}";
        var full = paths.FullPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.Copy(sourcePath, full);

        return new Attachment
        {
            OriginalFileName = Path.GetFileName(originalFileName),
            StoredPath = relative,
            ContentType = ContentTypeFor(originalFileName),
            SizeBytes = size,
            CreatedAt = now.DateTime,
            Kind = kind,
        };
    }

    // all or nothing, so a failed batch leaves no orphan copies behind
    public List<Attachment> ImportAll(IEnumerable<StagedFile> files, AttachmentKind kind)
    {
        var done = new List<Attachment>();
        try
        {
            foreach (var f in files) done.Add(Import(f.TempPath, f.OriginalFileName, kind));
            return done;
        }
        catch
        {
            DeleteFiles(done);
            throw;
        }
    }

    // staged copies are only needed until the real copy is saved
    public void DiscardStaged(IEnumerable<StagedFile> files)
    {
        foreach (var f in files) TryDelete(f.TempPath);
    }

    public string FullPath(Attachment attachment) => paths.FullPath(attachment.StoredPath);

    public void DeleteFile(Attachment attachment) => TryDelete(FullPath(attachment));

    public void DeleteFiles(IEnumerable<Attachment> attachments)
    {
        foreach (var a in attachments) DeleteFile(a);
    }

    public void ClearStaging()
    {
        if (!Directory.Exists(paths.Staging)) return;
        foreach (var file in Directory.GetFiles(paths.Staging)) TryDelete(file);
    }

    static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
