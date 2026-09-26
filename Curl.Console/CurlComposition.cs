using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;

namespace Curl.Console;

/// <summary>
/// The composition root: builds every service the executable needs with plain constructor
/// calls. There is no container, no reflection and no assembly scanning, so native
/// ahead-of-time publishing sees every type that is used.
/// </summary>
internal static class CurlComposition
{
    /// <summary>
    /// Creates the protocol handlers the executable registers. Today that is the
    /// <c>file</c> handler only.
    /// </summary>
    /// <returns>Every registered handler.</returns>
    internal static IReadOnlyList<IProtocolHandler> CreateProtocolHandlers() =>
        [new FileProtocolHandler(new PhysicalFileSystem())];

    /// <summary>
    /// Creates the runner that parses a command line and performs its transfers against
    /// the real disk and the given standard streams.
    /// </summary>
    /// <param name="standardOutput">Where a transfer without <c>-o</c> writes its bytes.</param>
    /// <param name="standardError">Where the <c>curl: (N) message</c> lines go.</param>
    /// <returns>The runner.</returns>
    internal static CurlCommandRunner CreateRunner(Stream standardOutput, Stream standardError) =>
        new(
            new ProtocolDispatcher(CreateProtocolHandlers()),
            new PhysicalFileSystem(),
            standardOutput,
            standardError);
}
