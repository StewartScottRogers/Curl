using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Writes what a whole-transfer test arranges and gets - the transfer's URL and options, its
/// result, the bytes it wrote, the in-memory server's events and the verbose transcript - and
/// what it asserts, as <c>ARRANGE</c>, <c>ACT</c>, <c>BYTES</c>, <c>ASSERT</c> and <c>DIFF</c>
/// lines through the shared <see cref="TestDiagnostics" /> helper (BL-1629).
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    // What the last ActTransfer wrote as checked: the transcript, or the result without one.
    private string checkedOutcome = "(none)";

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // The URL, then each option the transfer was given beyond the URL and the login.
    private void ArrangeTransfer(TransferContext context)
    {
        Diagnostics.Arrange("url", context.Url.OriginalString);
        Diagnostics.Arrange("user", context.Credentials?.UserName ?? "(none)");
        if (context.Upload is MemoryStream upload)
        {
            Diagnostics.Bytes("upload", upload.ToArray());
        }

        if (context.QuoteCommands.Count > 0)
        {
            Diagnostics.Arrange("quote commands", string.Join(" | ", context.QuoteCommands));
        }

        Diagnostics.Arrange("list only", context.ListOnly);
        Diagnostics.Arrange("max file size", context.MaxFileSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)");
        Diagnostics.Arrange("range", context.RangeText ?? "(none)");
        Diagnostics.Arrange("proxy", context.Proxy?.ToString() ?? "(none)");
    }

    // The result, the bytes written, the server's events and the verbose transcript.
    private void ActTransfer(TransferResult? result, TransferContext context, InMemorySshServer server)
    {
        Diagnostics.Act("result", result?.ToString() ?? "(none)");
        if (context.Output is MemoryStream output)
        {
            Diagnostics.ActBytes("output", output.ToArray());
        }

        Diagnostics.Act("server events", string.Join(" | ", server.Events));
        checkedOutcome = result?.ToString() ?? "(none)";
        if (context.Events is TranscriptTransferEvents events)
        {
            checkedOutcome = SshAuthenticationDiagnostics.Text(string.Join(" | ", events.Transcript));
            Diagnostics.Act("transcript", checkedOutcome);
        }
    }

    // The ASSERT line before assertions that pin the transcript (or, with none, the result)
    // the last ActTransfer wrote: the expected lines are the assertion's own, so this line
    // names what they are checked against.
    private void AssertCheckedOutcomeDiagnostic() =>
        Diagnostics.Assert("checked transcript or result", "the lines the next assertion pins", checkedOutcome);

    // The ASSERT line between two texts, each escaped, then their first difference.
    private void AssertTextDiagnostic(string label, string expected, string? actual)
    {
        Diagnostics.Assert(label, SshAuthenticationDiagnostics.Text(expected), actual is null ? "(null)" : SshAuthenticationDiagnostics.Text(actual));
        Diagnostics.Diff(label, expected, actual ?? string.Empty);
    }
}
