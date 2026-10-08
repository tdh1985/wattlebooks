// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Core.Services;

public static class ClockExtensions
{
    public static DateOnly Today(this TimeProvider clock) => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public static DateTime Now(this TimeProvider clock) => clock.GetLocalNow().DateTime;
}
