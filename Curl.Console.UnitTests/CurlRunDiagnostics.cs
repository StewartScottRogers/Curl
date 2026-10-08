using System.Text;

using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Writes the diagnostic lines the composition tests that run the whole command share (BL-1589): the
/// command line as Arrange, and the exit code, standard output, standard error and the bytes the
/// client wrote as Act. Line endings are written as <c>\n</c> so the output is the same on every
/// operating system.
/// </summary>
internal static class CurlRunDiagnostics
{
    /// <summary>Writes the command line, its arguments joined by spaces.</summary>
    public static void ArrangeCommandLine(this TestDiagnostics diagnostics, IEnumerable<string> arguments) =>
        diagnostics.Arrange("command line", string.Join(' ', arguments));

    /// <summary>Writes the exit code, standard output and standard error of a run.</summary>
    public static void ActRun(this TestDiagnostics diagnostics, int exitCode, string standardOutput, string standardError)
    {
        diagnostics.Act("exit code", exitCode);
        diagnostics.Act("stdout", standardOutput.ReplaceLineEndings("\n"));
        diagnostics.Act("stderr", standardError.ReplaceLineEndings("\n"));
    }

    /// <summary>Writes the bytes a scripted connector was sent, as <c>BYTES</c>.</summary>
    public static void ActWritten(this TestDiagnostics diagnostics, ScriptedConnector connector) =>
        diagnostics.Bytes("request bytes", connector.Written);

    /// <summary>Writes the first difference between the expected and the written request.</summary>
    public static void AssertWritten(this TestDiagnostics diagnostics, string expected, ScriptedConnector connector) =>
        diagnostics.Diff("request", expected, Encoding.ASCII.GetString(connector.Written));
}
