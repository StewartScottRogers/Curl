namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="ConnectResult.IsProxyAuthenticationProblem" />, curl's <c>authproblem</c>
/// carried out of a refused CONNECT (STD-227, ADR-1465).
/// </summary>
[TestClass]
public sealed class ConnectResultProxyAuthenticationTests
{
    [TestMethod]
    public void Failed_ByDefault_IsNotAProxyAuthenticationProblem()
    {
        Assert.IsFalse(ConnectResult.Failed(CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 407", null, 0, 407).IsProxyAuthenticationProblem);
    }

    [TestMethod]
    public void Failed_MarkedAProxyAuthenticationProblem_KeepsItsStatus()
    {
        var result = ConnectResult.Failed(CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 407", null, 0, 407).WithProxyAuthenticationProblem(true);

        Assert.IsTrue(result.IsProxyAuthenticationProblem);
        Assert.AreEqual(407, result.ProxyConnectResponseCode);
    }
}
