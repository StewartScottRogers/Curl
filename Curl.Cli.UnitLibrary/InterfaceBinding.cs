namespace Curl.Cli;

/// <summary>
/// What a <c>--interface</c> value asks the connection's local end to bind to, split by libcurl's prefixes
/// as <c>CURLOPT_INTERFACE</c> splits it: <c>if!name</c> an interface name only, <c>host!name</c> a host name
/// or address only, <c>ifhost!interface!host</c> both, and a value with no prefix a name libcurl tries as an
/// interface, then as a host name or address. The prefixes are case-sensitive, so <c>IF!x</c> is a plain name.
/// </summary>
public sealed class InterfaceBinding
{
    /// <summary>The longest plain or <c>if!</c> name libcurl accepts; one character more fails with exit 43.</summary>
    private const int MaximumNameLength = 254;

    private const string InterfacePrefix = "if!";
    private const string HostPrefix = "host!";
    private const string InterfaceAndHostPrefix = "ifhost!";

    private InterfaceBinding(string value, string? interfaceOrHostName, string? interfaceName, string? hostName, bool isMalformed)
    {
        Value = value;
        InterfaceOrHostName = interfaceOrHostName;
        InterfaceName = interfaceName;
        HostName = hostName;
        IsMalformed = isMalformed;
    }

    /// <summary>The <c>--interface</c> value, verbatim.</summary>
    public string Value { get; }

    /// <summary>A value with no prefix, tried as an interface name and then as a host name or address; otherwise <see langword="null"/>.</summary>
    public string? InterfaceOrHostName { get; }

    /// <summary>The interface name after <c>if!</c>, or before the second <c>!</c> of <c>ifhost!</c> (possibly empty); otherwise <see langword="null"/>.</summary>
    public string? InterfaceName { get; }

    /// <summary>The host name or address after <c>host!</c>, or after the second <c>!</c> of <c>ifhost!</c>; otherwise <see langword="null"/>.</summary>
    public string? HostName { get; }

    /// <summary>
    /// <see langword="true"/> when libcurl refuses the value when curl sets it, before any connection: an empty
    /// name after <c>if!</c> or <c>host!</c>, an <c>ifhost!</c> value with no second <c>!</c> or nothing after it,
    /// or a name longer than 254 characters with no prefix or after <c>if!</c>. curl 8.21.0 then fails the transfer with exit 43 and
    /// <c>curl: (43) setopt 0x274e got bad argument</c> (measured on Windows, 2026-09-28, BL-599 Notes); the
    /// parser accepts the value, as curl's does. The name parts are left <see langword="null"/>.
    /// </summary>
    public bool IsMalformed { get; }

    /// <summary>Splits a <c>--interface</c> value by its prefix.</summary>
    /// <param name="value">The value as given; curl refuses an empty one as blank before this is reached.</param>
    /// <returns>The binding the value names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static InterfaceBinding Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.StartsWith(InterfacePrefix, StringComparison.Ordinal))
        {
            return ParseInterfaceOnly(value);
        }

        if (value.StartsWith(HostPrefix, StringComparison.Ordinal))
        {
            return ParseHostOnly(value);
        }

        if (value.StartsWith(InterfaceAndHostPrefix, StringComparison.Ordinal))
        {
            return ParseInterfaceAndHost(value);
        }

        return value.Length <= MaximumNameLength ? new(value, value, null, null, false) : Malformed(value);
    }

    /// <summary>Reads <c>if!name</c>: the name must be 1 to 254 characters.</summary>
    private static InterfaceBinding ParseInterfaceOnly(string value)
    {
        string name = value[InterfacePrefix.Length..];
        return name.Length is > 0 and <= MaximumNameLength ? new(value, null, name, null, false) : Malformed(value);
    }

    /// <summary>Reads <c>host!name</c>: the name must not be empty, and may be of any length.</summary>
    private static InterfaceBinding ParseHostOnly(string value)
    {
        string name = value[HostPrefix.Length..];
        return name.Length > 0 ? new(value, null, null, name, false) : Malformed(value);
    }

    /// <summary>
    /// Splits <c>ifhost!interface!host</c> at its second <c>!</c>. The interface may be empty, the host may not.
    /// Neither part is length-checked here: libcurl refuses an interface part past 254 characters only when it
    /// connects, with a different message.
    /// </summary>
    private static InterfaceBinding ParseInterfaceAndHost(string value)
    {
        string rest = value[InterfaceAndHostPrefix.Length..];
        int separator = rest.IndexOf('!', StringComparison.Ordinal);
        if (separator < 0 || separator == rest.Length - 1)
        {
            return Malformed(value);
        }

        return new(value, null, rest[..separator], rest[(separator + 1)..], false);
    }

    private static InterfaceBinding Malformed(string value) => new(value, null, null, null, true);
}
