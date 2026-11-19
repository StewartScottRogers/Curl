namespace Curl.Cli;

public static partial class CommandLineOptionTable
{
    /// <summary>
    /// The rows that pick the HTTP version, in table order: <c>--http1.0</c> (<c>-0</c>), <c>--http1.1</c>, <c>--http2</c>
    /// and <c>--http2-prior-knowledge</c>. Each row lives in its own file (<c>CommandLineOptionTable.Http10Row.cs</c> and
    /// its siblings) so a change to one of them touches only that file (BL-3433, BL-5529); <see cref="RowsInTableOrder"/>
    /// spreads them in after <c>--ipfs-gateway</c>.
    /// </summary>
    private static CommandLineOption[] HttpVersionRows() =>
    [
        Http10Row(),
        Http11Row(),
        Http2Row(),
        Http2PriorKnowledgeRow(),
    ];
}
