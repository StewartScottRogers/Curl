using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--interface</c> and <c>--local-port</c> as curl 8.21.0 parses them: every <c>--local-port</c> form
/// and refusal, and every <c>--interface</c> prefix, measured on Windows on 2026-09-28 with
/// <c>Record-CurlExchange.ps1</c> (BL-599 Notes).
/// </summary>
[TestClass]
public sealed class CommandLineInterfaceAndLocalPortOptionTests
{
    private const string Url = "http://127.0.0.1:1/";
    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    [TestMethod]
    public void Parse_WithoutEitherOption_LeavesThemUnset()
    {
        CommandLineOptions options = Accept();

        Assert.IsNull(options.Interface);
        Assert.IsNull(options.LocalPorts);
    }

    [TestMethod]
    [DataRow("0", 0, 0)]
    [DataRow("65535", 65535, 65535)]
    [DataRow("000000000000000000000000005", 5, 5)]
    [DataRow("3000-3005", 3000, 3005)]
    [DataRow("0-0", 0, 0)]
    [DataRow("0-5", 0, 5)]
    [DataRow("5-5", 5, 5)]
    [DataRow("5 - 6", 5, 6)]
    [DataRow("5 -6", 5, 6)]
    [DataRow("5- 6", 5, 6)]
    [DataRow("5\t-6", 5, 6)]
    [DataRow("5-\t6", 5, 6)]
    [DataRow("5-000000000000000000000000006", 5, 6)]
    public void Parse_LocalPort_RecordsTheRange(string value, int first, int last)
    {
        Assert.AreEqual(new LocalPortRange(first, last), Accept("--local-port", value).LocalPorts);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("abc")]
    [DataRow("70000")]
    [DataRow("99999999999999999999")]
    [DataRow("-1")]
    [DataRow("-6")]
    [DataRow(" -6")]
    [DataRow("-")]
    [DataRow("+5")]
    [DataRow(" 5")]
    [DataRow(" 5-6")]
    [DataRow("5x")]
    [DataRow("5 6")]
    [DataRow("5 ")]
    [DataRow("5\t")]
    [DataRow("5-")]
    [DataRow("5- ")]
    [DataRow("5-3")]
    [DataRow("5-0")]
    [DataRow("5-abc")]
    [DataRow("5-6x")]
    [DataRow("5-6 ")]
    [DataRow("5--6")]
    [DataRow("5-+6")]
    [DataRow("5- -6")]
    [DataRow("5  - 6")]
    [DataRow("5-  6")]
    [DataRow("5-70000")]
    [DataRow("65535-65536")]
    public void Parse_MalformedLocalPort_IsRefusedAsBadlyUsed(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--local-port", value, Url]);

        AssertRefused(result, "curl: option --local-port: is badly used here");
    }

    [TestMethod]
    public void Parse_LocalPortRange_CountsItsPorts()
    {
        Assert.AreEqual(6, Accept("--local-port", "3000-3005").LocalPorts!.Value.Count);
    }

    [TestMethod]
    public void Parse_LocalPortWithAttachedValue_RecordsTheRange()
    {
        Assert.AreEqual(new LocalPortRange(8, 9), Accept("--local-port=8-9").LocalPorts);
    }

    [TestMethod]
    public void Parse_EmptyInterface_IsRefusedAsBlank()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--interface", "", Url]);

        AssertRefused(result, "curl: option --interface: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("eth0")]
    [DataRow("127.0.0.1")]
    [DataRow(" ")]
    [DataRow("IF!eth0")]
    public void Parse_InterfaceWithoutPrefix_IsTriedAsInterfaceOrHost(string value)
    {
        InterfaceBinding binding = Accept("--interface", value).Interface!;

        Assert.AreEqual(value, binding.Value);
        Assert.AreEqual(value, binding.InterfaceOrHostName);
        Assert.IsNull(binding.InterfaceName);
        Assert.IsNull(binding.HostName);
        Assert.IsFalse(binding.IsMalformed);
    }

    [TestMethod]
    public void Parse_InterfaceWithIfPrefix_NamesAnInterfaceOnly()
    {
        InterfaceBinding binding = Accept("--interface", "if!eth0").Interface!;

        AssertBinding(binding, interfaceName: "eth0", hostName: null);
    }

    [TestMethod]
    public void Parse_InterfaceWithHostPrefix_NamesAHostOnly()
    {
        InterfaceBinding binding = Accept("--interface", "host!127.0.0.1").Interface!;

        AssertBinding(binding, interfaceName: null, hostName: "127.0.0.1");
    }

    [TestMethod]
    [DataRow("ifhost!lo!127.0.0.1", "lo", "127.0.0.1")]
    [DataRow("ifhost!!h", "", "h")]
    [DataRow("ifhost!lo!a!b", "lo", "a!b")]
    public void Parse_InterfaceWithIfHostPrefix_NamesBoth(string value, string interfaceName, string hostName)
    {
        InterfaceBinding binding = Accept("--interface", value).Interface!;

        AssertBinding(binding, interfaceName, hostName);
    }

    [TestMethod]
    public void Parse_LongHostAfterPrefixes_IsNotMalformed()
    {
        string longName = new('a', 255);

        Assert.IsFalse(Accept("--interface", "host!" + longName).Interface!.IsMalformed);
        Assert.IsFalse(Accept("--interface", "ifhost!lo!" + longName).Interface!.IsMalformed);
        Assert.IsFalse(Accept("--interface", "ifhost!" + longName + "!h").Interface!.IsMalformed);
    }

    [TestMethod]
    public void Parse_NameOfTheLongestLength_IsNotMalformed()
    {
        string name = new('a', 254);

        Assert.IsFalse(Accept("--interface", name).Interface!.IsMalformed);
        Assert.IsFalse(Accept("--interface", "if!" + name).Interface!.IsMalformed);
    }

    [TestMethod]
    [DataRow("if!")]
    [DataRow("host!")]
    [DataRow("ifhost!")]
    [DataRow("ifhost!eth0")]
    [DataRow("ifhost!eth0!")]
    public void Parse_InterfaceLibcurlRefuses_IsAcceptedAndMarkedMalformed(string value)
    {
        InterfaceBinding binding = Accept("--interface", value).Interface!;

        Assert.AreEqual(value, binding.Value);
        Assert.IsTrue(binding.IsMalformed);
        Assert.IsNull(binding.InterfaceOrHostName);
        Assert.IsNull(binding.InterfaceName);
        Assert.IsNull(binding.HostName);
    }

    [TestMethod]
    public void Parse_NameLongerThanLibcurlAccepts_IsMarkedMalformed()
    {
        string name = new('a', 255);

        Assert.IsTrue(Accept("--interface", name).Interface!.IsMalformed);
        Assert.IsTrue(Accept("--interface", "if!" + name).Interface!.IsMalformed);
    }

    [TestMethod]
    public void Parse_BothGivenTwice_KeepTheLast()
    {
        CommandLineOptions options = Accept("--interface", "eth0", "--interface", "eth1", "--local-port", "1", "--local-port", "2");

        Assert.AreEqual("eth1", options.Interface!.Value);
        Assert.AreEqual(new LocalPortRange(2, 2), options.LocalPorts);
    }

    [TestMethod]
    [DataRow("--interface")]
    [DataRow("--local-port")]
    public void Parse_OptionLast_IsRefusedAsNeedingParameter(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, option]);

        AssertRefused(result, $"curl: option {option}: requires parameter");
    }

    [TestMethod]
    [DataRow("interface")]
    [DataRow("local-port")]
    public void Parse_NegatedOption_IsRefusedAsNotReversible(string longName)
    {
        CommandLineParseResult result = CommandLineParser.Parse([$"--no-{longName}", "1", Url]);

        AssertRefused(result, $"curl: option --no-{longName}: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_BeforeNext_DoesNotReachTheNextGroup()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--interface", "eth0", "--local-port", "9", Url, "--next", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNotNull(result.Groups[0].Interface);
        Assert.IsNull(result.Groups[1].Interface);
        Assert.IsNull(result.Groups[1].LocalPorts);
    }

    [TestMethod]
    public void TryParse_NullValue_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LocalPortRange.TryParse(null!, out _));
    }

    [TestMethod]
    public void InterfaceBindingParse_NullValue_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => InterfaceBinding.Parse(null!));
    }

    private static void AssertBinding(InterfaceBinding binding, string? interfaceName, string? hostName)
    {
        Assert.IsNull(binding.InterfaceOrHostName);
        Assert.AreEqual(interfaceName, binding.InterfaceName);
        Assert.AreEqual(hostName, binding.HostName);
        Assert.IsFalse(binding.IsMalformed);
    }

    private static CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private static void AssertRefused(CommandLineParseResult result, string optionLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { optionLine, TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }
}
