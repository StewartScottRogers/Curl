using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="CurlEasyErrorText" /> against the texts read from curl 8.21.0's
/// <c>libcurl-4.dll</c> (BL-440 Notes).
/// </summary>
[TestClass]
public sealed class CurlEasyErrorTextTests
{
    [TestMethod]
    [DataRow(CurlExitCode.CouldntConnect, "Could not connect to server")]
    [DataRow(CurlExitCode.ReadError, "Failed to open/read local data from file/application")]
    [DataRow(CurlExitCode.HttpReturnedError, "HTTP response code said error")]
    [DataRow(CurlExitCode.SslEngineInitFailed, "Failed to initialize SSL crypto engine")]
    [DataRow(CurlExitCode.EchRequired, "ECH attempted but failed")]
    public void Of_NamedCode_ReturnsLibcurlsText(CurlExitCode exitCode, string expected)
    {
        string text = CurlEasyErrorText.Of(exitCode);

        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    public void Of_EveryCurlExitCode_HasATextOfItsOwn()
    {
        CurlExitCode[] codes = Enum.GetValues<CurlExitCode>();

        string[] texts = [.. codes.Select(CurlEasyErrorText.Of)];

        CollectionAssert.DoesNotContain(texts, CurlEasyErrorText.UnknownError);
        CollectionAssert.AllItemsAreUnique(texts);
    }

    [TestMethod]
    public void Of_CodeLibcurlDoesNotName_ReturnsUnknownError()
    {
        string text = CurlEasyErrorText.Of((CurlExitCode)20);

        Assert.AreEqual("Unknown error", text);
    }
}
