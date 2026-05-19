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

    public static void EncodeWithStorageHeader(in DltMessage msg, ArrayBufferWriter<byte> writer)
    {
        var hdr = writer.GetSpan(DltConstants.StorageHeaderSize);
        DltConstants.StorageHeaderPattern.CopyTo(hdr);

        var totalUs = msg.StorageWallClock.ToUnixTimeMilliseconds() * 1000L;
        var secs = (uint)(totalUs / 1_000_000L);
        var usecs = (int)(totalUs % 1_000_000L);

        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(4, 4), secs);
        BinaryPrimitives.WriteInt32LittleEndian(hdr.Slice(8, 4), usecs);
        WriteId(hdr.Slice(12, 4), msg.EcuId);
        writer.Advance(DltConstants.StorageHeaderSize);

        Encode(msg, writer);
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
            case DltArgumentKind.String: WriteString(arg.AsString(), writer); return;
            case DltArgumentKind.Bool:   WriteBool(arg.AsBool(), writer); return;
            case DltArgumentKind.Int8:   WriteScalar1(writer, DltConstants.TypeInfoSint | DltConstants.TypeInfoLength8Bit, (byte)arg.AsInt8()); return;
            case DltArgumentKind.UInt8:  WriteScalar1(writer, DltConstants.TypeInfoUint | DltConstants.TypeInfoLength8Bit, arg.AsUInt8()); return;
            case DltArgumentKind.Int16:  WriteScalar2(writer, DltConstants.TypeInfoSint | DltConstants.TypeInfoLength16Bit, unchecked((ushort)arg.AsInt16())); return;
            case DltArgumentKind.UInt16: WriteScalar2(writer, DltConstants.TypeInfoUint | DltConstants.TypeInfoLength16Bit, arg.AsUInt16()); return;
            case DltArgumentKind.Int32:  WriteScalar4(writer, DltConstants.TypeInfoSint | DltConstants.TypeInfoLength32Bit, unchecked((uint)arg.AsInt32())); return;
            case DltArgumentKind.UInt32: WriteScalar4(writer, DltConstants.TypeInfoUint | DltConstants.TypeInfoLength32Bit, arg.AsUInt32()); return;
            case DltArgumentKind.Int64:  WriteScalar8(writer, DltConstants.TypeInfoSint | DltConstants.TypeInfoLength64Bit, unchecked((ulong)arg.AsInt64())); return;
            case DltArgumentKind.UInt64: WriteScalar8(writer, DltConstants.TypeInfoUint | DltConstants.TypeInfoLength64Bit, arg.AsUInt64()); return;
            case DltArgumentKind.Float32: WriteScalar4(writer, DltConstants.TypeInfoFloat | DltConstants.TypeInfoLength32Bit, (uint)BitConverter.SingleToInt32Bits(arg.AsFloat32())); return;
            case DltArgumentKind.Float64: WriteScalar8(writer, DltConstants.TypeInfoFloat | DltConstants.TypeInfoLength64Bit, (ulong)BitConverter.DoubleToInt64Bits(arg.AsFloat64())); return;
            case DltArgumentKind.Raw:
                WriteRaw(arg.AsRaw().Span, writer);
                return;
            default: WriteString(arg.Kind.ToString(), writer); return;
        }
    }

    private static void WriteRaw(ReadOnlySpan<byte> data, ArrayBufferWriter<byte> writer)
    {
        var len = Math.Min(data.Length, ushort.MaxValue);
        var span = writer.GetSpan(4 + 2 + len);
        BinaryPrimitives.WriteUInt32LittleEndian(span[..4], DltConstants.TypeInfoRaw);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(4, 2), (ushort)len);
        data[..len].CopyTo(span.Slice(6, len));
        writer.Advance(4 + 2 + len);
    }

    private static void WriteScalar1(ArrayBufferWriter<byte> w, uint info, byte v)
    {
        var span = w.GetSpan(4 + 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span[..4], info);
        span[4] = v;
        w.Advance(4 + 1);
    }

    private static void WriteScalar2(ArrayBufferWriter<byte> w, uint info, ushort v)
    {
        var span = w.GetSpan(4 + 2);
        BinaryPrimitives.WriteUInt32LittleEndian(span[..4], info);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(4, 2), v);
        w.Advance(4 + 2);
    }

    private static void WriteScalar4(ArrayBufferWriter<byte> w, uint info, uint v)
    {
        var span = w.GetSpan(4 + 4);
        BinaryPrimitives.WriteUInt32LittleEndian(span[..4], info);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), v);
        w.Advance(4 + 4);
    }

    private static void WriteScalar8(ArrayBufferWriter<byte> w, uint info, ulong v)
    {
        var span = w.GetSpan(4 + 8);
        BinaryPrimitives.WriteUInt32LittleEndian(span[..4], info);
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(4, 8), v);
        w.Advance(4 + 8);
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
