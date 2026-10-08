using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// Writes the diagnostic lines an FTP test shares (BL-1474): the URL and the scripted
/// control-connection replies it runs against, and what a run left behind - the exit code
/// and its error text, the commands sent on the control connection and the bytes written
/// to the output. Each method writes through <see cref="TestDiagnostics" />, so its lines
/// count on the test's END line.
/// </summary>
internal static class FtpDiagnostics
{
    /// <summary>Writes ARRANGE lines for the URL and the server's scripted replies.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="url">The URL the transfer runs against.</param>
    /// <param name="replies">The control connection's scripted replies, or null for none.</param>
    /// <param name="data">The data connection's scripted bytes, or null for none.</param>
    public static void ArrangeFtp(this TestDiagnostics diagnostics, string url, string? replies = null, string? data = null)
    {
        diagnostics.Arrange("url", url);
        if (replies is not null)
        {
            diagnostics.Arrange("control replies", Escape(replies));
        }

        if (data is not null)
        {
            diagnostics.Bytes("data connection script", Encoding.Latin1.GetBytes(data));
        }
    }

    /// <summary>Writes ACT lines for a transfer result: its exit code and error text.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="result">The handler's result.</param>
    public static void ActResult(this TestDiagnostics diagnostics, TransferResult result)
    {
        diagnostics.Act("exit code", $"{result.ExitCode} ({(int)result.ExitCode})");
        diagnostics.Act("error", result.ErrorMessage ?? "(none)");
    }

    /// <summary>
    /// Writes ACT lines for a run - its exit code and error text - and BYTES lines for the
    /// commands sent on the control connection and the bytes written to the output.
    /// </summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="run">The run.</param>
    public static void ActRun(this TestDiagnostics diagnostics, FtpRun run)
    {
        diagnostics.ActResult(run.Result);
        diagnostics.Act("control commands sent", Escape(run.Sent));
        diagnostics.Bytes("data connection sent", run.Data.Sent);
        if (run.Output is MemoryStream output)
        {
            diagnostics.Bytes("output", output.ToArray());
        }
    }

    /// <summary>Writes a DIFF line for the commands a run sent against the expected ones.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The expected control-connection commands.</param>
    /// <param name="actual">The commands sent.</param>
    public static void DiffSent(this TestDiagnostics diagnostics, string expected, string actual) =>
        diagnostics.Diff("control commands sent", expected, actual);

    /// <summary>Shows CR and LF as <c>\r</c> and <c>\n</c> so a dialogue stays on one line.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with its line breaks escaped.</returns>
    public static string Escape(string text) => text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}
