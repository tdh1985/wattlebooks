// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.IO;
using InvoiceDesk.Core.Rules;
using Microsoft.Extensions.Logging;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace InvoiceDesk.App.Host;

// the text reader built into windows, so receipts are read on this pc for free
public sealed class ReceiptReader(ILogger<ReceiptReader> log)
{
    // wide enough for small receipt print, small enough to read quickly
    const uint PdfRenderWidth = 2000;

    // a stuck pdf render or ocr call would leave the drawer scanning forever
    static readonly TimeSpan ReadLimit = TimeSpan.FromSeconds(20);

    public async Task<string?> ReadTextAsync(string path)
    {
        try
        {
            return await ReadAsync(path).WaitAsync(ReadLimit);
        }
        catch (TimeoutException)
        {
            log.LogWarning("Gave up reading {File} after {Seconds} seconds", Path.GetFileName(path), ReadLimit.TotalSeconds);
            return null;
        }
        catch (Exception ex)
        {
            // a photo windows can't decode, such as webp without its codec, just isn't read
            log.LogWarning(ex, "Couldn't read text from {File}", Path.GetFileName(path));
            return null;
        }
    }

    async Task<string?> ReadAsync(string path)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            log.LogWarning("No Windows OCR language is installed, so receipts can't be read");
            return null;
        }
        using var bitmap = await LoadAsync(path);
        if (bitmap is null) return null;
        var result = await engine.RecognizeAsync(bitmap);
        return ReceiptParser.JoinRows(result.Lines.SelectMany(l => l.Words)
            .Select(w => new PageWord(w.Text, w.BoundingRect.X, w.BoundingRect.Y, w.BoundingRect.Width, w.BoundingRect.Height)));
    }

    static async Task<SoftwareBitmap?> LoadAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
        if (!Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            using var image = await file.OpenReadAsync();
            return await DecodeAsync(image);
        }

        var pdf = await PdfDocument.LoadFromFileAsync(file);
        if (pdf.PageCount == 0) return null;
        using var page = pdf.GetPage(0);
        using var rendered = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(rendered, new PdfPageRenderOptions { DestinationWidth = PdfRenderWidth });
        return await DecodeAsync(rendered);
    }

    static async Task<SoftwareBitmap> DecodeAsync(IRandomAccessStream stream)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var transform = new BitmapTransform { InterpolationMode = BitmapInterpolationMode.Fant };
        var longest = Math.Max(decoder.OrientedPixelWidth, decoder.OrientedPixelHeight);
        if (longest > OcrEngine.MaxImageDimension)
        {
            var scale = (double)OcrEngine.MaxImageDimension / longest;
            transform.ScaledWidth = (uint)(decoder.PixelWidth * scale);
            transform.ScaledHeight = (uint)(decoder.PixelHeight * scale);
        }
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
    }
}
