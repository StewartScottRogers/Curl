using System.Net;
using System.Net.Sockets;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Networking;

namespace Curl.Conformance.UnitProgram.UrlParsing;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1961.c</c>: <c>curlx_inet_ntop()</c> and <c>curlx_inet_pton()</c>.
/// Curl writes an address as <c>inet_ntop</c> does with <see cref="IPAddress.ToString()"/> over the address's
/// bytes (no scope id), and reads one as <c>inet_pton</c> does with <see cref="DnsServerList.TryParseAddress"/>.
/// The C program's two checks, in its order: <c>test_ntop</c> writes <c>192.168.100.1</c>,
/// <c>fe80::214:4fff:fe0b:76c8</c> and the RFC 4291 IPv4-compatible <c>::127.0.0.1</c>, each compared by its
/// prefix as <c>memcmp</c> does; <c>test_pton</c> reads <c>192.168.100.1</c> and <c>fe80::214:4fff:fe0b:76c8</c>
/// and compares every byte, where its guard byte past the end becomes "exactly 4 or 16 bytes".
/// </summary>
/// <param name="ntop">Writes an address's bytes as text; <see cref="IPAddress.ToString()"/> unless a test injects another.</param>
/// <param name="pton">
/// Reads an address of one family, <see langword="null"/> when the text is not one;
/// <see cref="DnsServerList.TryParseAddress"/> unless a test injects another.
/// </param>
public sealed class Unit1961InetAddressText(Func<byte[], string> ntop, Func<string, AddressFamily, IPAddress?> pton) : IUnitProgram
{
    private static readonly byte[] Ipv4Bytes = [0xc0, 0xa8, 0x64, 0x01];

    private static readonly byte[] Ipv6Bytes = [0xfe, 0x80, 0, 0, 0, 0, 0, 0, 0x02, 0x14, 0x4f, 0xff, 0xfe, 0x0b, 0x76, 0xc8];

    private static readonly byte[] Ipv4CompatibleBytes = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x7f, 0, 0, 0x01];

    private static readonly (byte[] Bytes, string Text)[] NtopRows =
    [
        (Ipv4Bytes, "192.168.100.1"),
        (Ipv6Bytes, "fe80::214:4fff:fe0b:76c8"),
        (Ipv4CompatibleBytes, "::127.0.0.1"),
    ];

    private static readonly (string Text, AddressFamily Family, byte[] Bytes)[] PtonRows =
    [
        ("192.168.100.1", AddressFamily.InterNetwork, Ipv4Bytes),
        ("fe80::214:4fff:fe0b:76c8", AddressFamily.InterNetworkV6, Ipv6Bytes),
    ];

    /// <summary>Creates the port over <see cref="IPAddress.ToString()"/> and <see cref="DnsServerList.TryParseAddress"/>.</summary>
    public Unit1961InetAddressText()
        : this(bytes => new IPAddress(bytes).ToString(), ParseAddress)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1961";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        checks.FailUnless(NtopRows.All(row => ntop(row.Bytes).StartsWith(row.Text, StringComparison.Ordinal)), "curlx_inet_ntop()");
        checks.FailUnless(PtonRows.All(PtonReads), "curlx_inet_pton()");
        return Task.FromResult(checks.Result);
    }

    // TryParseAddress leaves the address null when it refuses the text.
    private static IPAddress? ParseAddress(string text, AddressFamily family)
    {
        _ = DnsServerList.TryParseAddress(text, family, out IPAddress? address);
        return address;
    }

    private bool PtonReads((string Text, AddressFamily Family, byte[] Bytes) row) =>
        pton(row.Text, row.Family) is { } address && address.GetAddressBytes().AsSpan().SequenceEqual(row.Bytes);
}
