using System.Text;
using Curl.Testing;

namespace Curl.Console;

/// <summary>Pins <see cref="ProcessUserId" />: the real user id from a <c>/proc/self/status</c> file, and 0 without one.</summary>
[TestClass]
public sealed class ProcessUserIdTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_StatusWithUidLine_ReturnsTheRealUserId()
    {
        byte[] status = Encoding.ASCII.GetBytes("Name:\tcurl\nUmask:\t0022\nUid:\t1000\t1001\t1002\t1003\nGid:\t1000\t1000\t1000\t1000\n");
        Diagnostics.Arrange("status", "Name, Umask, Uid 1000 1001 1002 1003, Gid lines");
        Diagnostics.Bytes("status", status);

        uint userId = ProcessUserId.Parse(status);
        Diagnostics.Act("user id", userId);

        Diagnostics.Assert("user id", 1000u, userId);
        Assert.AreEqual(1000u, userId);
    }

    [TestMethod]
    [DataRow("Name:\tcurl\n", DisplayName = "No Uid line")]
    [DataRow("Uid:\n", DisplayName = "Uid line without a number")]
    [DataRow("Uid:\t-1\n", DisplayName = "Uid not a number")]
    public void Parse_NoUserId_ReturnsZero(string status)
    {
        Diagnostics.Arrange("status", status.Replace("\t", "\t").Replace("\n", "\n"));

        uint userId = ProcessUserId.Parse(Encoding.ASCII.GetBytes(status));
        Diagnostics.Act("user id", userId);

        Diagnostics.Assert("user id", 0u, userId);
        Assert.AreEqual(0u, userId);
    }

    [TestMethod]
    public void Parse_NoFile_ReturnsZero()
    {
        Diagnostics.Arrange("status", "null (no file)");

        uint userId = ProcessUserId.Parse(null);
        Diagnostics.Act("user id", userId);

        Diagnostics.Assert("user id", 0u, userId);
        Assert.AreEqual(0u, userId);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.OSX)]
    public void Read_WithoutProcStatus_ReturnsZero()
    {
        Diagnostics.Arrange("status file", "none on this platform");

        uint userId = ProcessUserId.Read();
        Diagnostics.Act("user id", userId);

        Diagnostics.Assert("user id", 0u, userId);
        Assert.AreEqual(0u, userId);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void Read_OnLinux_ReturnsTheUidLinesFirstNumber()
    {
        Diagnostics.Arrange("status file", ProcessUserId.StatusPath);
        uint expected = ProcessUserId.Parse(File.ReadAllBytes(ProcessUserId.StatusPath));

        uint userId = ProcessUserId.Read();
        Diagnostics.Act("user id", userId);

        Diagnostics.Assert("user id", expected, userId);
        Assert.AreEqual(expected, userId);
    }
}
