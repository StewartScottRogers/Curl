namespace Curl.Protobuf.Descriptors;

/// <summary>A range of reserved field or enum value numbers.</summary>
/// <param name="Start">The first number reserved.</param>
/// <param name="End">The last number reserved, inclusive; <c>max</c> is the largest number allowed.</param>
public sealed record ProtoReservedRange(int Start, int End);
