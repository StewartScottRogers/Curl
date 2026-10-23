using Curl.Conformance.Differential.Libcurl.WebSocket;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.WebSocket;

[TestClass]
public sealed class LibcurlWebSocketCasesTests
{
    [TestMethod]
    public void All_Cases_HaveUniqueNamesAndCoverBothModes()
    {
        var cases = LibcurlWebSocketCases.All;

        Assert.HasCount(12, cases);
        Assert.HasCount(cases.Count, cases.Select(webSocketCase => webSocketCase.Name).Distinct());
        Assert.IsTrue(cases.Any(webSocketCase => !webSocketCase.ConnectOnly && webSocketCase.Steps.Count == 0));
        Assert.IsTrue(cases.Any(webSocketCase => webSocketCase.ServerHangsUp));
    }
}
