using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins that <see cref="EngineComposition.CreateSaslAuthenticator" /> draws DIGEST-MD5's client nonce from the
/// random bytes it is given and, over the hand-built NTLM contexts, answers in curl's own form (ADR-0751, BL-2834).
/// </summary>
[TestClass]
public sealed class EngineCompositionDigestClientNonceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CreateSaslAuthenticator_ForcedRandomBytes_SendsTheirHexAsTheClientNonce()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        HandBuiltNtlmSecurityContextFactory contexts = new(new UnusedFactory(), new UnusedFactory());
        ISaslAuthenticator authenticator = EngineComposition.CreateSaslAuthenticator(contexts, digestRandomBytes: count => new byte[count]);
        ISaslExchange exchange = authenticator.Begin("DIGEST-MD5", new SaslRequest(new NetworkCredential("user", "secret"), null, null, null, "imap", "127.0.0.1"));
        byte[] challenge = Encoding.ASCII.GetBytes("realm=\"curl\",nonce=\"5300d17a\",algorithm=md5-sess,qop=\"auth\"");

        byte[]? answer = await exchange.RespondAsync(challenge, CancellationToken.None);

        string text = Encoding.ASCII.GetString(answer!);
        diagnostics.Act("answer", text);
        StringAssert.StartsWith(text, "username=\"user\",realm=\"curl\",nonce=\"5300d17a\",cnonce=\"00000000000000000000000000000000\",");
    }

    [TestMethod]
    public void CreateHttpAuthenticator_ForcedRandomBytes_BuildsTheAuthenticator()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        HandBuiltNtlmSecurityContextFactory contexts = new(new UnusedFactory(), new UnusedFactory());

        RankedHttpAuthenticator authenticator = EngineComposition.CreateHttpAuthenticator(contexts, digestRandomBytes: count => new byte[count]);

        diagnostics.Assert("authenticator built", true, authenticator is not null);
        Assert.IsNotNull(authenticator);
    }

    private sealed class UnusedFactory : ISecurityContextFactory
    {
        public ISecurityContext Create(SecurityContextRequest request) => throw new NotSupportedException();
    }
}
