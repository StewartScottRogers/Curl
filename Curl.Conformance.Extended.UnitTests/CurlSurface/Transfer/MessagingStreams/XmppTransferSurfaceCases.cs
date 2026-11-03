using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Xmpp;
using static Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.MessagingStreams.TransferSurfaceCaseRun;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.MessagingStreams;

/// <summary>
/// curl's generic transfer options for <c>xmpp://</c> and <c>xmpps://</c> (TLS from the first byte on port 5223, curl
/// run with <c>-k</c>): each check logs in anonymously to an <see cref="XmppReferenceServer" /> and sends the disco#info
/// query a URL with no data sends to <c>bob@localhost</c>, which the server answers with an empty disco#info result or
/// with an <c>item-not-found</c> stanza error, and pins exit code, standard output and standard error. Expected values
/// come from curl 8.22.0's manual page and, for a stanza error, from ADR-1068 (exit 8).
/// </summary>
[TestClass]
public sealed class XmppTransferSurfaceCases
{
    private const string ServerErrorMessage = "XMPP stanza error: item-not-found";

    private const string Reply = "<query xmlns=\"http://jabber.org/protocol/disco#info\" />\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-Xmpp-1")]
    [SpecRequirement("CurlSurface:Verbose-Xmpps-1")]
    [DataRow("xmpp", "CurlSurface:Verbose-Xmpp-1")]
    [DataRow("xmpps", "CurlSurface:Verbose-Xmpps-1")]
    public Task Verbose_SuccessfulQuery_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Result), "-sS", "-v", Url(scheme));

                AssertVerboseRun(result, requirement, "manual: -v", Reply, VerboseLines(scheme));
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-Xmpp-1")]
    [SpecRequirement("CurlSurface:Output-Xmpps-1")]
    [DataRow("xmpp", "CurlSurface:Output-Xmpp-1")]
    [DataRow("xmpps", "CurlSurface:Output-Xmpps-1")]
    public Task Output_File_WritesTheReplyToTheFileAndNothingToStandardOutput(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                string directory = CreateTemporaryDirectory();
                try
                {
                    string file = Path.Combine(directory, "reply.txt");

                    CurlRunResult result = await RunAsync(Route(scheme, Result), "-sS", "-o", file, Url(scheme));

                    AssertRun(result, requirement, "manual: -o", 0, string.Empty, string.Empty);
                    Assert.AreEqual(Reply, File.ReadAllText(file), $"{requirement}: file");
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:RemoteName-Xmpp-1")]
    [SpecRequirement("CurlSurface:RemoteName-Xmpps-1")]
    [DataRow("xmpp", "CurlSurface:RemoteName-Xmpp-1")]
    [DataRow("xmpps", "CurlSurface:RemoteName-Xmpps-1")]
    public Task RemoteName_WritesTheReplyToAFileNamedForTheUrlsLastSegment(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                string directory = CreateTemporaryDirectory();
                try
                {
                    CurlRunResult result = await RunAsync(Route(scheme, Result), "-sS", "--output-dir", directory, "-O", Url(scheme));

                    AssertRun(result, requirement, "manual: -O", 0, string.Empty, string.Empty);
                    Assert.AreEqual(Reply, File.ReadAllText(Path.Combine(directory, "bob@localhost")), $"{requirement}: file");
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Xmpp-1")]
    [SpecRequirement("CurlSurface:WriteOut-Xmpps-1")]
    [DataRow("xmpp", "CurlSurface:WriteOut-Xmpp-1")]
    [DataRow("xmpps", "CurlSurface:WriteOut-Xmpps-1")]
    public Task WriteOut_SuccessfulQuery_WritesEveryVariableAfterTheReply(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Result), "-sS", "-w", WriteOutFormat, Url(scheme));

                AssertRun(result, requirement, "manual: -w", 0, $"{Reply}0||{Url(scheme)}|{Url(scheme)}|{scheme}|127.0.0.1|{Port(scheme)}|{Reply.Length}|0.000000", string.Empty);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Xmpp-2")]
    [SpecRequirement("CurlSurface:WriteOut-Xmpps-2")]
    [DataRow("xmpp", "CurlSurface:WriteOut-Xmpp-2")]
    [DataRow("xmpps", "CurlSurface:WriteOut-Xmpps-2")]
    public Task WriteOut_StanzaError_WritesExitCode8AndTheErrorMessage(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Error), "-sS", "-w", WriteOutFormat, Url(scheme));

                AssertRun(
                    result,
                    requirement,
                    "manual: -w; ADR-1068: a stanza error is exit 8",
                    8,
                    $"8|{ServerErrorMessage}|{Url(scheme)}|{Url(scheme)}|{scheme}|127.0.0.1|{Port(scheme)}|0|0.000000",
                    $"curl: (8) {ServerErrorMessage}\n");
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Xmpp-1")]
    [SpecRequirement("CurlSurface:Fail-Xmpps-1")]
    [SpecRequirement("CurlSurface:FailWithBody-Xmpp-1")]
    [SpecRequirement("CurlSurface:FailWithBody-Xmpps-1")]
    [DataRow("xmpp", "--fail", "CurlSurface:Fail-Xmpp-1")]
    [DataRow("xmpps", "--fail", "CurlSurface:Fail-Xmpps-1")]
    [DataRow("xmpp", "--fail-with-body", "CurlSurface:FailWithBody-Xmpp-1")]
    [DataRow("xmpps", "--fail-with-body", "CurlSurface:FailWithBody-Xmpps-1")]
    public Task Fail_StanzaError_StaysExit8AsXmppMapsNoServerErrorTo22(string scheme, string option, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Error), "-sS", option, Url(scheme));

                AssertRun(result, requirement, "manual: --fail is for HTTP; ADR-1068: a stanza error is exit 8", 8, string.Empty, $"curl: (8) {ServerErrorMessage}\n");
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Xmpp-2")]
    [SpecRequirement("CurlSurface:Fail-Xmpps-2")]
    [DataRow("xmpp", "CurlSurface:Fail-Xmpp-2")]
    [DataRow("xmpps", "CurlSurface:Fail-Xmpps-2")]
    public Task Fail_SuccessfulQuery_WritesTheReplyAndExits0(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Result), "-sS", "--fail", Url(scheme));

                AssertRun(result, requirement, "manual: --fail", 0, Reply, string.Empty);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-Xmpp-1")]
    [SpecRequirement("CurlSurface:MaxTime-Xmpps-1")]
    [DataRow("xmpp", "CurlSurface:MaxTime-Xmpp-1")]
    [DataRow("xmpps", "CurlSurface:MaxTime-Xmpps-1")]
    public Task MaxTime_ServerNeverAnswers_Exits28AtTheDeadline(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunPastMaxTimeAsync(Route(scheme, Result), 5, Url(scheme), TestContext.CancellationToken);

                AssertRun(result, requirement, "manual: -m, EXIT CODES 28", 28, string.Empty, "curl: (28) Operation timed out after 5000 milliseconds with 0 bytes received\n");
            });

    private static IReadOnlyList<string> Result(System.Xml.Linq.XElement query) =>
        ["<iq type='result' id='disco' from='bob@localhost'><query xmlns='http://jabber.org/protocol/disco#info'/></iq>"];

    private static IReadOnlyList<string> Error(System.Xml.Linq.XElement query) =>
        ["<iq type='error' id='disco'><error type='cancel'><item-not-found xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/></error></iq>"];

    private static string Url(string scheme) => $"{scheme}://localhost/bob@localhost";

    private static int Port(string scheme) => scheme == "xmpps" ? 5223 : 5222;

    // The connection line every transfer's -v writes (curl 8.22.0 measured with Record-CurlExchange.ps1: "* Established
    // connection to localhost (127.0.0.1 port N) from ..."), and for xmpps a TLS line after it: the Schannel build's
    // "* schannel: ..." on Windows, the OpenSSL build's "* SSL connection using TLSv1..." elsewhere.
    private static string[] VerboseLines(string scheme) => scheme == "xmpps"
        ? ["* Established connection to localhost (127.0.0.1 port 5223) from ", OperatingSystem.IsWindows() ? "* schannel: " : "* SSL connection using TLSv1."]
        : ["* Established connection to localhost (127.0.0.1 port 5222) from "];

    private static TransferSurfaceRoute Route(string scheme, Func<System.Xml.Linq.XElement, IReadOnlyList<string>> answer)
    {
        Func<IReferenceServer> server = () => new XmppReferenceServer(new XmppReferenceScript { Answer = answer });
        return scheme == "xmpps" ? TransferSurfaceRoute.Tls(5223, server) : TransferSurfaceRoute.Tcp(5222, server);
    }
}
