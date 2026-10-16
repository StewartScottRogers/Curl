using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;

namespace Curl.Api;

/// <summary>
/// The checks the transport option records run as each member is set, so an invalid value throws
/// when the record is built, before any transfer starts. Each check returns the value it accepted.
/// </summary>
internal static class TransportOptionGuard
{
    /// <summary>Every <see cref="SslOptions"/> bit libcurl 8.22.0 defines.</summary>
    private const SslOptions AllSslOptions = (SslOptions)((1 << 7) - 1);

    /// <summary>Every <see cref="Socks5Authentication"/> bit libcurl 8.22.0 defines.</summary>
    private const Socks5Authentication AllSocks5Authentication = Socks5Authentication.Basic | Socks5Authentication.GssApi;

    /// <summary><paramref name="value"/> when it is zero or more.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public static TimeSpan NonNegative(TimeSpan value, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero, name);
        return value;
    }

    /// <summary><paramref name="value"/> when it is zero or more, or <see cref="Timeout.InfiniteTimeSpan"/> (libcurl's -1).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is any other negative time.</exception>
    public static TimeSpan NonNegativeOrInfinite(TimeSpan value, string name) =>
        value == Timeout.InfiniteTimeSpan ? value : NonNegative(value, name);

    /// <summary><paramref name="value"/> when it lies from <paramref name="minimum"/> to <paramref name="maximum"/> inclusive.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is outside the range.</exception>
    public static long InRange(long value, long minimum, long maximum, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, minimum, name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, maximum, name);
        return value;
    }

    /// <summary><paramref name="value"/> when it is a TCP port, 0 to 65535.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is outside the range.</exception>
    public static int Port(int value, string name) => (int)InRange(value, 0, 65535, name);

    /// <summary><paramref name="value"/> when it is one of <typeparamref name="TEnum"/>'s named values.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not a named value.</exception>
    public static TEnum Defined<TEnum>(TEnum value, string name)
        where TEnum : struct, Enum =>
        Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(name, value, $"{value} is not a {typeof(TEnum).Name} value libcurl defines.");

    /// <summary><paramref name="value"/> when it sets no bit libcurl does not define.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> sets an undefined bit.</exception>
    public static SslOptions Flags(SslOptions value, string name) =>
        (value & ~AllSslOptions) == 0 ? value : throw new ArgumentOutOfRangeException(name, value, "The value sets an SSL option bit libcurl does not define.");

    /// <summary><paramref name="value"/> when it sets no bit libcurl does not define.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> sets an undefined bit.</exception>
    public static Socks5Authentication Flags(Socks5Authentication value, string name) =>
        (value & ~AllSocks5Authentication) == 0 ? value : throw new ArgumentOutOfRangeException(name, value, "The value sets a SOCKS5 authentication bit libcurl does not define.");

    /// <summary><paramref name="value"/> when it is not <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static T NotNull<T>(T value, string name)
        where T : class =>
        value ?? throw new ArgumentNullException(name);
}
