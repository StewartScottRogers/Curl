using System.Net;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.Errors;
using Curl.Api.Info;
using Curl.Api.Messages;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;

namespace Curl.Api.HttpMessageHandler.Behaviour;

[TestClass]
public sealed class CurlHttpMessageHandlerBehaviourTests
{
    private const HttpAuthenticationMethods EveryScheme =
        HttpAuthenticationMethods.Negotiate | HttpAuthenticationMethods.Ntlm | HttpAuthenticationMethods.Digest | HttpAuthenticationMethods.Basic;

    private static readonly Uri Origin = new("http://example/a");

    private static async Task<HttpResponseMessage> SendAsync(ScriptedEasyTransfer easy, Action<CurlHttpMessageHandler> configure, CurlClientOptions? options = null)
    {
        await using CurlClient client = new(new ScriptedCurlEngine(easy), options ?? new CurlClientOptions(), ownsEngine: true);
        CurlHttpMessageHandler handler = new(client);
        configure(handler);
        using HttpClient http = new(handler);
        HttpResponseMessage response = await http.GetAsync(Origin);
        _ = await response.Content.ReadAsByteArrayAsync();
        return response;
    }

    private static async Task<EasyOptions> OptionsSentAsync(Action<CurlHttpMessageHandler> configure, CurlClientOptions? options = null)
    {
        ScriptedEasyTransfer easy = new();
        await SendAsync(easy, configure, options);
        return easy.PerformedOptions!;
    }

    private static ScriptedEasyTransfer Responding(params string[] headerLines)
    {
        ScriptedEasyTransfer easy = new() { BodyBytes = Encoding.UTF8.GetBytes("decoded") };
        easy.HeaderLines.AddRange(["HTTP/1.1 200 OK\r\n", .. headerLines, "\r\n"]);
        return easy;
    }

    [TestMethod]
    public async Task Defaults_AreSocketsHttpHandlers()
    {
        await using CurlClient client = new(new ScriptedCurlEngine(new ScriptedEasyTransfer()));
        using CurlHttpMessageHandler handler = new(client);

        Assert.AreEqual(DecompressionMethods.None, handler.AutomaticDecompression);
        Assert.IsTrue(handler.AllowAutoRedirect);
        Assert.AreEqual(50, handler.MaxAutomaticRedirections);
        Assert.IsNull(handler.Credentials);
        Assert.IsFalse(handler.PreAuthenticate);
        Assert.IsTrue(handler.UseCookies);
        Assert.AreEqual(0, handler.CookieContainer.Count);
    }

    [TestMethod]
    public async Task Defaults_FollowRedirectsObeyingTheCode_AndKeepTheClientsEncodingCookieAndAuthentication()
    {
        CurlClientOptions options = new()
        {
            Http = new CurlHttpOptions { AcceptEncoding = "zstd", Cookie = "fixed=1" },
            Authentication = new CurlAuthenticationOptions { UserPassword = "client:secret" },
        };

        EasyOptions sent = await OptionsSentAsync(_ => { }, options);

        Assert.AreEqual(FollowLocation.ObeyCode, sent.Http.FollowLocation);
        Assert.AreEqual(50, sent.Http.MaxRedirects);
        Assert.AreEqual("zstd", sent.Http.AcceptEncoding);
        Assert.AreEqual("fixed=1", sent.Http.Cookie);
        Assert.AreEqual("client:secret", sent.Authentication.UserPassword);
        Assert.AreEqual(HttpAuthenticationMethods.Basic, sent.Authentication.HttpAuthentication);
    }

    [TestMethod]
    public async Task AllowAutoRedirectFalse_SetsFollowLocationOff_AndMaxRedirectionsSetsMaxRedirs()
    {
        EasyOptions sent = await OptionsSentAsync(handler =>
        {
            handler.AllowAutoRedirect = false;
            handler.MaxAutomaticRedirections = 3;
        });

        Assert.AreEqual(FollowLocation.None, sent.Http.FollowLocation);
        Assert.AreEqual(3, sent.Http.MaxRedirects);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task MaxAutomaticRedirections_ZeroOrNegative_Throws(int value)
    {
        await using CurlClient client = new(new ScriptedCurlEngine(new ScriptedEasyTransfer()));
        using CurlHttpMessageHandler handler = new(client);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => handler.MaxAutomaticRedirections = value);
    }

    [TestMethod]
    public async Task FollowedRedirect_SetsTheRequestUriToTheRedirectedUrl()
    {
        ScriptedEasyTransfer easy = Responding();
        easy.Info = new TransferInfo { EffectiveUrl = "http://other/b" };

        HttpResponseMessage response = await SendAsync(easy, _ => { });

        Assert.AreEqual(new Uri("http://other/b"), response.RequestMessage!.RequestUri);
    }

    [TestMethod]
    public async Task RedirectNotFollowed_KeepsTheRequestUri()
    {
        ScriptedEasyTransfer easy = Responding();
        easy.Info = new TransferInfo { EffectiveUrl = "http://other/b" };

        HttpResponseMessage response = await SendAsync(easy, handler => handler.AllowAutoRedirect = false);

        Assert.AreEqual(Origin, response.RequestMessage!.RequestUri);
    }

    [TestMethod]
    public async Task RedirectLimitExceeded_ThrowsHttpRequestException_WithTooManyRedirectsInside()
    {
        ScriptedEasyTransfer easy = new() { Result = CurlCode.TooManyRedirects, ErrorText = "Maximum (2) redirects followed" };

        HttpRequestException exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(
            () => SendAsync(easy, handler => handler.MaxAutomaticRedirections = 2));

        Assert.AreEqual(2, easy.PerformedOptions!.Http.MaxRedirects);
        CurlException inner = Assert.IsInstanceOfType<CurlException>(exception.InnerException);
        Assert.AreEqual(CurlCode.TooManyRedirects, inner.Code);
        Assert.AreEqual("Maximum (2) redirects followed", exception.Message);
    }

    [TestMethod]
    [DataRow(DecompressionMethods.All, "gzip, deflate, br")]
    [DataRow(DecompressionMethods.GZip, "gzip")]
    [DataRow(DecompressionMethods.Deflate | DecompressionMethods.Brotli, "deflate, br")]
    public async Task AutomaticDecompression_AsksForTheCodings_AndDropsTheEncodingAndLengthOfTheDecodedBody(DecompressionMethods methods, string acceptEncoding)
    {
        ScriptedEasyTransfer easy = Responding("Content-Encoding: gzip\r\n", "Content-Length: 31\r\n", "Content-Type: text/plain\r\n");
        CurlClientOptions options = new() { Http = new CurlHttpOptions { HttpContentDecoding = false } };

        HttpResponseMessage response = await SendAsync(easy, handler => handler.AutomaticDecompression = methods, options);

        Assert.AreEqual(acceptEncoding, easy.PerformedOptions!.Http.AcceptEncoding);
        Assert.IsTrue(easy.PerformedOptions.Http.HttpContentDecoding);
        Assert.IsEmpty(response.Content.Headers.ContentEncoding);
        Assert.IsFalse(response.Content.Headers.NonValidated.Contains("Content-Length"));
        Assert.AreEqual("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.AreEqual("decoded", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task NoAutomaticDecompression_KeepsTheContentEncodingAndLength()
    {
        ScriptedEasyTransfer easy = Responding("Content-Encoding: gzip\r\n", "Content-Length: 7\r\n");

        HttpResponseMessage response = await SendAsync(easy, _ => { });

        Assert.IsNull(easy.PerformedOptions!.Http.AcceptEncoding);
        Assert.AreEqual("gzip", response.Content.Headers.ContentEncoding.Single());
        Assert.AreEqual(7, response.Content.Headers.ContentLength);
    }

    [TestMethod]
    public async Task NetworkCredential_SetsUserAndPasswordForEveryScheme_ProbingFirst()
    {
        EasyOptions sent = await OptionsSentAsync(handler => handler.Credentials = new NetworkCredential("user", "secret"));

        Assert.AreEqual("user", sent.Authentication.UserName);
        Assert.AreEqual("secret", sent.Authentication.Password);
        Assert.IsNull(sent.Authentication.UserPassword);
        Assert.AreEqual(EveryScheme | HttpAuthenticationMethods.Only, sent.Authentication.HttpAuthentication);
    }

    [TestMethod]
    public async Task PreAuthenticate_SendsTheCredentialsWithoutProbing()
    {
        EasyOptions sent = await OptionsSentAsync(handler =>
        {
            handler.Credentials = new NetworkCredential("user", "secret");
            handler.PreAuthenticate = true;
        });

        Assert.AreEqual(EveryScheme, sent.Authentication.HttpAuthentication);
    }

    [TestMethod]
    public async Task CredentialWithDomain_SendsDomainBackslashUser_AndKeepsTheClientsOtherAuthentication()
    {
        CurlClientOptions options = new() { Authentication = new CurlAuthenticationOptions { UserPassword = "old:pw", ServiceName = "HTTP" } };

        EasyOptions sent = await OptionsSentAsync(handler => handler.Credentials = new NetworkCredential("user", "secret", "CORP"), options);

        Assert.AreEqual(@"CORP\user", sent.Authentication.UserName);
        Assert.IsNull(sent.Authentication.UserPassword);
        Assert.AreEqual("HTTP", sent.Authentication.ServiceName);
    }

    [TestMethod]
    public async Task CredentialCache_GivesTheStrongestSchemesCredential_WithOnlyTheSchemesSharingIt()
    {
        CredentialCache cache = new()
        {
            { Origin, "Basic", new NetworkCredential("basic", "b") },
            { Origin, "Digest", new NetworkCredential("digest", "d") },
        };

        EasyOptions sent = await OptionsSentAsync(handler => handler.Credentials = cache);

        Assert.AreEqual("digest", sent.Authentication.UserName);
        Assert.AreEqual("d", sent.Authentication.Password);
        Assert.AreEqual(HttpAuthenticationMethods.Digest | HttpAuthenticationMethods.Only, sent.Authentication.HttpAuthentication);
    }

    [TestMethod]
    public async Task CredentialCacheWithNothingForTheUrl_KeepsTheClientsAuthentication()
    {
        CredentialCache cache = new() { { new Uri("http://elsewhere/"), "Basic", new NetworkCredential("u", "p") } };

        EasyOptions sent = await OptionsSentAsync(handler => handler.Credentials = cache);

        Assert.IsNull(sent.Authentication.UserName);
        Assert.AreEqual(HttpAuthenticationMethods.Basic, sent.Authentication.HttpAuthentication);
    }

    [TestMethod]
    public async Task DefaultCredentials_AreTheLoggedOnUsers_ForNegotiateAndNtlmOnly()
    {
        EasyOptions sent = await OptionsSentAsync(handler => handler.Credentials = CredentialCache.DefaultCredentials);

        Assert.AreEqual("", sent.Authentication.UserName);
        Assert.AreEqual(HttpAuthenticationMethods.Negotiate | HttpAuthenticationMethods.Ntlm | HttpAuthenticationMethods.Only, sent.Authentication.HttpAuthentication);
    }

    [TestMethod]
    public void AcceptEncodingOf_NamesTheCodingsInSocketsHttpHandlersOrder()
    {
        Assert.AreEqual("gzip, br", CurlHttpMessageHandler.AcceptEncodingOf(DecompressionMethods.Brotli | DecompressionMethods.GZip));
    }

    [TestMethod]
    public async Task UseCookies_SendsTheContainersCookies_AndStoresTheResponsesValidOnes()
    {
        ScriptedEasyTransfer easy = Responding("Set-Cookie: b=2\r\n", "set-cookie: c=3; Domain=elsewhere.test\r\n");
        CookieContainer cookies = new();
        cookies.Add(Origin, new Cookie("a", "1"));

        await SendAsync(easy, handler => handler.CookieContainer = cookies);

        Assert.AreEqual("a=1", easy.PerformedOptions!.Http.Cookie);
        Assert.AreEqual("a=1; b=2", cookies.GetCookieHeader(Origin));
        Assert.AreEqual(2, cookies.Count);
    }

    [TestMethod]
    public async Task UseCookiesFalse_NeitherSendsNorStoresTheContainersCookies()
    {
        ScriptedEasyTransfer easy = Responding("Set-Cookie: b=2\r\n");
        CookieContainer cookies = new();
        cookies.Add(Origin, new Cookie("a", "1"));

        await SendAsync(easy, handler =>
        {
            handler.CookieContainer = cookies;
            handler.UseCookies = false;
        });

        Assert.IsNull(easy.PerformedOptions!.Http.Cookie);
        Assert.AreEqual("a=1", cookies.GetCookieHeader(Origin));
    }

    [TestMethod]
    public async Task CookieContainer_Null_Throws()
    {
        await using CurlClient client = new(new ScriptedCurlEngine(new ScriptedEasyTransfer()));
        using CurlHttpMessageHandler handler = new(client);

        Assert.ThrowsExactly<ArgumentNullException>(() => handler.CookieContainer = null!);
    }
}
