using System.Text;

namespace Curl.Cli;

[TestClass]
public sealed class ConfigFileWireTextTests
{
    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    [TestMethod]
    public void Respell_Windows1252_EncodesBackToTheUtf8Bytes()
    {
        string respelled = ConfigFileWireText.Respell("X-A: “quoted”", Windows1252);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("X-A: “quoted”"), Windows1252.GetBytes(respelled));
    }

    [TestMethod]
    public void Respell_EncodingThatCannotCarryTheBytes_ReturnsTheTextUnchanged()
    {
        string respelled = ConfigFileWireText.Respell("“quoted”", Encoding.ASCII);

        Assert.AreEqual("“quoted”", respelled);
    }

    [TestMethod]
    public void Respell_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfigFileWireText.Respell(null!, Windows1252));
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfigFileWireText.Respell("x", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfigFileWireText.WindowsAnsiCodePage(null!));
    }

    [TestMethod]
    public void WindowsAnsiCodePage_HostNamesOne_ReturnsIt()
    {
        Encoding codePage = ConfigFileWireText.WindowsAnsiCodePage(() => Encoding.Latin1);

        Assert.AreSame(Encoding.Latin1, codePage);
    }

    [TestMethod]
    public void WindowsAnsiCodePage_FirstAskFails_AsksAgain()
    {
        int asks = 0;

        Encoding codePage = ConfigFileWireText.WindowsAnsiCodePage(() => ++asks == 1 ? null : Encoding.Latin1);

        Assert.AreSame(Encoding.Latin1, codePage);
        Assert.AreEqual(2, asks);
    }

    [TestMethod]
    public void WindowsAnsiCodePage_HostNamesNone_ReturnsWindows1252()
    {
        Encoding codePage = ConfigFileWireText.WindowsAnsiCodePage(() => null);

        Assert.AreEqual(1252, codePage.CodePage);
    }
}
