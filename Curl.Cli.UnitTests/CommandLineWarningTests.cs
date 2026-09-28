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

    [TestMethod]
    public void MoreOutputOptionsThanUrls_IsCurlsExactText()
    {
        Assert.AreEqual("Warning: Got more output options than URLs", CommandLineWarning.MoreOutputOptionsThanUrls);
    }

    [TestMethod]
    public void TimeConditionIsNotADate_IsCurlsExactUnwrappedLine()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Illegal date format for -z, --time-cond (and not a filename). Disabling time condition. See curl_getdate(3) for valid date syntax.",
            },
            CommandLineWarning.TimeConditionIsNotADate.ToArray());
    }

    [TestMethod]
    public void PostRequestedWithHead_IsCurlsExactUnwrappedLine()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: You can only select one HTTP request method! You asked for both POST (-d, --data) and HEAD (-I, --head).",
            },
            CommandLineWarning.PostRequestedWithHead.ToArray());
    }

    [TestMethod]
    public void PostRequestedWithGet_IsCurlsExactUnwrappedLine()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: You can only select one HTTP request method! You asked for both POST (-d, --data) and GET (-G, --get).",
            },
            CommandLineWarning.PostRequestedWithGet.ToArray());
    }
}
