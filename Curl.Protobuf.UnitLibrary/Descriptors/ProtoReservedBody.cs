namespace Curl.Protobuf.Descriptors;

/// <summary>A message or enum being read, which can hold options and reserve numbers and names.</summary>
internal abstract class ProtoReservedBody
{
    /// <summary>The numbers reserved.</summary>
    public List<ProtoReservedRange> ReservedRanges { get; } = [];

    /// <summary>The names reserved.</summary>
    public List<string> ReservedNames { get; } = [];

    /// <summary>The options of the message or enum.</summary>
    public List<ProtoOption> Options { get; } = [];
}
