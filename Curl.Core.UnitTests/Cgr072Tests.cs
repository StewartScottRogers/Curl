using System.Net;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-072 (BL-3601): under <c>--location-trusted</c> a redirect to another host or port
/// still sends credentials but keeps no Digest answer, so the new host's first request carries
/// no <c>Authorization</c> and its own 401 starts a fresh exchange at <c>nc=00000001</c>, as
/// curl 8.22.0 does; a same-origin redirect keeps it and counts on (upstream test1286).
/// </summary>
[TestClass]
public sealed class Cgr072Tests
{
    private const string Start = "http://127.0.0.1:20500/a";

    [TestMethod]
    [DataRow("http://localhost:20500/b", null, DisplayName = "other-host")]
    [DataRow("http://127.0.0.1:20501/b", null, DisplayName = "other-port")]
    [DataRow("http://127.0.0.1:20500/b", "Digest nc=00000001", DisplayName = "same-origin")]
    public async Task FollowAsync_TrustedRedirect_KeepsTheDigestAnswerOnlyForTheSameOrigin(string target, string? expected)
    {
        ScriptedHandler handler = new(Redirect(target), TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = 200 } });

        await new RedirectFollower(new ProtocolDispatcher([handler]))
            .FollowAsync(
                new TransferContext
                {
                    Url = CurlUrl.Parse(Start),
                    Output = Stream.Null,
                    Http = new HttpRequestOptions { FollowRedirects = true },
                    Credentials = new NetworkCredential("user", "pass"),
                    TimeProvider = TimeProvider.System,
                },
                new RedirectPolicy { LocationTrusted = true });

        Assert.HasCount(2, handler.Contexts);
        Assert.IsNotNull(handler.Contexts[1].Credentials);
        Assert.AreEqual(expected, handler.Contexts[1].Http!.KeptDigestAuthorization);
    }

    private static TransferResult Redirect(string location) =>
        TransferResult.Success(0) with
        {
            Report = new TransferReport { ResponseCode = 302, RedirectUrl = location, DigestAuthorizationSent = "Digest nc=00000001" },
        };

    /// <summary>Serves http, answering each call with the next scripted result and keeping its context.</summary>
    private sealed class ScriptedHandler(params TransferResult[] script) : IProtocolHandler
    {
        public List<ITransferContext> Contexts { get; } = [];

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Contexts.Add(context);
            return ValueTask.FromResult(script[Contexts.Count - 1]);
        }
    }
}
