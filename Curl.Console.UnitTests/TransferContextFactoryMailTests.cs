using System.Net;

using Curl.Authentication;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins how <see cref="TransferContextFactory" /> carries the mail options into
/// <see cref="TransferContext.Mail" /> (<see cref="MailRequestOptionsMapping" />), and that
/// <see cref="CurlComposition.CreateSaslAuthenticator" /> composes the authenticator from
/// <c>Curl.Authentication.UnitLibrary</c>.
/// </summary>
[TestClass]
public sealed class TransferContextFactoryMailTests
{
    [TestMethod]
    public void Create_MailScheme_CarriesEveryMailOptionUnchanged()
    {
        MailRequestOptions mail = MailOf(
            "smtp://example.com/",
            "--mail-from", "from@example.com",
            "--mail-rcpt", "b@example.com",
            "--mail-rcpt", "a@example.com",
            "--mail-rcpt", "",
            "--mail-auth", "auth@example.com",
            "--mail-rcpt-allowfails",
            "-X", "VRFY",
            "--login-options", "AUTH=PLAIN",
            "--sasl-authzid", "zid",
            "--sasl-ir",
            "--oauth2-bearer", "tok");

        Assert.AreEqual("from@example.com", mail.From);
        CollectionAssert.AreEqual(new[] { "b@example.com", "a@example.com", string.Empty }, mail.Recipients.ToArray());
        Assert.AreEqual("auth@example.com", mail.Auth);
        Assert.IsTrue(mail.RecipientAllowFails);
        Assert.AreEqual("VRFY", mail.CustomCommand);
        Assert.AreEqual("AUTH=PLAIN", mail.LoginOptions);
        Assert.AreEqual("zid", mail.SaslAuthorizationIdentity);
        Assert.IsTrue(mail.SaslInitialResponse);
        Assert.AreEqual("tok", mail.BearerToken);
        Assert.IsNull(mail.ServiceName);
    }

    [TestMethod]
    public void Create_MailSchemeWithoutMailOptions_CarriesTheDefaults()
    {
        MailRequestOptions mail = MailOf("imap://example.com/");

        Assert.AreEqual(new MailRequestOptions { UploadFlags = mail.UploadFlags, Recipients = mail.Recipients }, mail);
        Assert.IsEmpty(mail.Recipients);
        CollectionAssert.AreEqual(new[] { "seen" }, mail.UploadFlags.ToArray());
    }

    [TestMethod]
    [DataRow("seen,draft,answered", new[] { "answered", "draft", "seen" })]
    [DataRow("flagged,deleted", new[] { "deleted", "flagged", "seen" })]
    [DataRow("-seen", new string[0])]
    public void Create_UploadFlags_AreTheSetFlagsNamesInCurlsOrder(string value, string[] expected)
    {
        MailRequestOptions mail = MailOf("imap://example.com/INBOX", "--upload-flags", value);

        CollectionAssert.AreEqual(expected, mail.UploadFlags.ToArray());
    }

    [TestMethod]
    [DataRow("smtp://example.com/")]
    [DataRow("smtps://example.com/")]
    [DataRow("pop3://example.com/")]
    [DataRow("pop3s://example.com/")]
    [DataRow("imap://example.com/")]
    [DataRow("imaps://example.com/")]
    public void Create_EachMailScheme_CarriesMailOptions(string url)
    {
        Assert.AreEqual("from@example.com", MailOf(url, "--mail-from", "from@example.com").From);
    }

    [TestMethod]
    [DataRow("http://example.com/")]
    [DataRow("ftp://example.com/")]
    [DataRow("file:///x.txt")]
    public void Create_OtherScheme_CarriesNoMailOptions(string url)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse(url, "--mail-from", "from@example.com"), CurlUrl.Parse(url), output, null, null, null);

        Assert.IsNull(context.Mail);
    }

    [TestMethod]
    public void CreateSaslAuthenticator_IsTheAuthenticationLibrarysInThePlatformsEncoding()
    {
        ISaslAuthenticator authenticator = CurlComposition.CreateSaslAuthenticator();
        SaslRequest request = new(new NetworkCredential("\u00e9", "p"), null, null, null, "smtp", "example.com");

        ISaslExchange exchange = authenticator.Begin("PLAIN", request);

        Assert.IsInstanceOfType<SaslAuthenticator>(authenticator);
        CollectionAssert.AreEqual(
            CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()).GetBytes("\0\u00e9\0p"),
            exchange.InitialResponse);
    }

    /// <summary>Creates the context for <paramref name="url" /> and returns its mail options.</summary>
    private static MailRequestOptions MailOf(string url, params string[] arguments)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse([url, .. arguments]), CurlUrl.Parse(url), output, null, null, null);

        Assert.IsNotNull(context.Mail);
        return context.Mail;
    }

    /// <summary>
    /// Parses <paramref name="arguments" /> as if every path exists and returns the accepted options.
    /// </summary>
    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
