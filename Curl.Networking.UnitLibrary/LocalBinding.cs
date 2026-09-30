namespace Curl.Networking;

/// <summary>
/// What the local end of every TCP connection is bound to before it connects (<c>--interface</c>,
/// <c>--local-port</c>), as libcurl's <c>bindlocal</c> binds it: the address of the interface
/// <see cref="InterfaceName" /> names, else the address <see cref="HostName" /> resolves to, else the
/// unspecified address, on the first port of <see cref="FirstPort" /> to
/// <see cref="FirstPort" /> + <see cref="PortCount" /> - 1 that binds.
/// </summary>
/// <remarks>
/// A plain <c>--interface</c> name sets both <see cref="InterfaceName" /> and <see cref="HostName" />,
/// as libcurl tries it as an interface and then as a host name; <c>if!name</c> sets
/// <see cref="InterfaceName" /> only, <c>host!name</c> <see cref="HostName" /> only, and
/// <c>ifhost!interface!host</c> <see cref="DeviceName" /> and <see cref="HostName" />.
/// </remarks>
/// <param name="InterfaceName">An interface whose address to bind; <see langword="null" /> for none.</param>
/// <param name="HostName">
/// A host name or address to bind, tried when <see cref="InterfaceName" /> names no interface;
/// <see langword="null" /> for none.
/// </param>
/// <param name="DeviceName">
/// The interface part of <c>ifhost!</c>, which libcurl refuses at connect when it is longer than
/// <see cref="LongestDeviceName" /> characters; <see langword="null" /> for none.
/// </param>
/// <param name="FirstPort">The first local port tried, 0 for any.</param>
/// <param name="PortCount">How many ports from <see cref="FirstPort" /> are tried, at least 1.</param>
public sealed record LocalBinding(
    string? InterfaceName,
    string? HostName,
    string? DeviceName,
    int FirstPort,
    int PortCount)
{
    /// <summary>The longest <c>ifhost!</c> interface part libcurl binds; one character more is exit 43.</summary>
    public const int LongestDeviceName = 254;
}
