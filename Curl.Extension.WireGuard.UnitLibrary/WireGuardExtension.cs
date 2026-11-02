using System.Security.Cryptography;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Resolution;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.Tunnel;

namespace Curl.Extension.WireGuard;

/// <summary>The <c>--wireguard</c> extension's option, description and connection hook activator (ADR-1159).</summary>
public static class WireGuardExtension
{
    /// <summary>Gets <c>--wireguard &lt;conf&gt;</c>.</summary>
    public static ExtensionOption Option { get; } = new("wireguard", ExtensionOptionArgument.Value, "Run every connection through a WireGuard tunnel set up from this wg-quick configuration file");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "wireguard",
        "Runs every transfer through a user-space WireGuard tunnel and TCP/IP stack set up from a wg-quick configuration file, with no system interface or administrator rights.",
        [],
        [Option],
        new Uri("https://www.wireguard.com/papers/wireguard.pdf"));

    /// <summary>Gives the connection hook for <paramref name="values" />, reading the configuration from disk and opening system UDP sockets.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--wireguard</c>.</returns>
    /// <exception cref="ArgumentException">The configuration cannot be read, is not a wg-quick file or names no reachable peer.</exception>
    public static IConnectionHook? Activate(ExtensionOptionValues values) =>
        Activate(values, File.ReadAllText, new SystemWireGuardUdpOpener(), TimeProvider.System, RandomNumberGenerator.Create());

    /// <summary>Gives the connection hook for <paramref name="values" />.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <param name="readFile">Reads the configuration file's text.</param>
    /// <param name="opener">Opens the UDP socket to the peer.</param>
    /// <param name="timeProvider">The clock the handshake and TCP timers run on.</param>
    /// <param name="random">The randomness for the handshake.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--wireguard</c>.</returns>
    /// <exception cref="ArgumentException">The configuration cannot be read, is not a wg-quick file or names no reachable peer.</exception>
    internal static IConnectionHook? Activate(ExtensionOptionValues values, Func<string, string> readFile, IWireGuardUdpOpener opener, TimeProvider timeProvider, RandomNumberGenerator random) =>
        ConfigurationOf(values, readFile) is { } configuration ? new WireGuardConnectionHook(configuration, opener, timeProvider, random) : null;

    /// <summary>Gives the resolver hook for <paramref name="values" />, reading the configuration from disk and opening system UDP sockets (BL-4392).</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--wireguard</c>.</returns>
    /// <exception cref="ArgumentException">The configuration cannot be read, is not a wg-quick file or names no reachable peer.</exception>
    public static IResolverHook? ActivateResolver(ExtensionOptionValues values) =>
        ActivateResolver(values, File.ReadAllText, new SystemWireGuardUdpOpener(), TimeProvider.System, RandomNumberGenerator.Create());

    /// <summary>Gives the resolver hook for <paramref name="values" />, which looks host names up through the tunnel's DNS.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <param name="readFile">Reads the configuration file's text.</param>
    /// <param name="opener">Opens the UDP socket to the peer.</param>
    /// <param name="timeProvider">The clock the handshake and TCP timers run on.</param>
    /// <param name="random">The randomness for the handshake.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--wireguard</c>.</returns>
    /// <exception cref="ArgumentException">The configuration cannot be read, is not a wg-quick file or names no reachable peer.</exception>
    internal static IResolverHook? ActivateResolver(ExtensionOptionValues values, Func<string, string> readFile, IWireGuardUdpOpener opener, TimeProvider timeProvider, RandomNumberGenerator random) =>
        ConfigurationOf(values, readFile) is { } configuration ? new WireGuardResolverHook(configuration, opener, timeProvider, random) : null;

    private static WireGuardConfiguration? ConfigurationOf(ExtensionOptionValues values, Func<string, string> readFile)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? path = values.ValueOf(Option);
        if (path is null)
        {
            return null;
        }

        WireGuardConfiguration configuration = Read(path, readFile);
        return configuration.Peers.Any(peer => peer.Endpoint is not null)
            ? configuration
            : throw new ArgumentException($"--wireguard needs a [Peer] with an Endpoint in '{path}'", nameof(values));
    }
    private static WireGuardConfiguration Read(string path, Func<string, string> readFile)
    {
        string text;
        try
        {
            text = readFile(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException($"--wireguard cannot read '{path}': {exception.Message}", "values", exception);
        }

        try
        {
            return WireGuardConfigurationParser.Parse(text);
        }
        catch (WireGuardConfigurationException exception)
        {
            throw new ArgumentException($"--wireguard '{path}' {exception.Message}", "values", exception);
        }
    }
}
