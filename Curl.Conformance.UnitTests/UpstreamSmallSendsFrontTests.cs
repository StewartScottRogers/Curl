using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="UpstreamSmallSendsFront"/> splits a case's writes only when its <c>&lt;setenv&gt;</c> sets <c>CURL_SMALLREQSEND</c> or <c>CURL_SMALLSENDS</c> to a positive number (BL-2832).</summary>
[TestClass]
public sealed class UpstreamSmallSendsFrontTests
{
    [TestMethod]
    [DataRow("CURL_SMALLREQSEND=128\n")]
    [DataRow("CURL_SMALLSENDS=1\n")]
    [DataRow("CURL_SMALLREQSEND=8\nCURL_SMALLSENDS=8\n")]
    [DataRow("CURL_SMALLREQSEND=x\nCURL_SMALLSENDS=8\n")]
    public void Wrap_CaseSetsAVariable_SplitsTheWrites(string setenv)
    {
        IConnector inner = new NoListenPortConnector(new UnreachableConnector());

        IConnector wrapped = new UpstreamSmallSendsFront().Wrap(inner, ContextFor(setenv));

        Assert.IsInstanceOfType<SmallSendsConnector>(wrapped);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("CURL_SMALLREQSEND=0\n")]
    [DataRow("CURL_SMALLSENDS=-1\nCURL_SMALLREQSEND=abc\n")]
    public void Wrap_CaseSetsNoPositiveLimit_ReturnsTheInnerConnector(string setenv)
    {
        IConnector inner = new NoListenPortConnector(new UnreachableConnector());

        IConnector wrapped = new UpstreamSmallSendsFront().Wrap(inner, ContextFor(setenv));

        Assert.AreSame(inner, wrapped);
    }

    [TestMethod]
    public void StageAndVariables_OutermostAndNone()
    {
        UpstreamSmallSendsFront front = new();
        Dictionary<string, string> variables = [];

        front.AddVariables(variables, certificateDirectory: null);

        Assert.AreEqual(UpstreamStandInStage.Outermost, front.Stage);
        Assert.IsEmpty(variables);
    }

    private static UpstreamStandInContext ContextFor(string setenv)
    {
        UpstreamTestCase testCase = ParsedTestCase.From($"<client>\n<setenv>\n{setenv}</setenv>\n</client>\n");
        return new UpstreamStandInContext(testCase, null, new SwsHttpServerConnector(testCase, TimeProvider.System));
    }

    private sealed class UnreachableConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Refused("unreachable"));
    }
}
