// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Ui.Platform;

// nothing on this pc can make a pdf, so the message says what to do instead
public sealed class PdfUnavailableException(string message) : Exception(message);
