namespace Curl.Extension.Abstractions.Connection;

/// <summary>
/// The connection hook of the extension one option group activated (ADR-1010). A host builds it from the
/// group's <see cref="ExtensionOptionValues" /> and passes <see cref="ConnectorOver(IConnector, ITlsProvider)" /> and
/// <see cref="DatagramConnectorOver" /> to the engine; with no extension option given it holds no hook and
/// gives curl's connectors back unchanged.
/// </summary>
public sealed class ConnectionHooks
{
    private ConnectionHooks(IConnectionHook? hook) => Hook = hook;

    /// <summary>Gets the hooks of an option group that activated no extension.</summary>
    public static ConnectionHooks None { get; } = new(null);

    /// <summary>Gets the activated hook, or <see langword="null" /> when none was.</summary>
    public IConnectionHook? Hook { get; }

    /// <summary>Gives the hook the extensions' activators return for <paramref name="values" />.</summary>
    /// <param name="values">The extension options the option group gave.</param>
    /// <param name="activators">One per extension: its hook when its option was given, otherwise <see langword="null" />.</param>
    /// <returns>The activated hook, or <see cref="None" />.</returns>
    /// <exception cref="ExtensionHookConflictException">Two or more extensions' options claim the transfer's connection; the message names each option.</exception>
    public static ConnectionHooks ActivatedBy(ExtensionOptionValues values, IEnumerable<Func<ExtensionOptionValues, IConnectionHook?>> activators)
    {
        IConnectionHook[] hooks = [.. activators.Select(activator => activator(values)).OfType<IConnectionHook>()];
        return hooks.Length > 1
            ? throw new ExtensionHookConflictException($"the options {string.Join(" and ", hooks.Select(hook => $"--{hook.Option.Name}"))} each supply the connection a transfer runs over; give only one of them")
            : new ConnectionHooks(hooks.SingleOrDefault());
    }

    /// <summary>Gives the connector a transfer's stream connections open through.</summary>
    /// <param name="curlConnector">The connector curl would use.</param>
    /// <returns><paramref name="curlConnector" /> itself without a hook, otherwise the hook's connector.</returns>
    public IConnector ConnectorOver(IConnector curlConnector) =>
        Hook is null ? curlConnector : Hook.ConnectorOver(curlConnector);

    /// <summary>
    /// Gives the connector a transfer's stream connections open through, handing the hook curl's TLS provider so a
    /// hook that supplies only the byte stream can layer curl's TLS over it (ADR-1055).
    /// </summary>
    /// <param name="curlConnector">The connector curl would use.</param>
    /// <param name="curlTlsProvider">The TLS provider curl built from the option group's TLS options.</param>
    /// <returns><paramref name="curlConnector" /> itself without a hook, otherwise the hook's connector.</returns>
    public IConnector ConnectorOver(IConnector curlConnector, ITlsProvider curlTlsProvider) =>
        Hook is null ? curlConnector : Hook.ConnectorOver(curlConnector, curlTlsProvider);

    /// <summary>Gives the connector a transfer's datagram sockets open through.</summary>
    /// <param name="curlDatagramConnector">The datagram connector curl would use.</param>
    /// <returns><paramref name="curlDatagramConnector" /> itself without a hook, otherwise the hook's datagram connector.</returns>
    public IDatagramConnector DatagramConnectorOver(IDatagramConnector curlDatagramConnector) =>
        Hook is null ? curlDatagramConnector : Hook.DatagramConnectorOver(curlDatagramConnector);
}
