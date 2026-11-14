namespace Curl.Extended.Protocol.Plugin;

/// <summary>Checks that a URL holding a cmd.exe metacharacter never reaches a .cmd or .bat helper.</summary>
[TestClass]
public sealed class BatchHelperArgumentTests
{
    [TestMethod]
    [DataRow("curl-proto-demo.cmd", "demo://host/a&echo.>pwn1.txt")]
    [DataRow("C:/bin/curl-proto-demo.BAT", "demo://host/a\"&echo.>pwn2.txt&\"")]
    [DataRow("/bin/curl-proto-demo.cmd", "demo://host/%PATH%")]
    [DataRow("/bin/curl-proto-demo.cmd", "demo://host/a|b")]
    [DataRow("/bin/curl-proto-demo.cmd", "demo://host/a<b")]
    [DataRow("/bin/curl-proto-demo.cmd", "demo://host/a^b")]
    [DataRow("/bin/curl-proto-demo.cmd", "demo://host/a\nb")]
    [DataRow("srv.cmd ", "demo://host/a&b")]
    [DataRow("srv.cmd.", "demo://host/a&b")]
    [DataRow("srv.cmd\u0001", "demo://host/a&b")]
    [DataRow("srv.bat\t", "demo://host/a&b")]
    [DataRow("srv.bat\u000B", "demo://host/a&b")]
    [DataRow("srv.bat  .", "demo://host/a&b")]
    public void RefuseUnsafeBatchArgument_BatchHelperAndMetacharacter_Refuses(string program, string url)
    {
        Assert.ThrowsExactly<IOException>(() => SystemHelperProcessStarter.RefuseUnsafeBatchArgument(program, url));
    }

    [TestMethod]
    public void RefuseUnsafeBatchArgument_BatchHelperAndCleanUrl_DoesNotRefuse()
    {
        SystemHelperProcessStarter.RefuseUnsafeBatchArgument("/bin/curl-proto-demo.cmd", "demo://host/path?a=1#frag");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" \t.")]
    public void RefuseUnsafeBatchArgument_NoProgramName_DoesNotRefuse(string program)
    {
        SystemHelperProcessStarter.RefuseUnsafeBatchArgument(program, "demo://host/a&b");
    }

    [TestMethod]
    public void RefuseUnsafeBatchArgument_NonBatchHelper_DoesNotRefuse()
    {
        SystemHelperProcessStarter.RefuseUnsafeBatchArgument("/bin/curl-proto-demo.exe", "demo://host/a&b\"%");
        SystemHelperProcessStarter.RefuseUnsafeBatchArgument("/bin/curl-proto-demo", "demo://host/a&b");
    }
}
