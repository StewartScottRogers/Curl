using System.Text;

namespace Curl.Console;

/// <summary>Pins <see cref="ProcessUserId" />: the real user id from a <c>/proc/self/status</c> file, and 0 without one.</summary>
[TestClass]
public sealed class ProcessUserIdTests
{
    [TestMethod]
    public void Parse_StatusWithUidLine_ReturnsTheRealUserId()
    {
        Assert.AreEqual(1000u, ProcessUserId.Parse(Encoding.ASCII.GetBytes("Name:\tcurl\nUmask:\t0022\nUid:\t1000\t1001\t1002\t1003\nGid:\t1000\t1000\t1000\t1000\n")));
    }

    [TestMethod]
    [DataRow("Name:\tcurl\n", DisplayName = "No Uid line")]
    [DataRow("Uid:\n", DisplayName = "Uid line without a number")]
    [DataRow("Uid:\t-1\n", DisplayName = "Uid not a number")]
    public void Parse_NoUserId_ReturnsZero(string status)
    {
        Assert.AreEqual(0u, ProcessUserId.Parse(Encoding.ASCII.GetBytes(status)));
    }

    [TestMethod]
    public void Parse_NoFile_ReturnsZero()
    {
        Assert.AreEqual(0u, ProcessUserId.Parse(null));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.OSX)]
    public void Read_WithoutProcStatus_ReturnsZero()
    {
        Assert.AreEqual(0u, ProcessUserId.Read());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void Read_OnLinux_ReturnsTheUidLinesFirstNumber()
    {
        Assert.AreEqual(ProcessUserId.Parse(File.ReadAllBytes(ProcessUserId.StatusPath)), ProcessUserId.Read());
    }
}
