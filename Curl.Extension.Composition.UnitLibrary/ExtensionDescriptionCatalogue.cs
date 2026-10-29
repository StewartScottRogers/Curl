using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.Composition;

/// <summary>
/// Every extension's description, one entry per line (ADR-1008). Each extension adds its own line and
/// nothing else here; the file is marked <c>merge=union</c> in <c>.gitattributes</c> and listed in
/// <c>Tasks/SharedListFiles.txt</c>, so lanes adding different extensions do not overlap.
/// </summary>
internal static class ExtensionDescriptionCatalogue
{
    /// <summary>Gets the descriptions, in the order <c>--extensions</c> lists them.</summary>
    internal static IReadOnlyList<ExtensionDescription> Entries { get; } =
    [
        Curl.Extension.NamedPipe.NamedPipeExtension.Description,
        GraphQl.GraphQlOptions.Description,
        Curl.Extension.JsonRpc.JsonRpcExtension.Description,
        SocketIo.SocketIoExtension.Description,
        Coap.Client.CoapExtension.Description,
    ];
}
