namespace Curl.Cli;

/// <summary>
/// Pins the warning lines curl 8.21.0 prints while reading a command line it carries on with,
/// byte for byte (measured with the local curl on 2026-09-26).
/// </summary>
[TestClass]
public sealed class CommandLineWarningTests
{
    [TestMethod]
    public void FileNameLooksLikeFlag_Value_IsCurlsExactText()
    {
        string line = CommandLineWarning.FileNameLooksLikeFlag("-s");

        Assert.AreEqual("Warning: The filename argument '-s' looks like a flag.", line);
    }

    [TestMethod]
    public void FileNameLooksLikeFlag_Null_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineWarning.FileNameLooksLikeFlag(null!));

        Assert.AreEqual("fileName", exception.ParamName);
    }
}
