using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// CGR-205: <c>-OJ</c> with a 300-character <c>Content-Disposition</c> filename warns
/// <c>File name too long</c> on Linux and macOS, as curl does, and <c>Invalid argument</c> on Windows.
/// </summary>
[TestClass]
public sealed class Cgr205Tests
{
    [TestMethod]
    [DataRow(false, "File name too long")]
    [DataRow(true, "Invalid argument")]
    public void ReasonFor_NameTooLong_IsTheRuntimesStrerrorText(bool windowsRuntime, string expected)
    {
        Assert.AreEqual(expected, OutputFileOpenWarning.ReasonFor(FileAccessStatus.NameTooLong, windowsRuntime));
    }

    [TestMethod]
    public void ReasonFor_NameTooLongOnThisPlatform_MatchesItsRuntime()
    {
        string expected = OperatingSystem.IsWindows() ? "Invalid argument" : "File name too long";

        Assert.AreEqual(expected, OutputFileOpenWarning.ReasonFor(FileAccessStatus.NameTooLong));
    }
}
