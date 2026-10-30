namespace Curl.Protobuf.Descriptors;

/// <summary>One <c>enum</c> of a <c>.proto</c> file.</summary>
/// <param name="Name">The enum's name as written.</param>
/// <param name="Values">The enum's values, in order; the first is 0.</param>
/// <param name="Options">The enum's <c>option</c> statements, in order.</param>
/// <param name="ReservedRanges">The value numbers the enum reserves.</param>
/// <param name="ReservedNames">The value names the enum reserves.</param>
public sealed record ProtoEnumDescriptor(
    string Name,
    IReadOnlyList<ProtoEnumValueDescriptor> Values,
    IReadOnlyList<ProtoOption> Options,
    IReadOnlyList<ProtoReservedRange> ReservedRanges,
    IReadOnlyList<string> ReservedNames);
