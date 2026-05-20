using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Serilog.Sinks.Dlt.Sink;

internal sealed class CtidResolver
{
    private static readonly char[] Base32Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    private readonly string _defaultCtid;
    private readonly IReadOnlyDictionary<string, string>? _overrides;
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public CtidResolver(string defaultCtid, IReadOnlyDictionary<string, string>? overrides)
    {
        _defaultCtid = defaultCtid;
        _overrides = overrides;
    }

    public string Resolve(string? sourceContext)
    {
        if (string.IsNullOrEmpty(sourceContext)) return _defaultCtid;
        if (_overrides is not null && _overrides.TryGetValue(sourceContext, out var explicitCtid)) return explicitCtid;
        return _cache.GetOrAdd(sourceContext, Compute);
    }

    private static string Compute(string s)
    {
        uint hash = 2166136261;
        foreach (var ch in s)
        {
            hash ^= ch;
            hash *= 16777619;
        }
        Span<char> buf = stackalloc char[4];
        for (var i = 0; i < 4; i++)
        {
            buf[i] = Base32Alphabet[(int)(hash & 0x1F)];
            hash >>= 5;
        }
        return new string(buf);
    }
}
