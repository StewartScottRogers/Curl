using System.Net;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;

namespace Curl.Engine;

/// <summary>
/// Pins the proxy credentials a handle's <c>CURLOPT_PROXYUSERNAME</c>, <c>CURLOPT_PROXYPASSWORD</c> and
/// <c>CURLOPT_PROXYUSERPWD</c> give a perform from the options, used as given and never URL-decoded, as
/// libcurl 8.22.0 sends them in a <c>CONNECT</c>'s <c>Proxy-Authorization</c> (upstream test503, BL-2584).
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_ProxyUserNameAndPasswordSet_PutsThemUndecodedOnTheContextsProxy()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler, []);
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Proxy = new EasyProxyOptions { Proxy = "http://proxy:3128", HttpProxyTunnel = true },
            Authentication = new EasyAuthenticationOptions { ProxyUserName = "test%20", ProxyPassword = "ing%41" },
        };

        await easy.PerformAsync(CancellationToken.None);

        NetworkCredential credential = handler.Contexts.Single().Proxy!.Credential!;
        Assert.AreEqual("test%20", credential.UserName);
        Assert.AreEqual("ing%41", credential.Password);
    }
}
