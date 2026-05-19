using System;

namespace Serilog.Sinks.Dlt;

public sealed class DltSinkException : Exception
{
    public DltSinkException(string message) : base(message) { }
    public DltSinkException(string message, Exception inner) : base(message, inner) { }
}
