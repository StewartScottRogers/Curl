using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Writes what an SSH user-authentication test arranges and gets - the server's scripted
/// messages and the client's messages, each as its decoded message number and its bytes, the
/// failure's exit code and text, and the <c>-v</c> lines - as <c>ARRANGE</c>, <c>ACT</c>,
/// <c>BYTES</c>, <c>ASSERT</c> and <c>DIFF</c> lines through the shared
/// <see cref="TestDiagnostics" /> helper (BL-1622).
/// </summary>
internal static class SshAuthenticationDiagnostics
{
    /// <summary>The most messages of one list written as <c>BYTES</c> lines; the rest are counted.</summary>
    private const int MessageCap = 12;

    /// <summary>The most characters of one text value written; the rest are counted.</summary>
    private const int TextCap = 300;

    /// <summary>Writes a list of SSH messages the test scripts: their names, then each one's bytes.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What the messages are.</param>
    /// <param name="payloads">The messages' payloads.</param>
    public static void ArrangeMessages(this TestDiagnostics diagnostics, string label, IReadOnlyList<byte[]> payloads)
    {
        diagnostics.Arrange(label, MessageNames(payloads));
        WriteBytes(diagnostics, label, payloads);
    }

    /// <summary>Writes a list of SSH messages the test got: their names, then each one's bytes.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What the messages are.</param>
    /// <param name="payloads">The messages' payloads.</param>
    public static void ActMessages(this TestDiagnostics diagnostics, string label, IReadOnlyList<byte[]> payloads)
    {
        diagnostics.Act(label, MessageNames(payloads));
        WriteBytes(diagnostics, label, payloads);
    }

    /// <summary>Writes the exit code and text of the failure the test caught.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="failure">The failure.</param>
    public static void ActFailure(this TestDiagnostics diagnostics, SshTransferException failure)
    {
        diagnostics.Act("exit code", ExitCode(failure.ExitCode));
        diagnostics.Act("error", Text(failure.Message));
    }

    /// <summary>Writes the <c>-v</c> lines the authentication reported.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="transcript">The lines.</param>
    public static void ActLines(this TestDiagnostics diagnostics, IReadOnlyList<string> transcript) =>
        diagnostics.Act("verbose lines", Lines(transcript));

    /// <summary>Writes the ASSERT lines for a failure's exit code and text.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expectedExitCode">The expected exit code.</param>
    /// <param name="expectedMessage">The expected text.</param>
    /// <param name="failure">The failure.</param>
    public static void AssertFailure(this TestDiagnostics diagnostics, CurlExitCode expectedExitCode, string expectedMessage, SshTransferException failure)
    {
        diagnostics.Assert("exit code", ExitCode(expectedExitCode), ExitCode(failure.ExitCode));
        diagnostics.Diff("error", expectedMessage, failure.Message);
    }

    /// <summary>Writes the ASSERT line between two lists of lines, each escaped.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What is compared.</param>
    /// <param name="expected">The expected lines.</param>
    /// <param name="actual">The actual lines.</param>
    public static void AssertLines(this TestDiagnostics diagnostics, string label, IEnumerable<string> expected, IEnumerable<string> actual) =>
        diagnostics.Assert(label, Lines(expected), Lines(actual));

    /// <summary>Writes the DIFF lines between the expected client messages and those sent, one per message.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The expected payloads.</param>
    /// <param name="actual">The payloads sent.</param>
    public static void DiffMessages(this TestDiagnostics diagnostics, IReadOnlyList<byte[]> expected, IReadOnlyList<byte[]> actual)
    {
        diagnostics.Assert("client message count", expected.Count, actual.Count);
        for (int index = 0; index < Math.Min(expected.Count, actual.Count); index++)
        {
            diagnostics.Diff($"client message {index}", expected[index], actual[index]);
        }
    }

    /// <summary>Names a payload by its message number, as RFC 4250, RFC 4252, RFC 4253 and RFC 4254 do.</summary>
    /// <param name="payload">The payload.</param>
    /// <returns>For example <c>51 USERAUTH_FAILURE</c>, or <c>(empty)</c>.</returns>
    public static string MessageName(byte[] payload) =>
        payload.Length == 0 ? "(empty)" : string.Create(CultureInfo.InvariantCulture, $"{payload[0]} {MessageNumberName(payload[0])}");

    /// <summary>Formats an exit code as its name and number.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <returns>For example <c>LoginDenied (67)</c>.</returns>
    public static string ExitCode(CurlExitCode exitCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{exitCode} ({(int)exitCode})");

    /// <summary>Formats lines as one bracketed, escaped list.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>For example <c>[* SSH: user accepted with no authentication]</c>.</returns>
    public static string Lines(IEnumerable<string> lines) => "[" + string.Join(", ", lines.Select(Text)) + "]";

    /// <summary>Escapes control characters and caps the length, so one value stays on one line.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The escaped text.</returns>
    public static string Text(string text)
    {
        var builder = new StringBuilder();
        foreach (char character in text.Length > TextCap ? text[..TextCap] : text)
        {
            builder.Append(character switch
            {
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                < ' ' or (char)0x7f => string.Create(CultureInfo.InvariantCulture, $"\\x{(int)character:x2}"),
                _ => character.ToString(),
            });
        }

        return text.Length > TextCap ? $"{builder}... ({text.Length} characters)" : builder.ToString();
    }

    private static string MessageNames(IReadOnlyList<byte[]> payloads) =>
        string.Create(CultureInfo.InvariantCulture, $"{payloads.Count} messages [{string.Join(", ", payloads.Select(MessageName))}]");

    private static void WriteBytes(TestDiagnostics diagnostics, string label, IReadOnlyList<byte[]> payloads)
    {
        for (int index = 0; index < Math.Min(payloads.Count, MessageCap); index++)
        {
            diagnostics.Bytes($"{label} {index}", payloads[index]);
        }
    }

    // Numbers 60 and 61 mean different messages in each method; the name gives each reading.
    private static string MessageNumberName(byte number) => number switch
    {
        1 => "DISCONNECT",
        2 => "IGNORE",
        3 => "UNIMPLEMENTED",
        4 => "DEBUG",
        5 => "SERVICE_REQUEST",
        6 => "SERVICE_ACCEPT",
        7 => "EXT_INFO",
        20 => "KEXINIT",
        21 => "NEWKEYS",
        50 => "USERAUTH_REQUEST",
        51 => "USERAUTH_FAILURE",
        52 => "USERAUTH_SUCCESS",
        53 => "USERAUTH_BANNER",
        60 => "USERAUTH_PK_OK/PASSWD_CHANGEREQ/INFO_REQUEST",
        61 => "USERAUTH_INFO_RESPONSE",
        30 => "KEXDH_INIT/KEX_ECDH_INIT",
        31 => "KEXDH_REPLY/KEX_ECDH_REPLY",
        80 => "GLOBAL_REQUEST",
        81 => "REQUEST_SUCCESS",
        82 => "REQUEST_FAILURE",
        90 => "CHANNEL_OPEN",
        91 => "CHANNEL_OPEN_CONFIRMATION",
        92 => "CHANNEL_OPEN_FAILURE",
        93 => "CHANNEL_WINDOW_ADJUST",
        94 => "CHANNEL_DATA",
        95 => "CHANNEL_EXTENDED_DATA",
        96 => "CHANNEL_EOF",
        97 => "CHANNEL_CLOSE",
        98 => "CHANNEL_REQUEST",
        99 => "CHANNEL_SUCCESS",
        100 => "CHANNEL_FAILURE",
        _ => "unknown",
    };
}
