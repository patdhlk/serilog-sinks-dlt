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

    [Fact]
    public void Int8_writes_signed_8bit()
    {
        var payload = EncodePayload(DltArgument.Int8(-7));
        BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4))
            .Should().Be(DltConstants.TypeInfoSint | DltConstants.TypeInfoLength8Bit);
        ((sbyte)payload[4]).Should().Be(-7);
    }

    [Fact]
    public void Int32_writes_signed_32bit_little_endian()
    {
        var payload = EncodePayload(DltArgument.Int32(-1234567));
        BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4))
            .Should().Be(DltConstants.TypeInfoSint | DltConstants.TypeInfoLength32Bit);
        BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4)).Should().Be(-1234567);
    }

    [Fact]
    public void UInt64_writes_unsigned_64bit_little_endian()
    {
        var payload = EncodePayload(DltArgument.UInt64(0xDEAD_BEEF_F00D_BABEul));
        BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4))
            .Should().Be(DltConstants.TypeInfoUint | DltConstants.TypeInfoLength64Bit);
        BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(4, 8)).Should().Be(0xDEAD_BEEF_F00D_BABEul);
    }

    [Fact]
    public void Float32_writes_ieee754_little_endian()
    {
        var payload = EncodePayload(DltArgument.Float32(3.14159f));
        BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4))
            .Should().Be(DltConstants.TypeInfoFloat | DltConstants.TypeInfoLength32Bit);
        BinaryPrimitives.ReadSingleLittleEndian(payload.AsSpan(4, 4)).Should().Be(3.14159f);
    }

    [Fact]
    public void Float64_writes_ieee754_little_endian()
    {
        var payload = EncodePayload(DltArgument.Float64(double.MaxValue));
        BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4))
            .Should().Be(DltConstants.TypeInfoFloat | DltConstants.TypeInfoLength64Bit);
        BinaryPrimitives.ReadDoubleLittleEndian(payload.AsSpan(4, 8)).Should().Be(double.MaxValue);
    }

    [Fact]
    public void Raw_writes_u16_length_then_bytes()
    {
        var data = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        var payload = EncodePayload(DltArgument.Raw(data));
        BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4)).Should().Be(DltConstants.TypeInfoRaw);
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(4, 2)).Should().Be(4);
        payload.AsSpan(6, 4).ToArray().Should().Equal(data);
    }
}
