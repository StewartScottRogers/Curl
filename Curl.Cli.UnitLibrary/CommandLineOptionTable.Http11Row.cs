using Curl.Protocol.Abstractions;

namespace Curl.Cli;

public static partial class CommandLineOptionTable
{
    /// <summary>The row for <c>--http1.1</c>; it lives in its own file so a change to it touches nothing else (BL-5529).</summary>
    private static CommandLineOption Http11Row() =>
        CommandLineOption.Flag("http1.1", null, options => options.SelectHttpVersion(RequestedHttpVersion.Http11));
}
