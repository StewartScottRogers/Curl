using Curl.Protocol.Abstractions;

namespace Curl.Cli;

public static partial class CommandLineOptionTable
{
    /// <summary>
    /// The rows that pick the HTTP version: <c>--http1.0</c> (<c>-0</c>), <c>--http1.1</c>, <c>--http2</c> and
    /// <c>--http2-prior-knowledge</c>. They live in their own file so a change to one of them does not touch the
    /// rest of the table (BL-3433); <see cref="RowsInTableOrder"/> spreads them in after <c>--ipfs-gateway</c>.
    /// </summary>
    private static CommandLineOption[] HttpVersionRows() =>
    [
        CommandLineOption.Flag("http1.0", '0', options => options.SelectHttpVersion(RequestedHttpVersion.Http10)),
        CommandLineOption.Flag("http1.1", null, options => options.SelectHttpVersion(RequestedHttpVersion.Http11)),
        CommandLineOption.Flag("http2", null, options => options.SelectHttpVersion(RequestedHttpVersion.Http2)),
        CommandLineOption.Flag("http2-prior-knowledge", null, options => options.SelectHttpVersion(RequestedHttpVersion.Http2PriorKnowledge)),
    ];
}
