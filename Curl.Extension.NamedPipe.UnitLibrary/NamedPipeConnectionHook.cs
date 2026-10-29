using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.NamedPipe;

/// <summary>The <c>--npipe</c> connection hook: curl's connector is set aside for one that opens the named pipe.</summary>
/// <param name="name">The pipe <c>--npipe</c> named.</param>
/// <param name="opener">Opens the pipe.</param>
internal sealed class NamedPipeConnectionHook(NamedPipeName name, INamedPipeOpener opener) : IConnectionHook
{
    /// <inheritdoc />
    public ExtensionOption Option => NamedPipeExtension.Option;

    /// <summary>Gets the pipe the hook connects to.</summary>
    public NamedPipeName Name => name;

    /// <inheritdoc />
    public IConnector ConnectorOver(IConnector curlConnector) => new NamedPipeConnector(name, opener);
}
