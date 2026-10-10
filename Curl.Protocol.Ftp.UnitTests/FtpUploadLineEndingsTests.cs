namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins when an FTP upload's line feeds become CRLF: under <c>--crlf</c> everywhere, and in
/// ASCII mode everywhere but Windows, as upstream's test475 expects (BL-1957).
/// </summary>
[TestClass]
public sealed class FtpUploadLineEndingsTests
{
    [TestMethod]
    [DataRow(true, false, true)]
    [DataRow(true, false, false)]
    [DataRow(true, true, true)]
    [DataRow(true, true, false)]
    public void AreConverted_UnderCrlf_ReturnsTrueOnEveryPlatform(bool convertLineEndings, bool useAscii, bool runsOnWindows)
    {
        bool converted = FtpUploadLineEndings.AreConverted(convertLineEndings, useAscii, runsOnWindows);

        Assert.IsTrue(converted);
    }

    [TestMethod]
    public void AreConverted_AsciiOffWindows_ReturnsTrue()
    {
        bool converted = FtpUploadLineEndings.AreConverted(convertLineEndings: false, useAscii: true, runsOnWindows: false);

        Assert.IsTrue(converted);
    }

    [TestMethod]
    public void AreConverted_AsciiOnWindows_ReturnsFalse()
    {
        bool converted = FtpUploadLineEndings.AreConverted(convertLineEndings: false, useAscii: true, runsOnWindows: true);

        Assert.IsFalse(converted);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void AreConverted_BinaryWithoutCrlf_ReturnsFalse(bool runsOnWindows)
    {
        bool converted = FtpUploadLineEndings.AreConverted(convertLineEndings: false, useAscii: false, runsOnWindows);

        Assert.IsFalse(converted);
    }
}
