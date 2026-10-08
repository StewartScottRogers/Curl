using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="CurlEasyErrorText" /> against the texts read from curl 8.21.0's
/// <c>libcurl-4.dll</c> (BL-440 Notes).
/// </summary>
[TestClass]
public sealed class CurlEasyErrorTextTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(CurlExitCode.CouldntConnect, "Could not connect to server")]
    [DataRow(CurlExitCode.ReadError, "Failed to open/read local data from file/application")]
    [DataRow(CurlExitCode.HttpReturnedError, "HTTP response code said error")]
    [DataRow(CurlExitCode.SslEngineInitFailed, "Failed to initialize SSL crypto engine")]
    [DataRow(CurlExitCode.EchRequired, "ECH attempted but failed")]
    public void Of_NamedCode_ReturnsLibcurlsText(CurlExitCode exitCode, string expected)
    {
        Diagnostics.Arrange("exit code", $"{exitCode} ({(int)exitCode})");

        string text = CurlEasyErrorText.Of(exitCode);
        Diagnostics.Act("error text", text);

        Diagnostics.Assert("error text", expected, text);
        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    public void Of_EveryCurlExitCode_HasATextOfItsOwn()
    {
        CurlExitCode[] codes = Enum.GetValues<CurlExitCode>();
        Diagnostics.Arrange("exit codes", codes.Length);

        string[] texts = [.. codes.Select(CurlEasyErrorText.Of)];
        Diagnostics.Act("distinct texts", texts.Distinct(StringComparer.Ordinal).Count());

        Diagnostics.Assert("texts that are the unknown error", 0, texts.Count(text => text == CurlEasyErrorText.UnknownError));
        Diagnostics.Assert("distinct texts", texts.Length, texts.Distinct(StringComparer.Ordinal).Count());
        CollectionAssert.DoesNotContain(texts, CurlEasyErrorText.UnknownError);
        CollectionAssert.AllItemsAreUnique(texts);
    }

    [TestMethod]
    public void Of_CodeLibcurlDoesNotName_ReturnsUnknownError()
    {
        Diagnostics.Arrange("exit code", 20);

        string text = CurlEasyErrorText.Of((CurlExitCode)20);
        Diagnostics.Act("error text", text);

        Diagnostics.Assert("error text", "Unknown error", text);
        Assert.AreEqual("Unknown error", text);
    }
}
