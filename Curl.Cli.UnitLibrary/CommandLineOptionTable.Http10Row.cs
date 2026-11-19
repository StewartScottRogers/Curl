using Curl.Protocol.Abstractions;

namespace Curl.Cli;

public static partial class CommandLineOptionTable
{
    /// <summary>The row for <c>--http1.0</c> (<c>-0</c>); it lives in its own file so a change to it touches nothing else (BL-5529).</summary>
    private static CommandLineOption Http10Row() =>
        CommandLineOption.Flag("http1.0", '0', options => options.SelectHttpVersion(RequestedHttpVersion.Http10));
}
