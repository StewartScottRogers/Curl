using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.GrpcWeb;

/// <summary>
/// The <c>--grpc-web</c> extension (ADR-1132): one unary gRPC-Web call over HTTP/1.1 or HTTP/2, the <c>-d</c> data
/// sent as the request message and the reply's messages written out, its <c>grpc-status</c> checked.
/// <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class GrpcWebExtension
{
    /// <summary>Gets the <c>--grpc-web</c> flag.</summary>
    public static ExtensionOption Option { get; } = new(
        "grpc-web",
        ExtensionOptionArgument.None,
        "Call a gRPC-Web method: send the -d data as one application/grpc-web+proto message, write the reply's messages and fail on a non-zero grpc-status");

    /// <summary>Gets what <c>curl --extensions-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "grpc-web",
        "Calls gRPC-Web services over HTTP/1.1 or HTTP/2, framing the request message and checking the reply's grpc-status.",
        [],
        [Option],
        new Uri("https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-WEB.md"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--grpc-web</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--grpc-web</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IsGiven(Option) ? new GrpcWebTransferHook() : null;
    }
}
