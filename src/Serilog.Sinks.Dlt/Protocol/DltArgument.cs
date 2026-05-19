using System;

namespace Serilog.Sinks.Dlt.Protocol;

internal enum DltArgumentKind : byte
{
    String,
    Bool,
    Int8, Int16, Int32, Int64,
    UInt8, UInt16, UInt32, UInt64,
    Float32, Float64,
    Raw,
}

internal readonly struct DltArgument
{
    public readonly DltArgumentKind Kind;
    private readonly long _scalar;
    private readonly object? _reference;

    private DltArgument(DltArgumentKind kind, long scalar, object? reference)
    {
        Kind = kind;
        _scalar = scalar;
        _reference = reference;
    }

    public static DltArgument String(string v)  => new(DltArgumentKind.String, 0, v ?? string.Empty);
    public static DltArgument Bool(bool v)      => new(DltArgumentKind.Bool, v ? 1 : 0, null);
    public static DltArgument Int8(sbyte v)     => new(DltArgumentKind.Int8, v, null);
    public static DltArgument Int16(short v)    => new(DltArgumentKind.Int16, v, null);
    public static DltArgument Int32(int v)      => new(DltArgumentKind.Int32, v, null);
    public static DltArgument Int64(long v)     => new(DltArgumentKind.Int64, v, null);
    public static DltArgument UInt8(byte v)     => new(DltArgumentKind.UInt8, v, null);
    public static DltArgument UInt16(ushort v)  => new(DltArgumentKind.UInt16, v, null);
    public static DltArgument UInt32(uint v)    => new(DltArgumentKind.UInt32, v, null);
    public static DltArgument UInt64(ulong v)   => new(DltArgumentKind.UInt64, unchecked((long)v), null);
    public static DltArgument Float32(float v)  => new(DltArgumentKind.Float32, BitConverter.SingleToInt32Bits(v), null);
    public static DltArgument Float64(double v) => new(DltArgumentKind.Float64, BitConverter.DoubleToInt64Bits(v), null);
    public static DltArgument Raw(byte[] v)     => new(DltArgumentKind.Raw, 0, v ?? Array.Empty<byte>());

    public string AsString() => (string)(_reference ?? string.Empty);
    public bool   AsBool()   => _scalar != 0;
    public sbyte  AsInt8()   => (sbyte)_scalar;
    public short  AsInt16()  => (short)_scalar;
    public int    AsInt32()  => (int)_scalar;
    public long   AsInt64()  => _scalar;
    public byte   AsUInt8()  => (byte)_scalar;
    public ushort AsUInt16() => (ushort)_scalar;
    public uint   AsUInt32() => (uint)_scalar;
    public ulong  AsUInt64() => unchecked((ulong)_scalar);
    public float  AsFloat32() => BitConverter.Int32BitsToSingle((int)_scalar);
    public double AsFloat64() => BitConverter.Int64BitsToDouble(_scalar);
    public ReadOnlyMemory<byte> AsRaw() => (byte[])(_reference ?? Array.Empty<byte>());
}
