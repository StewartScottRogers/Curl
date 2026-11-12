namespace Curl.Cli;

/// <summary>Pins the bytes a <c>-T</c> file name is percent-encoded in when appended to the URL (BL-4928).</summary>
[TestClass]
public sealed class UploadUrlCodePageTests
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void AppendLocalFileName_Windows_UsesAnsiBytes()
    {
        string url = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile("http://h/up/", "ünï.txt");

        Assert.AreEqual("http://h/up/%FCn%EF.txt", url);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void AppendLocalFileName_NotWindows_UsesUtf8Bytes()
    {
        string url = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile("http://h/up/", "ünï.txt");

        Assert.AreEqual("http://h/up/%C3%BCn%C3%AF.txt", url);
    }

    [TestMethod]
    public void EscapeBaseName_Utf8_EncodesAsUtf8()
    {
        Assert.AreEqual("%C3%BCn%C3%AF.txt", UploadUrl.EscapeBaseName("ünï.txt", false));
    }
}
