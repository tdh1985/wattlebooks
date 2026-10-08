// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Ui;

// blazor writes a true bool as an empty attribute and drops a false one
public static class Aria
{
    public static string Bool(bool value) => value ? "true" : "false";

    public static string? Hidden(bool value) => value ? "true" : null;
}
