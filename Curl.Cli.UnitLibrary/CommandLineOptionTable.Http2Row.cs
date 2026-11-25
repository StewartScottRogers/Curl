using Curl.Protocol.Abstractions;

namespace Curl.Cli;

public static partial class CommandLineOptionTable
{
    /// <summary>The row for <c>--http2</c>; it lives in its own file so a change to it touches nothing else (BL-5529).</summary>
    private static CommandLineOption Http2Row() =>
        CommandLineOption.Flag("http2", null, options => options.SelectHttpVersion(RequestedHttpVersion.Http2));
}
