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
        // Placeholder for Task 6 only. Real args added in Tasks 7-9. Emits a 1-byte ASCII STRG
        // so headers can be exercised in isolation.
        var info = DltConstants.TypeInfoString | DltConstants.TypeInfoLength16Bit | DltConstants.TypeInfoScodAscii;
        var span = writer.GetSpan(4 + 2 + 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span[..4], info);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(4, 2), 1);
        span[6] = 0;
        writer.Advance(7);
    }
}
