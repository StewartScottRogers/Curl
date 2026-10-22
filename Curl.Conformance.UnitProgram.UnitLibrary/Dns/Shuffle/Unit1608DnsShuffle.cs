using Curl.Networking;

namespace Curl.Conformance.UnitProgram.Dns.Shuffle;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1608.c</c>: <c>dns_shuffle_addr</c>, here
/// <see cref="DnsAddressShuffle.Shuffle{T}"/> over the cryptographic generator, which
/// <c>CURLOPT_DNS_SHUFFLE_ADDRESSES</c> runs on every resolved name. The C program shuffles a list of eight
/// addresses up to ten times and stops as soon as its head changes; a head that never changes is its one
/// failure, written to standard error with the C program's message. Curl has no <c>Curl_addrinfo</c> list:
/// the addresses are the numbers 0 to 7, and the head is the first of the list a shuffle gives.
/// </summary>
/// <param name="shuffle">Shuffles one list; <see cref="DnsAddressShuffle.Shuffle{T}"/> unless a test injects another.</param>
public sealed class Unit1608DnsShuffle(Func<IReadOnlyList<int>, IReadOnlyList<int>> shuffle) : IUnitProgram
{
    private const int Attempts = 10;

    private static readonly int[] Addresses = [0, 1, 2, 3, 4, 5, 6, 7];

    /// <summary>Creates the port over <see cref="DnsAddressShuffle.Shuffle{T}"/>.</summary>
    public Unit1608DnsShuffle()
        : this(addresses => DnsAddressShuffle.Shuffle(addresses, DnsAddressShuffle.CryptographicRandomBelow))
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1608";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<int> addresses = Addresses;
        for (int attempt = 0; attempt < Attempts && addresses[0] == Addresses[0]; attempt++)
        {
            addresses = shuffle(addresses);
        }

        if (addresses[0] != Addresses[0])
        {
            return new UnitProgramResult(0);
        }

        await context.StandardError.WriteAsync("unit1608.c Assertion FAILED: addresses are not being reordered\n").ConfigureAwait(false);
        return new UnitProgramResult(1);
    }
}
