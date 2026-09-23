// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using Photino.NET;

namespace InvoiceDesk.Desktop.Platform;

// services are built before photino makes its window, so they reach it through this
public sealed class PhotinoWindowRef
{
    public PhotinoWindow? Window { get; set; }
}
