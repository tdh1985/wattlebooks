// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Core.Rules;

// carries every problem at once so the ui can show them together
public sealed class ValidationException(IReadOnlyList<string> errors) : Exception(string.Join(" ", errors))
{
    public ValidationException(string error) : this([error]) { }

    public IReadOnlyList<string> Errors { get; } = errors;

    public static void ThrowIfAny(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0) throw new ValidationException(errors);
    }
}
