using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Serilog.Sinks.Dlt.Protocol;

namespace Serilog.Sinks.Dlt.Tests.FakeDaemon;

internal readonly record struct DecodedMessage(
    string EcuId, string AppId, string ContextId, byte Mcnt,
    DltLogLevel LogLevel, uint Timestamp, IReadOnlyList<DecodedArgument> Arguments);

internal readonly record struct DecodedArgument(DltArgumentKind Kind, object Value);

internal static class DltDecoder
{
    public static (DecodedMessage Message, int BytesConsumed) ParseFrame(ReadOnlySpan<byte> data)
    {
        if (data.Length < DltConstants.StandardHeaderSize + DltConstants.ExtendedHeaderSize)
            throw new InvalidOperationException("frame truncated");

        var htyp = data[0];
        if ((htyp & DltConstants.HtypUseExtendedHeader) == 0) throw new InvalidOperationException("UEH expected");
        if ((htyp & DltConstants.HtypMsbFirst) != 0) throw new InvalidOperationException("MSBF=1 unsupported");

        var mcnt = data[1];
        var len  = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(2, 2));
        var ecu  = Encoding.ASCII.GetString(TrimNulls(data.Slice(4, 4)));
        var tmsp = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8, 4));

        var msin = data[12];
        var noar = data[13];
        var apid = Encoding.ASCII.GetString(TrimNulls(data.Slice(14, 4)));
        var ctid = Encoding.ASCII.GetString(TrimNulls(data.Slice(18, 4)));
        var level = (DltLogLevel)((msin >> DltConstants.MsinLogLevelShift) & 0x0F);

        var payload = data.Slice(22, len - 22);
        var args = new List<DecodedArgument>(noar);
        var cursor = 0;
        for (var i = 0; i < noar; i++)
        {
            var (arg, consumed) = ParseArgument(payload[cursor..]);
            args.Add(arg);
            cursor += consumed;
        }

        return (new DecodedMessage(ecu, apid, ctid, mcnt, level, tmsp, args), len);
    }

    private static (DecodedArgument Arg, int BytesConsumed) ParseArgument(ReadOnlySpan<byte> data)
    {
        var info = BinaryPrimitives.ReadUInt32LittleEndian(data[..4]);
        var typeLen = info & DltConstants.TypeInfoTypeLengthMask;

        if ((info & DltConstants.TypeInfoString) != 0)
        {
            var len = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2));
            var s = Encoding.ASCII.GetString(data.Slice(6, len - 1));
            return (new DecodedArgument(DltArgumentKind.String, s), 6 + len);
        }
        if ((info & DltConstants.TypeInfoBool) != 0)
        {
            return (new DecodedArgument(DltArgumentKind.Bool, data[4] != 0), 4 + 1);
        }
        if ((info & DltConstants.TypeInfoRaw) != 0)
        {
            var len = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2));
            var bytes = data.Slice(6, len).ToArray();
            return (new DecodedArgument(DltArgumentKind.Raw, bytes), 6 + len);
        }
        if ((info & DltConstants.TypeInfoFloat) != 0)
        {
            if (typeLen == DltConstants.TypeInfoLength32Bit)
                return (new DecodedArgument(DltArgumentKind.Float32, BinaryPrimitives.ReadSingleLittleEndian(data.Slice(4, 4))), 4 + 4);
            return (new DecodedArgument(DltArgumentKind.Float64, BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(4, 8))), 4 + 8);
        }
        var isSigned = (info & DltConstants.TypeInfoSint) != 0;
        return typeLen switch
        {
            DltConstants.TypeInfoLength8Bit  => isSigned
                ? (new DecodedArgument(DltArgumentKind.Int8, (sbyte)data[4]), 4 + 1)
                : (new DecodedArgument(DltArgumentKind.UInt8, data[4]),       4 + 1),
            DltConstants.TypeInfoLength16Bit => isSigned
                ? (new DecodedArgument(DltArgumentKind.Int16, BinaryPrimitives.ReadInt16LittleEndian(data.Slice(4, 2))), 4 + 2)
                : (new DecodedArgument(DltArgumentKind.UInt16, BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2))), 4 + 2),
            DltConstants.TypeInfoLength32Bit => isSigned
                ? (new DecodedArgument(DltArgumentKind.Int32, BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4))), 4 + 4)
                : (new DecodedArgument(DltArgumentKind.UInt32, BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4))), 4 + 4),
            DltConstants.TypeInfoLength64Bit => isSigned
                ? (new DecodedArgument(DltArgumentKind.Int64, BinaryPrimitives.ReadInt64LittleEndian(data.Slice(4, 8))), 4 + 8)
                : (new DecodedArgument(DltArgumentKind.UInt64, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(4, 8))), 4 + 8),
            _ => throw new InvalidOperationException($"unknown type length {typeLen}"),
        };
    }

    private static ReadOnlySpan<byte> TrimNulls(ReadOnlySpan<byte> id)
    {
        var end = id.Length;
        while (end > 0 && id[end - 1] == 0) end--;
        return id[..end];
    }
}
