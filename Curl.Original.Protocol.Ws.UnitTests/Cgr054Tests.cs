using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ws;

/// <summary>
/// Pins CGR-054 (BL-3583): curl 8.22.0 sends the upgrade request's path with its <c>%XX</c> escapes in
/// upper case and the query as typed (measured: <c>ws://h/a%2fb%3d?x=%2f</c> goes out as
/// <c>GET /a%2Fb%3D?x=%2f</c>).
/// </summary>
[TestClass]
public sealed class Cgr054Tests
{
    [TestMethod]
    public void RequestTarget_LowerCaseEscapesInThePath_UpperCasesThemAndKeepsTheQuery()
    {
        Assert.AreEqual("/a%2Fb%3D?x=%2f", WsUpgradeRequestFormatter.RequestTarget(CurlUrl.Parse("ws://127.0.0.1:20583/a%2fb%3d?x=%2f")));
    }
}
