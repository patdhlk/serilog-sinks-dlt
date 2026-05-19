using System;
using System.Buffers;
using System.Buffers.Binary;
using FluentAssertions;
using Serilog.Sinks.Dlt.Protocol;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Protocol;

public class DltEncoderStorageTests
{
    [Fact]
    public void EncodeWithStorageHeader_prefixes_DLT_magic_and_timestamp()
    {
        var wall = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_500);
        var msg = new DltMessage("ECU1", "APID", "CTID", 0, 0, DltLogLevel.Info,
            new[] { DltArgument.String("x") }, 1, wall);
        var writer = new ArrayBufferWriter<byte>();
        DltEncoder.EncodeWithStorageHeader(msg, writer);

        var bytes = writer.WrittenSpan;
        bytes[..4].ToArray().Should().Equal((byte)'D', (byte)'L', (byte)'T', 0x01);
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4)).Should().Be(1_700_000_000u);
        BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8, 4)).Should().Be(500_000); // 500 ms => 500_000 us
        bytes.Slice(12, 4).ToArray().Should().Equal("ECU1"u8.ToArray());

        // Standard frame follows at offset 16
        bytes[16].Should().Be(DltConstants.HtypDefault);
    }
}
