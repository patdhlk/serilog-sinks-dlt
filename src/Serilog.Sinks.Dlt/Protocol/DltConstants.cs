namespace Serilog.Sinks.Dlt.Protocol;

internal static class DltConstants
{
    // Standard header HTYP bit flags (per COVESA DLT v1 spec, section 5.1.2)
    public const byte HtypUseExtendedHeader = 0x01;  // UEH
    public const byte HtypMsbFirst          = 0x02;  // MSBF (0 => little-endian payload)
    public const byte HtypWithEcuId         = 0x04;  // WEID
    public const byte HtypWithSessionId     = 0x08;  // WSID
    public const byte HtypWithTimestamp     = 0x10;  // WTMS
    public const byte HtypVersion1          = 0x20;  // VERS upper 3 bits = 001

    // Composite HTYP we always use: UEH + WEID + WTMS + VERS=1, MSBF=0
    public const byte HtypDefault = HtypUseExtendedHeader | HtypWithEcuId | HtypWithTimestamp | HtypVersion1;

    // HTYP including WSID — what libdlt v2.18.x sends. Ubuntu's dlt-daemon
    // appears to require this to route LOG messages past registration.
    public const byte HtypWithSession = HtypDefault | HtypWithSessionId;

    // Extended header MSIN bit fields (section 5.1.3.1)
    public const byte MsinVerbose       = 0x01;  // VERB
    public const byte MsinMessageTypeLog = 0x00; // MSTP = 0 (DLT_TYPE_LOG)
    // Log level lives in the upper nibble of MSIN.

    public const byte MsinLogLevelShift = 4;

    // TYPE_INFO bit fields (section 5.1.5)
    public const uint TypeInfoTypeLengthMask = 0x0000_000F;
    public const uint TypeInfoLength8Bit     = 0x0000_0001;
    public const uint TypeInfoLength16Bit    = 0x0000_0002;
    public const uint TypeInfoLength32Bit    = 0x0000_0003;
    public const uint TypeInfoLength64Bit    = 0x0000_0004;

    public const uint TypeInfoBool   = 0x0000_0010;
    public const uint TypeInfoSint   = 0x0000_0020;
    public const uint TypeInfoUint   = 0x0000_0040;
    public const uint TypeInfoFloat  = 0x0000_0080;
    public const uint TypeInfoArray  = 0x0000_0100;
    public const uint TypeInfoString = 0x0000_0200;
    public const uint TypeInfoRaw    = 0x0000_0400;

    public const uint TypeInfoScodAscii = 0x0000_0000;  // SCOD = 0 → ASCII string
    public const uint TypeInfoScodUtf8  = 0x0000_8000;

    /// <summary>
    /// Storage header magic for .dlt files: bytes 'D','L','T',0x01.
    /// The trailing 0x01 is the protocol version marker (DLT v1).
    /// </summary>
    public static readonly byte[] StorageHeaderPattern = [(byte)'D', (byte)'L', (byte)'T', 0x01];

    // Sizes
    public const int StandardHeaderSize  = 12; // HTYP+MCNT+LEN+ECU+TMSP (no SEID)
    public const int ExtendedHeaderSize  = 10; // MSIN+NOAR+APID+CTID
    public const int StorageHeaderSize   = 16; // pattern(4) + secs(4) + usecs(4) + ecu(4)
    public const int IdSize              = 4;  // ECU / APID / CTID width

    public const int MaxArgumentCount = 255;   // NOAR is u8
}
