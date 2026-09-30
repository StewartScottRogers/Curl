namespace Curl.Networking;

/// <summary>What a <c>--curves</c> value offers.</summary>
/// <param name="Groups">The <c>supported_groups</c> list, in order.</param>
/// <param name="KeyShares">The groups the first ClientHello sends a key share for, each one of <paramref name="Groups" />.</param>
internal sealed record OfferedGroups(IReadOnlyList<ushort> Groups, IReadOnlyList<ushort> KeyShares);
