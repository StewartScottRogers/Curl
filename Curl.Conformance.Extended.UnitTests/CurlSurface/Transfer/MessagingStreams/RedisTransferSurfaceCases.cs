using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Redis;
using Curl.Conformance.Extended.UnitTests.Redis;
using static Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.MessagingStreams.TransferSurfaceCaseRun;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.MessagingStreams;

/// <summary>
/// curl's generic transfer options for <c>redis://</c> and <c>rediss://</c> (TLS, curl run with <c>-k</c>): each check
/// runs <c>GET k</c> against a server that answers <c>HELLO 3</c> as RESP3 does and <c>GET</c> with the bulk string
/// <c>v</c>, or with an error reply, and pins exit code, standard output and standard error. Expected values come from
/// curl 8.22.0's manual page and, for the exit code of an error reply, ADR-1079.
/// </summary>
[TestClass]
public sealed class RedisTransferSurfaceCases
{
    private const string ServerErrorText = "ERR wrong kind of value";

    private const string ServerErrorMessage = "Redis server answered GET with error: " + ServerErrorText;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-Redis-1")]
    [SpecRequirement("CurlSurface:Verbose-Rediss-1")]
    [DataRow("redis", "CurlSurface:Verbose-Redis-1")]
    [DataRow("rediss", "CurlSurface:Verbose-Rediss-1")]
    public Task Verbose_SuccessfulGet_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Value), "-sS", "-v", Url(scheme));

                AssertVerboseRun(result, requirement, "manual: -v", "v\n", VerboseLines(scheme));
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-Redis-1")]
    [SpecRequirement("CurlSurface:Output-Rediss-1")]
    [DataRow("redis", "CurlSurface:Output-Redis-1")]
    [DataRow("rediss", "CurlSurface:Output-Rediss-1")]
    public Task Output_File_WritesTheReplyToTheFileAndNothingToStandardOutput(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                string directory = CreateTemporaryDirectory();
                try
                {
                    string file = Path.Combine(directory, "reply.txt");

                    CurlRunResult result = await RunAsync(Route(scheme, Value), "-sS", "-o", file, Url(scheme));

                    AssertRun(result, requirement, "manual: -o", 0, string.Empty, string.Empty);
                    Assert.AreEqual("v\n", File.ReadAllText(file), $"{requirement}: file");
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:RemoteName-Redis-1")]
    [SpecRequirement("CurlSurface:RemoteName-Rediss-1")]
    [DataRow("redis", "CurlSurface:RemoteName-Redis-1")]
    [DataRow("rediss", "CurlSurface:RemoteName-Rediss-1")]
    public Task RemoteName_WritesTheReplyToAFileNamedForTheUrlsLastSegment(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                string directory = CreateTemporaryDirectory();
                try
                {
                    CurlRunResult result = await RunAsync(Route(scheme, Value), "-sS", "--output-dir", directory, "-O", Url(scheme));

                    AssertRun(result, requirement, "manual: -O", 0, string.Empty, string.Empty);
                    Assert.AreEqual("v\n", File.ReadAllText(Path.Combine(directory, "k")), $"{requirement}: file");
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Redis-1")]
    [SpecRequirement("CurlSurface:WriteOut-Rediss-1")]
    [DataRow("redis", "CurlSurface:WriteOut-Redis-1")]
    [DataRow("rediss", "CurlSurface:WriteOut-Rediss-1")]
    public Task WriteOut_SuccessfulGet_WritesEveryVariableAfterTheReply(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Value), "-sS", "-w", WriteOutFormat, Url(scheme));

                AssertRun(result, requirement, "manual: -w", 0, $"v\n0||{Url(scheme)}|{Url(scheme)}|{scheme}|127.0.0.1|6379|2|0.000000", string.Empty);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Redis-2")]
    [SpecRequirement("CurlSurface:WriteOut-Rediss-2")]
    [DataRow("redis", "CurlSurface:WriteOut-Redis-2")]
    [DataRow("rediss", "CurlSurface:WriteOut-Rediss-2")]
    public Task WriteOut_ErrorReply_WritesExitCode8AndTheErrorMessage(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Error), "-sS", "-w", WriteOutFormat, Url(scheme));

                AssertRun(
                    result,
                    requirement,
                    "manual: -w; ADR-1079: an error reply is exit 8",
                    8,
                    $"8|{ServerErrorMessage}|{Url(scheme)}|{Url(scheme)}|{scheme}|127.0.0.1|6379|0|0.000000",
                    $"curl: (8) {ServerErrorMessage}\n");
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Redis-1")]
    [SpecRequirement("CurlSurface:Fail-Rediss-1")]
    [SpecRequirement("CurlSurface:FailWithBody-Redis-1")]
    [SpecRequirement("CurlSurface:FailWithBody-Rediss-1")]
    [DataRow("redis", "--fail", "CurlSurface:Fail-Redis-1")]
    [DataRow("rediss", "--fail", "CurlSurface:Fail-Rediss-1")]
    [DataRow("redis", "--fail-with-body", "CurlSurface:FailWithBody-Redis-1")]
    [DataRow("rediss", "--fail-with-body", "CurlSurface:FailWithBody-Rediss-1")]
    public Task Fail_ErrorReply_StaysExit8AsRedisMapsNoServerErrorTo22(string scheme, string option, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Error), "-sS", option, Url(scheme));

                AssertRun(result, requirement, "manual: --fail is for HTTP; ADR-1079: an error reply is exit 8", 8, string.Empty, $"curl: (8) {ServerErrorMessage}\n");
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Redis-2")]
    [SpecRequirement("CurlSurface:Fail-Rediss-2")]
    [DataRow("redis", "CurlSurface:Fail-Redis-2")]
    [DataRow("rediss", "CurlSurface:Fail-Rediss-2")]
    public Task Fail_SuccessfulGet_WritesTheReplyAndExits0(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Value), "-sS", "--fail", Url(scheme));

                AssertRun(result, requirement, "manual: --fail", 0, "v\n", string.Empty);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-Redis-1")]
    [SpecRequirement("CurlSurface:MaxTime-Rediss-1")]
    [DataRow("redis", "CurlSurface:MaxTime-Redis-1")]
    [DataRow("rediss", "CurlSurface:MaxTime-Rediss-1")]
    public Task MaxTime_ServerNeverAnswers_Exits28AtTheDeadline(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunPastMaxTimeAsync(Route(scheme, Value), 5, Url(scheme), TestContext.CancellationToken);

                AssertRun(result, requirement, "manual: -m, EXIT CODES 28", 28, string.Empty, "curl: (28) Operation timed out after 5000 milliseconds with 0 bytes received\n");
            });

    private static string[] Value(string command) => [RedisCaseRun.Hello3Reply, "$1\r\nv\r\n"];

    private static string[] Error(string command) => [RedisCaseRun.Hello3Reply, $"-{ServerErrorText}\r\n"];

    private static string Url(string scheme) => $"{scheme}://localhost/GET/k";

    // The connection line every transfer's -v writes (curl 8.22.0 measured: "* Established connection to localhost
    // (127.0.0.1 port 6379) from 127.0.0.1 port <local>", BL-4651 Notes), and for rediss a TLS line after it: the
    // Schannel build's "* schannel: ..." on Windows, the OpenSSL build's "* SSL connection using TLSv1..." elsewhere.
    private const string ConnectionLine = "* Established connection to localhost (127.0.0.1 port 6379) from ";

    private static string[] VerboseLines(string scheme) => scheme == "rediss"
        ? [ConnectionLine, OperatingSystem.IsWindows() ? "* schannel: " : "* SSL connection using TLSv1."]
        : [ConnectionLine];

    private static TransferSurfaceRoute Route(string scheme, Func<string, string[]> replies)
    {
        Func<IReferenceServer> server = () => new RedisReferenceServer(command => Reply(Encoding.UTF8.GetString(command[0]), replies));
        return scheme == "rediss" ? TransferSurfaceRoute.Tls(6379, server) : TransferSurfaceRoute.Tcp(6379, server);
    }

    private static IReadOnlyList<byte[]> Reply(string command, Func<string, string[]> replies) =>
        [Encoding.Latin1.GetBytes(command == "HELLO" ? replies(command)[0] : replies(command)[1])];
}
