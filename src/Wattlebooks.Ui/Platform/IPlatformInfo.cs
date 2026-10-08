// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Ui.Platform;

public interface IPlatformInfo
{
    // what the theme picker calls the os, such as windows or macos
    string SystemName { get; }

    bool CanBeTranslucent { get; }

    bool SystemPrefersDark();
}
