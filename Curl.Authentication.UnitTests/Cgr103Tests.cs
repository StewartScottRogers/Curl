using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins CGR-103 (BL-3632, ADR-0826): curl 8.22.0 sends SASL LOGIN's empty password as an empty
/// line, not <c>=</c>, over SMTP, POP3 and IMAP alike (measured with
/// <c>Record-CurlExchange.ps1</c>: <c>AUTH LOGIN</c>, <c>dXNlcg==</c>, then an empty line).
/// </summary>
[TestClass]
public sealed class Cgr103Tests
{
    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Begin_LoginWithAnEmptyPassword_SendsTheEmptyPasswordAsAnEmptyLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", "user:");
        ISaslExchange exchange = new SaslAuthenticator(Windows1252)
            .Begin("LOGIN", new SaslRequest(new NetworkCredential("user", string.Empty), null, null, null, "smtp", "127.0.0.1"));
        byte[]? initialResponse = await exchange.GetInitialResponseAsync(CancellationToken.None);

        byte[]? password = await exchange.RespondAsync("Password:"u8.ToArray(), CancellationToken.None);
        bool sendsEmptyLine = exchange.SendsLastAnswerAsEmptyLine;

        diagnostics.Act("password answer length", password?.Length);
        diagnostics.Assert("sends last answer as empty line", true, sendsEmptyLine);
        CollectionAssert.AreEqual("user"u8.ToArray(), initialResponse);
        Assert.IsNotNull(password);
        Assert.IsEmpty(password);
        Assert.IsTrue(sendsEmptyLine);
    }
}
