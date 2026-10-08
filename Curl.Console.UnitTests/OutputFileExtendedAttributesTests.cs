using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="OutputFileExtendedAttributes.WithoutCredentials" />, curl's <c>stripcredentials</c>:
/// the user name and password go, and nothing else in the URL changes.
/// </summary>
[TestClass]
public sealed class OutputFileExtendedAttributesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("http://u:p@127.0.0.1:18653/a?b#frag", "http://127.0.0.1:18653/a?b#frag")]
    [DataRow("http://u@host/", "http://host/")]
    [DataRow("http://a@b:p@host?q@r", "http://host?q@r")]
    [DataRow("http://u:p@host", "http://host")]
    [DataRow("http://host/a@b", "http://host/a@b")]
    [DataRow("file:///tmp/a@b", "file:///tmp/a@b")]
    [DataRow("no-scheme@host", "no-scheme@host")]
    public void WithoutCredentials_RemovesOnlyTheUserInfo(string url, string expected)
    {
        Diagnostics.Arrange("url", url);

        string actual = OutputFileExtendedAttributes.WithoutCredentials(url);
        Diagnostics.Act("url without credentials", actual);

        Diagnostics.Diff("url without credentials", expected, actual);
        Assert.AreEqual(expected, OutputFileExtendedAttributes.WithoutCredentials(url));
    }
}
