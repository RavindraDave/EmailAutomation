using System;
using System.Collections.Generic;
using System.Linq;

namespace EmailAutomation.Domain.Models;

public static class EmailAddressList
{
    private static readonly char[] Separators = [';', ','];

    public static IReadOnlyList<string> Split(string? addresses) =>
        (addresses ?? string.Empty)
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
