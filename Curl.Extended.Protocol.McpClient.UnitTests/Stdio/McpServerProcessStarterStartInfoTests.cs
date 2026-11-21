namespace Curl.Extended.Protocol.McpClient.Stdio;

[TestClass]
public sealed class McpServerProcessStarterStartInfoTests
{
    [TestMethod]
    public void BuildStartInfo_RedirectsInputAndOutputWithoutAShell()
    {
        var startInfo = McpServerProcessStarter.BuildStartInfo("server", ["--a", "b c"]);

        Assert.IsFalse(startInfo.UseShellExecute);
        Assert.IsTrue(startInfo.RedirectStandardInput);
        Assert.IsTrue(startInfo.RedirectStandardOutput);
        Assert.IsFalse(startInfo.RedirectStandardError);
        CollectionAssert.AreEqual(new[] { "--a", "b c" }, startInfo.ArgumentList.ToArray());
    }
}
