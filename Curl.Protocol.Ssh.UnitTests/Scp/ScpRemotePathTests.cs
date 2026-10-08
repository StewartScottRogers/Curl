using System.Text;
using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Scp;

/// <summary>
/// Pins <see cref="ScpRemotePath" /> to the paths curl 8.21.0 asked <c>scp</c> for,
/// as OpenSSH logged them 2026-09-29 (BL-574).
/// </summary>
[TestClass]
public sealed class ScpRemotePathTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("/~/bl574home.txt", "bl574home.txt", DisplayName = "home directory, as measured")]
    [DataRow("/%7E/bl574home.txt", "bl574home.txt", DisplayName = "escaped tilde, as measured")]
    [DataRow("/~/", "/~/", DisplayName = "home prefix alone, as measured")]
    [DataRow("/~", "/~", DisplayName = "tilde alone, as measured")]
    [DataRow("/f/a%20b.txt", "/f/a b.txt", DisplayName = "escaped space, as measured")]
    [DataRow("/x/~/f", "/x/~/f", DisplayName = "tilde further in")]
    public void Resolve_UrlPath_GivesThePathCurlSent(string urlPath, string path)
    {
        Diagnostics.Arrange("url path", urlPath);

        byte[] actual = ScpRemotePath.Resolve(urlPath);

        Diagnostics.Act("path", Encoding.Latin1.GetString(actual));
        Diagnostics.AssertBytes("path", Encoding.Latin1.GetBytes(path), actual);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(path), ScpRemotePath.Resolve(urlPath));
    }
}
