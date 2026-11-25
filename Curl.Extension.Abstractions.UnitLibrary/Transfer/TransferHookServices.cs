namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// What the host gives a transfer hook to make its own requests before or around the transfer (ADR-1039):
/// curl's connector, which opens TLS when <see cref="ConnectTarget.UseTls" /> asks and goes through the
/// proxy the target names, and a listener for a port on the loopback address, such as an OAuth 2.0
/// redirect URI's.
/// </summary>
/// <param name="connector">The option group's connector, the one curl's own handlers connect through.</param>
/// <param name="listener">Listens on a local port for connections made to the hook.</param>
public sealed class TransferHookServices(IConnector connector, IConnectionListener listener)
{
    /// <summary>Gets the option group's connector, the one curl's own handlers connect through.</summary>
    public IConnector Connector { get; } = connector ?? throw new ArgumentNullException(nameof(connector));

    /// <summary>Gets the listener for a local port connections are made to the hook on.</summary>
    public IConnectionListener Listener { get; } = listener ?? throw new ArgumentNullException(nameof(listener));

    /// <summary>
    /// Gets the hook that lends its DPoP key for binding tokens, or <see langword="null" /> when none of the transfer's
    /// hooks is one.
    /// </summary>
    public IDpopKeySource? DpopKeySource { get; init; }

    /// <summary>Gives these services with the DPoP key source among <paramref name="hooks" />, the first when there are several.</summary>
    /// <param name="hooks">The transfer's hooks.</param>
    /// <returns>The services the hooks are given: these themselves when no hook holds a key.</returns>
    internal TransferHookServices WithDpopKeySourceFrom(IReadOnlyList<ITransferHook> hooks) =>
        hooks.OfType<IDpopKeySource>().FirstOrDefault() is { } source ? new(Connector, Listener) { DpopKeySource = source } : this;
}
