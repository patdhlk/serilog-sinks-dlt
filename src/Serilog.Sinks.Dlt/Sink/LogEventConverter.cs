using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.Dlt.Protocol;

namespace Serilog.Sinks.Dlt.Sink;

internal sealed class LogEventConverter
{
    private readonly string _ecuId;
    private readonly string _appId;
    private readonly CtidResolver _ctidResolver;
    private readonly long _processStartTicks = Stopwatch.GetTimestamp();
    private int _mcnt;

    public LogEventConverter(string ecuId, string appId, CtidResolver ctidResolver)
    {
        _ecuId = ecuId;
        _appId = appId;
        _ctidResolver = ctidResolver;
    }

    public DltMessage Convert(LogEvent ev)
    {
        var contextId = ResolveContextId(ev);
        var args = new List<DltArgument>(8) { DltArgument.String(ev.RenderMessage()) };

        var consumed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in ev.MessageTemplate.Tokens)
        {
            if (token is not PropertyToken pt) continue;
            if (ev.Properties.TryGetValue(pt.PropertyName, out var pv))
            {
                args.Add(MapProperty(pv));
                consumed.Add(pt.PropertyName);
            }
        }

        foreach (var kvp in ev.Properties)
        {
            if (consumed.Contains(kvp.Key)) continue;
            if (string.Equals(kvp.Key, "SourceContext", StringComparison.Ordinal)) continue;
            args.Add(MapProperty(kvp.Value));
            if (args.Count >= DltConstants.MaxArgumentCount) break;
        }

        if (ev.Exception is not null && args.Count < DltConstants.MaxArgumentCount)
            args.Add(DltArgument.String(ev.Exception.ToString()));

        var mcnt = unchecked((byte)Interlocked.Increment(ref _mcnt));
        var timestamp = (uint)(Stopwatch.GetElapsedTime(_processStartTicks).TotalMilliseconds * 10);

        return new DltMessage(
            _ecuId, _appId, contextId,
            mcnt, timestamp,
            DltLogLevelExtensions.FromSerilog(ev.Level),
            args.ToArray(), args.Count,
            ev.Timestamp);
    }

    private string ResolveContextId(LogEvent ev)
    {
        if (ev.Properties.TryGetValue("SourceContext", out var src) && src is ScalarValue { Value: string s })
            return _ctidResolver.Resolve(s);
        return _ctidResolver.Resolve(null);
    }

    private static DltArgument MapProperty(LogEventPropertyValue pv)
    {
        if (pv is ScalarValue sv)
        {
            return sv.Value switch
            {
                null              => DltArgument.String("(null)"),
                string s          => DltArgument.String(s),
                bool b            => DltArgument.Bool(b),
                sbyte i8          => DltArgument.Int8(i8),
                byte u8           => DltArgument.UInt8(u8),
                short i16         => DltArgument.Int16(i16),
                ushort u16        => DltArgument.UInt16(u16),
                int i32           => DltArgument.Int32(i32),
                uint u32          => DltArgument.UInt32(u32),
                long i64          => DltArgument.Int64(i64),
                ulong u64         => DltArgument.UInt64(u64),
                float f32         => DltArgument.Float32(f32),
                double f64        => DltArgument.Float64(f64),
                byte[] raw        => DltArgument.Raw(raw),
                DateTime dt       => DltArgument.String(dt.ToString("o")),
                DateTimeOffset dt => DltArgument.String(dt.ToString("o")),
                Guid g            => DltArgument.String(g.ToString("D")),
                _                 => DltArgument.String(sv.Value.ToString() ?? string.Empty),
            };
        }
        return DltArgument.String(pv.ToString() ?? string.Empty);
    }
}
