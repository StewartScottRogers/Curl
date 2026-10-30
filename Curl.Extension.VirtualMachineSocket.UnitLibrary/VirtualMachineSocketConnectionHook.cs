using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>The <c>--vm-socket</c> connection hook: curl's connector is set aside for one that opens the vsock or Hyper-V socket.</summary>
/// <param name="address">The address <c>--vm-socket</c> named.</param>
/// <param name="opener">Opens the socket.</param>
internal sealed class VirtualMachineSocketConnectionHook(VirtualMachineSocketAddress address, IVirtualMachineSocketOpener opener) : IConnectionHook
{
    /// <inheritdoc />
    public ExtensionOption Option => VirtualMachineSocketExtension.Option;

    /// <summary>Gets the address the hook connects to.</summary>
    public VirtualMachineSocketAddress Address => address;

    /// <inheritdoc />
    public IConnector ConnectorOver(IConnector curlConnector) => new VirtualMachineSocketConnector(address, opener);

    /// <summary>
    /// Gives the connector that opens the socket and runs curl's TLS handshake over it for a TLS target, offering what
    /// <paramref name="curlConnector" /> offers through ALPN.
    /// </summary>
    /// <param name="curlConnector">The connector curl would use; set aside but for its ALPN list.</param>
    /// <param name="curlTlsProvider">The TLS provider curl built from the option group's TLS options.</param>
    /// <returns>The socket's connector.</returns>
    public IConnector ConnectorOver(IConnector curlConnector, ITlsProvider curlTlsProvider)
    {
        ArgumentNullException.ThrowIfNull(curlConnector);
        return new VirtualMachineSocketConnector(address, opener, curlTlsProvider, curlConnector.HttpOverTlsApplicationProtocols);
    }
}
