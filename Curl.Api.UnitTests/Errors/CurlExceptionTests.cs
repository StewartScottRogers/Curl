using Curl.Api.Codes;
using Curl.Api.Errors;

namespace Curl.Api.UnitTests.Errors;

[TestClass]
public sealed class CurlExceptionTests
{
    [TestMethod]
    public void Constructor_FailedCode_KeepsCodeAndMessage()
    {
        var exception = new CurlException(CurlCode.CouldntResolveHost, "Could not resolve host: example.invalid");

        Assert.AreEqual(CurlCode.CouldntResolveHost, exception.Code);
        Assert.AreEqual("Could not resolve host: example.invalid", exception.Message);
    }

    [TestMethod]
    public void Constructor_Ok_ThrowsArgumentOutOfRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlException(CurlCode.Ok, "No error"));
    }

    [TestMethod]
    public void ThrowIfFailed_Ok_DoesNotThrow()
    {
        CurlException.ThrowIfFailed(CurlCode.Ok, "ignored");
        CurlException.ThrowIfFailed(CurlCode.Ok, string.Empty);
    }

    [TestMethod]
    public void ThrowIfFailed_EveryFailedCodeWithEmptyErrorText_ThrowsWithCodeAndStrerrorText()
    {
        foreach (CurlCode code in Enum.GetValues<CurlCode>().Where(code => code != CurlCode.Ok))
        {
            CurlException exception = Assert.ThrowsExactly<CurlException>(() => CurlException.ThrowIfFailed(code, string.Empty));

            Assert.AreEqual(code, exception.Code, $"CurlCode.{code}");
            Assert.AreEqual(CurlCodeText.Of(code), exception.Message, $"CurlCode.{code}");
        }
    }

    [TestMethod]
    public void ThrowIfFailed_FailedCodeWithErrorText_UsesTheErrorText()
    {
        CurlException exception = Assert.ThrowsExactly<CurlException>(
            () => CurlException.ThrowIfFailed(CurlCode.CouldntConnect, "Failed to connect to localhost port 1"));

        Assert.AreEqual(CurlCode.CouldntConnect, exception.Code);
        Assert.AreEqual("Failed to connect to localhost port 1", exception.Message);
    }

    [TestMethod]
    public void ThrowIfFailed_NullErrorText_ThrowsArgumentNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlException.ThrowIfFailed(CurlCode.Ok, null!));
    }
}
