using System.Text;

using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Writes the diagnostic lines about the command line a test parses and the bytes a scripted connector
/// was sent, as the engine's tests share them.
/// </summary>
internal static class CurlRunDiagnostics
{
    /// <summary>Writes the command line, its arguments joined by spaces.</summary>
    public static void ArrangeCommandLine(this TestDiagnostics diagnostics, IEnumerable<string> arguments) =>
        diagnostics.Arrange("command line", string.Join(' ', arguments));

    /// <summary>Writes the bytes a scripted connector was sent, as <c>BYTES</c>.</summary>
    public static void ActWritten(this TestDiagnostics diagnostics, ScriptedConnector connector) =>
        diagnostics.Bytes("request bytes", connector.Written);

    /// <summary>Writes the first difference between the expected and the written request.</summary>
    public static void AssertWritten(this TestDiagnostics diagnostics, string expected, ScriptedConnector connector) =>
        diagnostics.Diff("request", expected, Encoding.ASCII.GetString(connector.Written));
}
