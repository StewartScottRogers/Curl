using System.Net;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Networking;

namespace Curl.Conformance.UnitProgram.UrlParsing;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1607.c</c>: <c>CURLOPT_RESOLVE</c> parsing, here
/// <see cref="ResolveOverrides.Parse"/>. Each of the C program's eleven rows parses one entry and reads back the
/// addresses for its lowercase <c>host:port</c> (<see cref="ResolveOverrides.Find"/>, as the C program reads the
/// DNS cache entry), comparing them in order with the row's addresses and the entry's permanence (no <c>+</c>)
/// with the row's; a row with any difference counts one failure. Curl always has IPv6, so no <c>::1</c> is
/// skipped. The C program's port check holds by construction: Curl finds the addresses by the row's port.
/// </summary>
/// <param name="parse">Parses the <c>--resolve</c> entries; <see cref="ResolveOverrides.Parse"/> unless a test injects another.</param>
public sealed class Unit1607ResolveParsing(Func<IEnumerable<string>, ResolveOverrides> parse) : IUnitProgram
{
    private static readonly (string OptionValue, string Host, int Port, bool Permanent, string[] Addresses)[] Rows =
    [
        ("test.com:80:127.0.0.1, 127.0.0.2", "test.com", 80, true, []),
        ("TEST.com:80:,,127.0.0.1,,,127.0.0.2,,,,::1,,,", "test.com", 80, true, ["127.0.0.1", "127.0.0.2", "::1"]),
        ("test.com:80:::1,127.0.0.1", "test.com", 80, true, ["::1", "127.0.0.1"]),
        ("test.com:80:[::1],127.0.0.1", "test.com", 80, true, ["::1", "127.0.0.1"]),
        ("test.com:80:::1", "test.com", 80, true, ["::1"]),
        ("test.com:80:[::1]", "test.com", 80, true, ["::1"]),
        ("test.com:80:127.0.0.1", "test.com", 80, true, ["127.0.0.1"]),
        ("test.com:80:,127.0.0.1", "test.com", 80, true, ["127.0.0.1"]),
        ("test.com:80:127.0.0.1,", "test.com", 80, true, ["127.0.0.1"]),
        ("test.com:0:127.0.0.1", "test.com", 0, true, ["127.0.0.1"]),
        ("+test.com:80:127.0.0.1,", "test.com", 80, false, ["127.0.0.1"]),
    ];

    /// <summary>Creates the port over <see cref="ResolveOverrides.Parse"/>.</summary>
    public Unit1607ResolveParsing()
        : this(ResolveOverrides.Parse)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1607";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        for (int index = 0; index < Rows.Length; index++)
        {
            string? problem = FirstProblem(index, parse([Rows[index].OptionValue]));
            checks.FailUnless(problem is null, $"tests[{index}] failed. {problem}");
        }

        return Task.FromResult(checks.Result);
    }

    /// <summary>The C program's first failure line for row <paramref name="index"/>, or <see langword="null"/> when it passes.</summary>
    private static string? FirstProblem(int index, ResolveOverrides overrides)
    {
        (_, string host, int port, bool permanent, string[] expected) = Rows[index];
        IReadOnlyList<IPAddress> addresses = overrides.Find(host, port) ?? [];
        for (int slot = 0; slot < Math.Max(addresses.Count, expected.Length); slot++)
        {
            if (AddressProblem(index, slot, addresses, expected) is { } problem)
            {
                return problem;
            }
        }

        return addresses.Count == 0 ? null : PermanenceProblem(index, overrides, $"{host}:{port}", permanent);
    }

    /// <summary>
    /// Compares the permanence of the last entry for <paramref name="hostAndPort"/>, the one whose addresses were
    /// found, with the row's, as the C program compares the cache entry's timestamp with zero.
    /// </summary>
    private static string? PermanenceProblem(int index, ResolveOverrides overrides, string hostAndPort, bool permanent)
    {
        bool isPermanent = overrides.Entries
            .Where(entry => string.Equals($"{entry.Host}:{entry.Port}", hostAndPort, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.IsPermanent)
            .LastOrDefault(true);
        if (isPermanent == permanent)
        {
            return null;
        }

        return permanent
            ? $"the timestamp is not zero but tests[{index}].permanent is TRUE"
            : $"the timestamp is zero but tests[{index}].permanent is FALSE";
    }

    /// <summary>Compares the address in <paramref name="slot"/> of the retrieved and the expected list, as the C loop's body does.</summary>
    private static string? AddressProblem(int index, int slot, IReadOnlyList<IPAddress> addresses, string[] expected)
    {
        if (slot >= addresses.Count)
        {
            return $"the retrieved addr is NULL but tests[{index}].address[{slot}] is {expected[slot]}.";
        }

        string retrieved = addresses[slot].ToString();
        if (slot >= expected.Length)
        {
            return $"the retrieved addr is {retrieved} but tests[{index}].address[{slot}] is NULL.";
        }

        return string.Equals(retrieved, expected[slot], StringComparison.OrdinalIgnoreCase)
            ? null
            : $"the retrieved addr {retrieved} is not equal to tests[{index}].address[{slot}] {expected[slot]}.";
    }
}
