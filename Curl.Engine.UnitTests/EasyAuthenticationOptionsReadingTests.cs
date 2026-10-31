using System.Net;
using System.Text;
using Curl.Api.Options.Authentication;
using Curl.Console;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins that <see cref="TransferCredentialLookup" /> reads <see cref="EasyAuthenticationOptions" /> as
/// ADR-0516 maps it (BL-2338): <see cref="EasyAuthenticationOptions.Netrc" /> and
/// <see cref="EasyAuthenticationOptions.NetrcFile" /> choose the file, and a user name in
/// <see cref="EasyAuthenticationOptions.UserName" /> or before the first colon of
/// <see cref="EasyAuthenticationOptions.UserPassword" /> wins over the URL's and the file's credentials.
/// </summary>
[TestClass]
public sealed class EasyAuthenticationOptionsReadingTests
{
    private const string NetrcPath = "my.netrc";

    private static readonly CurlUrl Url = CurlUrl.Parse("http://url-user:url-pw@h/");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TryLookUp_NetrcRequiredWithNetrcFile_SendsTheFilesEntry()
    {
        EasyAuthenticationOptions options = new() { Netrc = NetrcUsage.Required, NetrcFile = NetrcPath };
        Diagnostics.Arrange("options", options);

        bool looked = Lookup().TryLookUp(options, CurlUrl.Parse("http://h/"), out NetworkCredential? credentials, out TransferResult? failure);
        Diagnostics.Act("looked / user", $"{looked} / {credentials?.UserName}");

        Assert.IsTrue(looked);
        Assert.IsNull(failure);
        Assert.AreEqual("netrc-user", credentials!.UserName);
        Assert.AreEqual("netrc-pw", credentials.Password);
    }

    [TestMethod]
    public void TryLookUp_NetrcRequiredAndTheHostFound_ReportsNoHostNotFoundLine()
    {
        EasyAuthenticationOptions options = new() { Netrc = NetrcUsage.Required, NetrcFile = NetrcPath };
        List<string> infoLines = [];
        Diagnostics.Arrange("options", options);

        bool looked = Lookup().TryLookUp(options, CurlUrl.Parse("http://h/"), out _, out _, infoLines.Add);
        Diagnostics.Act("looked / info lines", $"{looked} / {string.Join(" | ", infoLines)}");

        Assert.IsTrue(looked);
        Assert.IsEmpty(infoLines);
    }

    [TestMethod]
    public void TryLookUp_NetrcRequiredAndTheHostNotInTheFile_ReportsTheHostNotFoundLine()
    {
        EasyAuthenticationOptions options = new() { Netrc = NetrcUsage.Required, NetrcFile = NetrcPath };
        List<string> infoLines = [];
        Diagnostics.Arrange("options", options);

        bool looked = Lookup().TryLookUp(options, CurlUrl.Parse("http://other/"), out _, out _, infoLines.Add);
        Diagnostics.Act("looked / info lines", $"{looked} / {string.Join(" | ", infoLines)}");

        Assert.IsTrue(looked);
        Assert.AreEqual("Could not find host other in the my.netrc file; using defaults", infoLines.Single());
    }

    [TestMethod]
    public void TryLookUp_NetrcRequiredAndFileMissing_FailsWithExit26()
    {
        EasyAuthenticationOptions options = new() { Netrc = NetrcUsage.Required, NetrcFile = "missing.netrc" };
        Diagnostics.Arrange("options", options);

        bool looked = Lookup().TryLookUp(options, Url, out _, out TransferResult? failure);
        Diagnostics.Act("looked / failure", $"{looked} / {failure?.ExitCode}");

        Assert.IsFalse(looked);
        Assert.AreEqual(CurlExitCode.ReadError, failure!.ExitCode);
        Assert.AreEqual(TransferCredentialLookup.NoSuchFileMessage, failure.ErrorMessage);
    }

    [TestMethod]
    [DataRow("machine h login a\u0001b password netrc-pw\n", DisplayName = "control code in the login only")]
    [DataRow("machine h login netrc-user password a\u0001b\n", DisplayName = "control code in the password only")]
    public void TryLookUp_NetrcEntryWithAControlCodeInOneFieldOnFtp_FailsWithExit26(string netrcText)
    {
        // One field alone is enough: either the login or the password with a byte below 0x20 is refused (AF-0510).
        InMemoryDataFileReader reader = new();
        reader.Files[NetrcPath] = Encoding.UTF8.GetBytes(netrcText);
        EasyAuthenticationOptions options = new() { Netrc = NetrcUsage.Required, NetrcFile = NetrcPath };
        Diagnostics.Arrange("netrc", netrcText);

        bool looked = Lookup(reader).TryLookUp(options, CurlUrl.Parse("ftp://h/"), out _, out TransferResult? failure);
        Diagnostics.Act("looked / failure", $"{looked} / {failure?.ExitCode}");

        Assert.IsFalse(looked);
        Assert.AreEqual(CurlExitCode.ReadError, failure!.ExitCode);
        Assert.AreEqual(TransferCredentialLookup.NetrcControlCodeMessage, failure.ErrorMessage);
    }

    [TestMethod]
    public void TryLookUp_NetrcIgnored_SendsTheUrlsCredentials()
    {
        EasyAuthenticationOptions options = new() { NetrcFile = NetrcPath };
        Diagnostics.Arrange("options", options);

        _ = Lookup().TryLookUp(options, Url, out NetworkCredential? credentials, out _);
        Diagnostics.Act("user", credentials?.UserName);

        Assert.AreEqual("url-user", credentials!.UserName);
        Assert.AreEqual("url-pw", credentials.Password);
    }

    [TestMethod]
    [DataRow("u:p")]
    [DataRow("u")]
    public void TryLookUp_UserPasswordWithAUserName_WinsAndReadsNoFile(string userPassword)
    {
        EasyAuthenticationOptions options = new() { Netrc = NetrcUsage.Required, NetrcFile = NetrcPath, UserPassword = userPassword };
        InMemoryDataFileReader reader = ReaderWithNetrc();
        Diagnostics.Arrange("user password", userPassword);

        bool looked = Lookup(reader).TryLookUp(options, Url, out NetworkCredential? credentials, out _);
        Diagnostics.Act("looked / credentials / paths read", $"{looked} / {credentials} / {reader.PathsRead.Count}");

        Assert.IsTrue(looked);
        Assert.IsNull(credentials);
        Assert.IsEmpty(reader.PathsRead);
        Assert.IsNull(Lookup().ForRedirectHops(options));
    }

    [TestMethod]
    public void TryLookUp_UserPasswordWithNoUserName_LetsTheUrlsCredentialsStand()
    {
        EasyAuthenticationOptions options = new() { UserPassword = ":p" };
        Diagnostics.Arrange("options", options);

        _ = Lookup().TryLookUp(options, Url, out NetworkCredential? credentials, out _);
        Diagnostics.Act("user", credentials?.UserName);

        Assert.AreEqual("url-user", credentials!.UserName);
    }

    [TestMethod]
    public void TryLookUp_UserNameSet_WinsOverAUserPasswordWithNoUserName()
    {
        EasyAuthenticationOptions options = new() { Netrc = NetrcUsage.Required, NetrcFile = NetrcPath, UserName = "name", UserPassword = ":p" };
        Diagnostics.Arrange("options", options);

        _ = Lookup().TryLookUp(options, Url, out NetworkCredential? credentials, out _);
        Diagnostics.Act("credentials", credentials);

        Assert.IsNull(credentials);
    }

    [TestMethod]
    public void ForRedirectHops_NetrcOptional_LooksEachHopUpInTheFile()
    {
        EasyAuthenticationOptions options = new() { Netrc = NetrcUsage.Optional, NetrcFile = NetrcPath };
        Diagnostics.Arrange("options", options);

        NetworkCredential? hop = Lookup().ForRedirectHops(options)!(CurlUrl.Parse("http://h/next"));
        Diagnostics.Act("hop user", hop?.UserName);

        Assert.AreEqual("netrc-user", hop!.UserName);
    }

    [TestMethod]
    [DataRow("ftp://a%01b@h/")]
    [DataRow("ftp://%01u:p@h/")]
    [DataRow("ftp://u:%01p@h/")]
    public void TryCheckUrlCredentials_ControlCodeInUrlCredentials_FailsUnlessAUserNameWins(string text)
    {
        // The control code first (index 0) as well as inside, in the user name and in the password (AF-0364).
        CurlUrl url = CurlUrl.Parse(text);
        Diagnostics.Arrange("url", url);

        bool refused = !TransferCredentialLookup.TryCheckUrlCredentials(new EasyAuthenticationOptions(), url, out TransferResult? failure);
        bool passedWithUser = TransferCredentialLookup.TryCheckUrlCredentials(new EasyAuthenticationOptions { UserPassword = "u:p" }, url, out _);
        Diagnostics.Act("refused / passed with -u", $"{refused} / {passedWithUser}");

        Assert.IsTrue(refused);
        Assert.AreEqual(CurlExitCode.UrlMalformat, failure!.ExitCode);
        Assert.IsTrue(passedWithUser);
    }

    [TestMethod]
    public void TryCheckUrlCredentials_PlainUrlCredentialsOnFtp_Passes()
    {
        CurlUrl url = CurlUrl.Parse("ftp://user:pw@h/");
        Diagnostics.Arrange("url", url);

        bool passed = TransferCredentialLookup.TryCheckUrlCredentials(new EasyAuthenticationOptions(), url, out TransferResult? failure);
        Diagnostics.Act("passed", passed);

        Assert.IsTrue(passed);
        Assert.IsNull(failure);
    }

    [TestMethod]
    [DataRow("http://zz@h/", "zz", "")]
    [DataRow("http://:x@h/", "", "x")]
    public void TryLookUp_NoNetrcAndUrlGivesOnlyOneHalf_SendsItWithTheOtherHalfEmpty(string url, string user, string password)
    {
        Diagnostics.Arrange("url", url);

        bool looked = Lookup().TryLookUp(new EasyAuthenticationOptions(), CurlUrl.Parse(url), out NetworkCredential? credentials, out _);
        Diagnostics.Act("looked / credentials", $"{looked} / {credentials?.UserName}:{credentials?.Password}");

        Assert.IsTrue(looked);
        Assert.IsNotNull(credentials);
        Assert.AreEqual(user, credentials.UserName);
        Assert.AreEqual(password, credentials.Password);
    }

    [TestMethod]
    public void TryLookUp_NoNetrcAndUrlWithEmptyUserInformation_SendsNoCredentials()
    {
        bool looked = Lookup().TryLookUp(new EasyAuthenticationOptions(), CurlUrl.Parse("http://@h/"), out NetworkCredential? credentials, out _);
        Diagnostics.Act("looked / credentials", $"{looked} / {credentials?.UserName}");

        Assert.IsTrue(looked);
        Assert.IsNull(credentials);
    }

    private static TransferCredentialLookup Lookup(InMemoryDataFileReader? reader = null) =>
        new(ReadFileOf(reader ?? ReaderWithNetrc()), _ => null, runsOnWindows: false);

    private static Func<string, byte[]?> ReadFileOf(InMemoryDataFileReader reader) =>
        path => reader.TryReadFile(path, out byte[] contents) ? contents : null;

    private static InMemoryDataFileReader ReaderWithNetrc()
    {
        InMemoryDataFileReader reader = new();
        reader.Files[NetrcPath] = Encoding.UTF8.GetBytes("machine h login netrc-user password netrc-pw\n");
        return reader;
    }
}
