using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--ip-tos</c> and <c>--vlan-priority</c>. Measured against the local
/// curl 8.21.0 (Schannel, Windows) on 2026-10-01; every case, with the bytes curl printed, is in BL-646's
/// Notes.
/// </summary>
[TestClass]
public sealed class CommandLineQualityOfServiceTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    [TestMethod]
    public void Parse_NeitherOption_LeavesBothZero()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(0, result.Options.IpTypeOfService);
        Assert.AreEqual(0, result.Options.VlanPriority);
    }

    [TestMethod]
    [DataRow("AF11", 0x28)]
    [DataRow("AF12", 0x30)]
    [DataRow("AF13", 0x38)]
    [DataRow("AF21", 0x48)]
    [DataRow("AF22", 0x50)]
    [DataRow("AF23", 0x58)]
    [DataRow("AF31", 0x68)]
    [DataRow("AF32", 0x70)]
    [DataRow("AF33", 0x78)]
    [DataRow("AF41", 0x88)]
    [DataRow("AF42", 0x90)]
    [DataRow("AF43", 0x98)]
    [DataRow("CE", 0x03)]
    [DataRow("CS0", 0x00)]
    [DataRow("CS1", 0x20)]
    [DataRow("CS2", 0x40)]
    [DataRow("CS3", 0x60)]
    [DataRow("CS4", 0x80)]
    [DataRow("CS5", 0xa0)]
    [DataRow("CS6", 0xc0)]
    [DataRow("CS7", 0xe0)]
    [DataRow("ECT0", 0x02)]
    [DataRow("ECT1", 0x01)]
    [DataRow("EF", 0xb8)]
    [DataRow("LE", 0x04)]
    [DataRow("LOWCOST", 0x02)]
    [DataRow("LOWDELAY", 0x10)]
    [DataRow("MINCOST", 0x02)]
    [DataRow("RELIABILITY", 0x04)]
    [DataRow("THROUGHPUT", 0x08)]
    public void Parse_IpTosName_RecordsItsByte(string name, int expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--ip-tos", name, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.IpTypeOfService);
    }

    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("-0", 0)]
    [DataRow("184", 184)]
    [DataRow("255", 255)]
    public void Parse_IpTosNumber_RecordsIt(string value, int expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--ip-tos", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.IpTypeOfService);
    }

    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("-0", 0)]
    [DataRow("07", 7)]
    [DataRow("3", 3)]
    public void Parse_VlanPriority_RecordsIt(string value, int expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--vlan-priority", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.VlanPriority);
    }

    [TestMethod]
    public void Parse_EachOptionTwice_LastOneWins()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--ip-tos", "CS1", "--vlan-priority", "3", "--ip-tos=EF", "--vlan-priority=5", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(0xb8, result.Options.IpTypeOfService);
        Assert.AreEqual(5, result.Options.VlanPriority);
    }

    [TestMethod]
    [DataRow("--ip-tos", "300", "too large number")]
    [DataRow("--ip-tos", "256", "too large number")]
    [DataRow("--ip-tos", "bogus", "expected a proper numerical parameter")]
    [DataRow("--ip-tos", "cs1", "expected a proper numerical parameter")]
    [DataRow("--ip-tos", "-1", "expected a positive numerical parameter")]
    [DataRow("--vlan-priority", "9", "too large number")]
    [DataRow("--vlan-priority", "8", "too large number")]
    [DataRow("--vlan-priority", "x", "expected a proper numerical parameter")]
    [DataRow("--vlan-priority", "+3", "expected a proper numerical parameter")]
    [DataRow("--vlan-priority", "3x", "expected a proper numerical parameter")]
    [DataRow("--vlan-priority", "", "expected a proper numerical parameter")]
    [DataRow("--vlan-priority", "99999999999999999999", "expected a proper numerical parameter")]
    [DataRow("--vlan-priority", "-1", "expected a positive numerical parameter")]
    public void Parse_BadValue_RefusedAsCurlRefusesIt(string option, string value, string reason)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, value, Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { $"curl: option {option}: {reason}", TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }
}
