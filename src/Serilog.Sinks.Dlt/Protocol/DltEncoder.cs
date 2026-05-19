using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Serilog.Sinks.Dlt.Protocol;

internal static class DltEncoder
{
    public static void Encode(in DltMessage msg, ArrayBufferWriter<byte> writer)
    {
        var startOffset = writer.WrittenCount;
        var argCount = Math.Min(msg.ArgumentCount, DltConstants.MaxArgumentCount);

        var hdr = writer.GetSpan(DltConstants.StandardHeaderSize + DltConstants.ExtendedHeaderSize);
        hdr[0] = DltConstants.HtypDefault;
        hdr[1] = msg.MessageCounter;
        // LEN at 2..3 patched after payload is written
        WriteId(hdr.Slice(4, 4), msg.EcuId);
        BinaryPrimitives.WriteUInt32BigEndian(hdr.Slice(8, 4), msg.Timestamp);

        var msin = (byte)(DltConstants.MsinVerbose
                          | DltConstants.MsinMessageTypeLog
                          | ((byte)msg.LogLevel << DltConstants.MsinLogLevelShift));
        hdr[12] = msin;
        hdr[13] = (byte)argCount;
        WriteId(hdr.Slice(14, 4), msg.AppId);
        WriteId(hdr.Slice(18, 4), msg.ContextId);

        writer.Advance(DltConstants.StandardHeaderSize + DltConstants.ExtendedHeaderSize);

        for (var i = 0; i < argCount; i++)
            WriteArgument(msg.Arguments[i], writer);

        var frameLength = writer.WrittenCount - startOffset;
        // Patch LEN — MemoryMarshal.AsMemory converts WrittenMemory's read-only into writable
        // without copying or reflection. ArrayBufferWriter<byte> is single-owner so this is safe.
        var writable = MemoryMarshal.AsMemory(writer.WrittenMemory);
        BinaryPrimitives.WriteUInt16BigEndian(writable.Span.Slice(startOffset + 2, 2), checked((ushort)frameLength));
    }

    private static void WriteId(Span<byte> dest, string id)
    {
        dest.Clear();
        var n = Math.Min(id.Length, DltConstants.IdSize);
        for (var i = 0; i < n; i++) dest[i] = (byte)id[i];
    }

    private static void WriteArgument(in DltArgument arg, ArrayBufferWriter<byte> writer)
    {
        switch (arg.Kind)
        {
            case DltArgumentKind.String:
                WriteString(arg.AsString(), writer);
                break;
            case DltArgumentKind.Bool:
                WriteBool(arg.AsBool(), writer);
                break;
            default:
                // Other kinds in Tasks 8-9.
                WriteString(arg.Kind.ToString(), writer);
                break;
        }
    }

    private static void WriteString(string s, ArrayBufferWriter<byte> writer)
    {
        var byteLength = s.Length + 1;
        if (byteLength > ushort.MaxValue) byteLength = ushort.MaxValue;

        var span = writer.GetSpan(4 + 2 + byteLength);
        BinaryPrimitives.WriteUInt32LittleEndian(span[..4],
            DltConstants.TypeInfoString | DltConstants.TypeInfoLength16Bit | DltConstants.TypeInfoScodAscii);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(4, 2), (ushort)byteLength);

        var payload = span.Slice(6, byteLength);
        for (var i = 0; i < byteLength - 1; i++)
        {
            var c = s[i];
            payload[i] = c < 0x80 ? (byte)c : (byte)'?';
        }
        payload[byteLength - 1] = 0;
        writer.Advance(4 + 2 + byteLength);
    }

    private static void WriteBool(bool v, ArrayBufferWriter<byte> writer)
    {
        var span = writer.GetSpan(4 + 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span[..4],
            DltConstants.TypeInfoBool | DltConstants.TypeInfoLength8Bit);
        span[4] = (byte)(v ? 1 : 0);
        writer.Advance(4 + 1);
    }
}
