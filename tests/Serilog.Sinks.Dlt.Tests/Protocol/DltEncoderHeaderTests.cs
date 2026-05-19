using System;
using System.Buffers;
using FluentAssertions;
using Serilog.Sinks.Dlt.Protocol;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Protocol;

public class DltEncoderHeaderTests
{
    private static DltMessage MakeMessage(params DltArgument[] args) => new(
        ecuId: "ECU1",
        appId: "MYAP",
        contextId: "CTXA",
        messageCounter: 7,
        timestamp: 0x0011_2233u,
        logLevel: DltLogLevel.Info,
        arguments: args,
        argumentCount: args.Length,
        storageWallClock: DateTimeOffset.UnixEpoch);

    [Fact]
    public void Encode_writes_standard_header_layout()
    {
        var writer = new ArrayBufferWriter<byte>();
        var msg = MakeMessage(DltArgument.String("hi"));

        DltEncoder.Encode(msg, writer);

        var bytes = writer.WrittenSpan;
        bytes[0].Should().Be(DltConstants.HtypDefault);
        bytes[1].Should().Be((byte)7);
        ((bytes[2] << 8) | bytes[3]).Should().Be(bytes.Length);
        bytes.Slice(4, 4).ToArray().Should().Equal("ECU1"u8.ToArray());
        var tmsp = (uint)((bytes[8] << 24) | (bytes[9] << 16) | (bytes[10] << 8) | bytes[11]);
        tmsp.Should().Be(0x0011_2233u);
    }

    [Fact]
    public void Encode_writes_extended_header_after_standard()
    {
        var writer = new ArrayBufferWriter<byte>();
        var msg = MakeMessage(DltArgument.String("hi"));

        DltEncoder.Encode(msg, writer);
        var bytes = writer.WrittenSpan;

        var msin = bytes[12];
        (msin & DltConstants.MsinVerbose).Should().Be(DltConstants.MsinVerbose);
        ((msin >> DltConstants.MsinLogLevelShift) & 0x0F).Should().Be((int)DltLogLevel.Info);

        bytes[13].Should().Be((byte)1);
        bytes.Slice(14, 4).ToArray().Should().Equal("MYAP"u8.ToArray());
        bytes.Slice(18, 4).ToArray().Should().Equal("CTXA"u8.ToArray());
    }

    [Fact]
    public void Encode_pads_short_ids_with_zero()
    {
        var writer = new ArrayBufferWriter<byte>();
        var msg = new DltMessage("E", "A", "C", 0, 0, DltLogLevel.Info,
            new[] { DltArgument.String("x") }, 1, DateTimeOffset.UnixEpoch);

        DltEncoder.Encode(msg, writer);
        var bytes = writer.WrittenSpan;
        bytes.Slice(4, 4).ToArray().Should().Equal((byte)'E', 0, 0, 0);
        bytes.Slice(14, 4).ToArray().Should().Equal((byte)'A', 0, 0, 0);
        bytes.Slice(18, 4).ToArray().Should().Equal((byte)'C', 0, 0, 0);
    }
}
