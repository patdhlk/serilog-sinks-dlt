using System;
using System.Buffers;
using System.Buffers.Binary;
using FluentAssertions;
using Serilog.Sinks.Dlt.Protocol;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Protocol;

public class DltEncoderArgumentTests
{
    private const int HeaderEnd = DltConstants.StandardHeaderSize + DltConstants.ExtendedHeaderSize;

    private static byte[] EncodePayload(params DltArgument[] args)
    {
        var msg = new DltMessage("ECU1", "APID", "CTID", 0, 0, DltLogLevel.Info, args, args.Length, DateTimeOffset.UnixEpoch);
        var writer = new ArrayBufferWriter<byte>();
        DltEncoder.Encode(msg, writer);
        return writer.WrittenSpan[HeaderEnd..].ToArray();
    }

    [Fact]
    public void String_writes_ascii_with_u16_length_and_null_terminator()
    {
        var payload = EncodePayload(DltArgument.String("hi"));
        var info = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4));
        info.Should().Be(DltConstants.TypeInfoString | DltConstants.TypeInfoLength16Bit | DltConstants.TypeInfoScodAscii);

        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(4, 2));
        len.Should().Be(3); // "hi" + '\0'
        payload.AsSpan(6, 3).ToArray().Should().Equal((byte)'h', (byte)'i', 0);
    }

    [Fact]
    public void String_empty_writes_just_null_terminator()
    {
        var payload = EncodePayload(DltArgument.String(""));
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(4, 2));
        len.Should().Be(1);
        payload[6].Should().Be(0);
    }

    [Fact]
    public void Bool_true_writes_one_byte_one()
    {
        var payload = EncodePayload(DltArgument.Bool(true));
        var info = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4));
        info.Should().Be(DltConstants.TypeInfoBool | DltConstants.TypeInfoLength8Bit);
        payload[4].Should().Be(1);
    }

    [Fact]
    public void Bool_false_writes_one_byte_zero()
    {
        var payload = EncodePayload(DltArgument.Bool(false));
        payload[4].Should().Be(0);
    }
}
