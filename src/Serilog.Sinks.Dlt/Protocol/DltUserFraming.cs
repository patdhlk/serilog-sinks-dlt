using System;
using System.Buffers;
using System.Buffers.Binary;

namespace Serilog.Sinks.Dlt.Protocol;

// libdlt's "user header" framing — the protocol an application speaks to
// dlt-daemon when connecting via its application interface (Unix socket / FIFO).
// Each transmission from app -> daemon is prefixed with an 8-byte DltUserHeader
// (pattern "DUH\x01" + uint32 message type). Without this prefix, the daemon's
// parser silently drops the frames.
//
// Reference: COVESA dlt-daemon, src/shared/dlt_user_shared.h
internal static class DltUserFraming
{
    // 'D','U','H',0x01
    public static readonly byte[] UserHeaderPattern = [(byte)'D', (byte)'U', (byte)'H', 0x01];

    public const int UserHeaderSize = 8;

    // DltUserMessage enum values (subset — we only use the ones we need).
    public enum UserMessage : uint
    {
        Log = 1,
        RegisterApplication = 2,
        UnregisterApplication = 3,
        RegisterContext = 4,
        UnregisterContext = 5,
        LogLevel = 6,
    }

    /// <summary>Writes the 8-byte user header (pattern + message type) to <paramref name="dest"/>.</summary>
    public static void WriteUserHeader(Span<byte> dest, UserMessage message)
    {
        UserHeaderPattern.CopyTo(dest);
        BinaryPrimitives.WriteUInt32LittleEndian(dest.Slice(4, 4), (uint)message);
    }

    /// <summary>
    /// Builds the registration "greeting" bytes an app must send to dlt-daemon
    /// before any log messages, declaring its APID and PID. Returned bytes can
    /// be written to the transport as-is.
    /// </summary>
    public static byte[] BuildRegisterApplicationMessage(string appId, int pid, string description = "")
    {
        // libdlt's DltUserControlMsgRegisterApplication (PACKED) for v2.18.x:
        //   char     apid[4];
        //   pid_t    pid;                   // int32 on Linux
        //   uint32_t description_length;
        // Wire layout: DltUserHeader(8) + apid(4) + pid(4) + description_length(4) + description bytes.
        var descBytes = System.Text.Encoding.ASCII.GetBytes(description);
        var total = UserHeaderSize + DltConstants.IdSize + 4 + 4 + descBytes.Length;
        var buffer = new byte[total];
        var span = buffer.AsSpan();

        WriteUserHeader(span, UserMessage.RegisterApplication);

        // apid (4 bytes, right-padded with 0)
        var apidSpan = span.Slice(8, DltConstants.IdSize);
        apidSpan.Clear();
        var apidLen = Math.Min(appId.Length, DltConstants.IdSize);
        for (var i = 0; i < apidLen; i++) apidSpan[i] = (byte)appId[i];

        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(12, 4), (uint)pid);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), (uint)descBytes.Length);
        descBytes.CopyTo(span.Slice(20));

        return buffer;
    }

    /// <summary>
    /// Builds the REGISTER_CONTEXT message libdlt sends to declare a context
    /// (APID/CTID pair) to dlt-daemon. Without this, the daemon's auto-context
    /// path may drop log messages on Ubuntu's apt-built daemon.
    /// Reference: libdlt v2.18.10 `DltUserControlMsgRegisterContext`.
    /// </summary>
    public static byte[] BuildRegisterContextMessage(string appId, string contextId, int pid, string description = "")
    {
        // Layout:
        //   DltUserHeader (8): pattern + message_type=REGISTER_CONTEXT(4)
        //   apid (4)
        //   ctid (4)
        //   log_level_pos (4, int32, little-endian)
        //   log_level (1, int8) — -1 means "not set, use default"
        //   trace_status (1, int8) — -1 means "not set"
        //   pid (4, int32)
        //   description_length (4, uint32)
        //   description (variable)
        var descBytes = System.Text.Encoding.ASCII.GetBytes(description);
        var total = UserHeaderSize + DltConstants.IdSize + DltConstants.IdSize + 4 + 1 + 1 + 4 + 4 + descBytes.Length;
        var buffer = new byte[total];
        var span = buffer.AsSpan();

        WriteUserHeader(span, UserMessage.RegisterContext);

        // apid (offset 8)
        var apidSpan = span.Slice(8, DltConstants.IdSize);
        apidSpan.Clear();
        var apidLen = Math.Min(appId.Length, DltConstants.IdSize);
        for (var i = 0; i < apidLen; i++) apidSpan[i] = (byte)appId[i];

        // ctid (offset 12)
        var ctidSpan = span.Slice(12, DltConstants.IdSize);
        ctidSpan.Clear();
        var ctidLen = Math.Min(contextId.Length, DltConstants.IdSize);
        for (var i = 0; i < ctidLen; i++) ctidSpan[i] = (byte)contextId[i];

        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(16, 4), 0);        // log_level_pos
        // libdlt v2.18.10 uses DLT_USER_LOG_LEVEL_NOT_SET = -2 (0xFE) and
        // DLT_USER_TRACE_STATUS_NOT_SET = -2 to mean "use daemon defaults".
        span[20] = unchecked((byte)(sbyte)(-2));                              // log_level = NOT_SET
        span[21] = unchecked((byte)(sbyte)(-2));                              // trace_status = NOT_SET
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(22, 4), pid);      // pid
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(26, 4), (uint)descBytes.Length);
        descBytes.CopyTo(span.Slice(30));

        return buffer;
    }
}

/// <summary>Selects the per-message wrapping the sink applies on encode.</summary>
internal enum DltFramingMode
{
    /// <summary>No wrapping — emit just the DLT v1 frame.</summary>
    None,
    /// <summary>Prepend the 16-byte DLT storage header (for .dlt files).</summary>
    StorageHeader,
    /// <summary>Prepend libdlt's 8-byte DltUserHeader with message type LOG (for daemon ingestion).</summary>
    UserHeader,
}
