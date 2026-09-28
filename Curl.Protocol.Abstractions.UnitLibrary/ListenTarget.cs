using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="IConnectionListener" /> is asked to bind: an address and the range of
/// ports it may use, <c>0</c> to <c>0</c> for any free port (ADR-0102).
/// </summary>
/// <param name="Address">The local address to bind, such as the control connection's own.</param>
/// <param name="LowPort">The lowest port that may be bound, from 0 to 65535.</param>
/// <param name="HighPort">
/// The highest port that may be bound, from 0 to 65535 and not below <paramref name="LowPort" />.
/// </param>
/// <remarks>
/// The members are validated when the target is built and have no <c>init</c> accessor, so
/// a <c>with</c> expression cannot produce a target that skipped the checks.
/// </remarks>
/// <exception cref="ArgumentNullException"><paramref name="Address" /> is <see langword="null" />.</exception>
/// <exception cref="ArgumentOutOfRangeException">
/// <paramref name="LowPort" /> or <paramref name="HighPort" /> is outside 0 to 65535, or
/// <paramref name="LowPort" /> is greater than <paramref name="HighPort" />.
/// </exception>
public sealed record ListenTarget(IPAddress Address, int LowPort, int HighPort)
{
    /// <summary>
    /// Gets the local address to bind.
    /// </summary>
    public IPAddress Address { get; } = RequireAddress(Address);

    /// <summary>
    /// Gets the lowest port that may be bound, from 0 to 65535.
    /// </summary>
    public int LowPort { get; } = RequirePort(LowPort, nameof(LowPort));

    /// <summary>
    /// Gets the highest port that may be bound, from <see cref="LowPort" /> to 65535.
    /// </summary>
    public int HighPort { get; } = RequireHighPort(LowPort, HighPort);

    private static IPAddress RequireAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address, nameof(Address));

        return address;
    }

    private static int RequirePort(int port, string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port, name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535, name);

        return port;
    }

    private static int RequireHighPort(int lowPort, int highPort)
    {
        RequirePort(highPort, nameof(HighPort));
        ArgumentOutOfRangeException.ThrowIfLessThan(highPort, lowPort, nameof(HighPort));

        return highPort;
    }
}
