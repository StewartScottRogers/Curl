using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-J</c> beside <c>-C -</c> in one option group as curl 8.22.0 (Schannel) refuses it, measured 2026-11-12 (BL-5027).
/// </summary>
[TestClass]
public sealed class CommandLineRefusalRemoteHeaderNameTests
{
    private const string Url = "http://127.0.0.1:41000/cd";

    private static readonly string[] Expected =
    [
        "curl: --continue-at and --remote-header-name cannot be combined",
        "curl: try 'curl --help' or 'curl --manual' for more information",
    ];

    [TestMethod]
    [DataRow(new[] { "-OJ", "-C", "-", Url })]
    [DataRow(new[] { "-J", "-C", "-", "-o", "o.txt", Url })]
    [DataRow(new[] { "-C", "-", "-J", "-o", "o.txt", Url })]
    [DataRow(new[] { "-s", "-J", "-C", "-", "-o", "o.txt", Url })]
    public void Parse_RemoteHeaderNameWithContinueAtDash_IsRefusedWithTwoLines(string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(Expected, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileRemoteHeaderNameWithContinueAtDash_IsRefused()
    {
        RecordingDataFileReader reader = new();
        reader.Files["cfg"] = Encoding.UTF8.GetBytes("remote-header-name\nremote-name\ncontinue-at = -\n");

        CommandLineParseResult result = CommandLineParser.Parse(["-K", "cfg", Url], _ => true, new EmptyPasswordPrompt(), reader, false);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(Expected, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "-OJ", Url, "--next", "-C", "-", "-o", "y", Url })]
    [DataRow(new[] { "-J", "-O", Url })]
    [DataRow(new[] { "-C", "-", "-o", "y", Url })]
    [DataRow(new[] { "-J", "-C", "10", "-o", "y", Url })]
    public void Parse_RemoteHeaderNameAndContinueAtDashNotInOneGroup_IsAccepted(string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Refusal);
    }

    [TestMethod]
    public void ContinueAtWithRemoteHeaderName_Always_IsExitTwoWithBothLines()
    {
        CommandLineRefusal refusal = CommandLineRefusal.ContinueAtWithRemoteHeaderName();

        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(Expected, refusal.StandardErrorLines.ToArray());
    }
}
