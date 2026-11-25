using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that a typed <c>-d</c> body reaches the <c>xmpp</c> and <c>xmpps</c> handler as UTF-8, on the Windows
/// build too, whose other schemes get it in the ANSI code page (BL-5915, ADR-1360). The handler sends those
/// bytes as the message body (<c>XmppUploadTests</c>).
/// </summary>
[TestClass]
public sealed class XmppPostDataUtf8Tests
{
    [TestMethod]
    [DataRow("xmpp", true)]
    [DataRow("xmpps", true)]
    [DataRow("XMPP", true)]
    [DataRow("xmpp", false)]
    public async Task RunAsync_TypedNonAsciiData_ReachesTheXmppHandlerAsUtf8(string scheme, bool runsOnWindows)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        PhysicalFileSystem fileSystem = new();
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath(scheme.ToLowerInvariant());

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler]), []),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows,
                TerminalColumns.Default)
            .RunAsync(["-q", "-sS", "-d", "café", $"{scheme}://example.com/romeo@example.net"]);

        Assert.AreEqual(0, exitCode, Encoding.UTF8.GetString(standardError.ToArray()));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("café"), handler.Contexts[0].PostData!.Value.ToArray());
    }

    [TestMethod]
    [DataRow("http://example.com/")]
    [DataRow("mqtt://example.com/topic")]
    public void TransferSettingsOf_CurlsOwnScheme_KeepsTheBytesCurlSends(string url)
    {
        CommandLineOptions options = Parse("-d", "café", url);

        ReadOnlyMemory<byte>? body = EasyOptionsFromCommandLine.TransferSettingsOf(options, CurlUrl.Parse(url)).PostData;

        Assert.AreEqual(options.PostData, body);
    }

    [TestMethod]
    public void TransferSettingsOf_XmppUrlUnderGet_HasNoBody()
    {
        CommandLineOptions options = Parse("-G", "-d", "a=1", "xmpp://example.com/a@b");

        Assert.IsNull(EasyOptionsFromCommandLine.TransferSettingsOf(options, CurlUrl.Parse("xmpp://example.com/a@b")).PostData);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            arguments, _ => true, new EmptyPasswordPrompt(), DiskDataFileReader.ForProcess, isWindows: true);

        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }
}
