namespace Curl.Engine;

/// <summary>Tests for <see cref="EngineInterfaceBinding" />, which splits a <c>--interface</c> value as <c>CURLOPT_INTERFACE</c> does.</summary>
[TestClass]
public sealed class EngineInterfaceBindingTests
{
    [TestMethod]
    [DataRow("eth0")]
    [DataRow("127.0.0.1")]
    [DataRow("IF!eth0")]
    public void Parse_NoPrefix_IsTriedAsInterfaceOrHost(string value)
    {
        EngineInterfaceBinding binding = EngineInterfaceBinding.Parse(value);

        Assert.AreEqual(value, binding.Value);
        Assert.AreEqual(value, binding.InterfaceOrHostName);
        Assert.IsNull(binding.InterfaceName);
        Assert.IsNull(binding.HostName);
        Assert.IsFalse(binding.IsMalformed);
    }

    [TestMethod]
    [DataRow("if!eth0", "eth0", null)]
    [DataRow("host!127.0.0.1", null, "127.0.0.1")]
    [DataRow("ifhost!lo!127.0.0.1", "lo", "127.0.0.1")]
    [DataRow("ifhost!!h", "", "h")]
    [DataRow("ifhost!lo!a!b", "lo", "a!b")]
    public void Parse_Prefix_NamesInterfaceHostOrBoth(string value, string? interfaceName, string? hostName)
    {
        EngineInterfaceBinding binding = EngineInterfaceBinding.Parse(value);

        Assert.IsNull(binding.InterfaceOrHostName);
        Assert.AreEqual(interfaceName, binding.InterfaceName);
        Assert.AreEqual(hostName, binding.HostName);
        Assert.IsFalse(binding.IsMalformed);
    }

    [TestMethod]
    public void Parse_LongestNamesAndLongHosts_AreNotMalformed()
    {
        string name = new('a', 254);
        string longName = new('a', 255);

        Assert.IsFalse(EngineInterfaceBinding.Parse(name).IsMalformed);
        Assert.IsFalse(EngineInterfaceBinding.Parse("if!" + name).IsMalformed);
        Assert.IsFalse(EngineInterfaceBinding.Parse("host!" + longName).IsMalformed);
        Assert.IsFalse(EngineInterfaceBinding.Parse("ifhost!lo!" + longName).IsMalformed);
        Assert.IsFalse(EngineInterfaceBinding.Parse("ifhost!" + longName + "!h").IsMalformed);
    }

    [TestMethod]
    [DataRow("if!")]
    [DataRow("host!")]
    [DataRow("ifhost!")]
    [DataRow("ifhost!eth0")]
    [DataRow("ifhost!eth0!")]
    public void Parse_ValueLibcurlRefuses_IsMarkedMalformedWithNoNames(string value)
    {
        EngineInterfaceBinding binding = EngineInterfaceBinding.Parse(value);

        Assert.AreEqual(value, binding.Value);
        Assert.IsTrue(binding.IsMalformed);
        Assert.IsNull(binding.InterfaceOrHostName);
        Assert.IsNull(binding.InterfaceName);
        Assert.IsNull(binding.HostName);
    }

    [TestMethod]
    public void Parse_NameOrInterfaceTooLong_IsMalformed()
    {
        string tooLong = new('a', 255);

        Assert.IsTrue(EngineInterfaceBinding.Parse(tooLong).IsMalformed);
        Assert.IsTrue(EngineInterfaceBinding.Parse("if!" + tooLong).IsMalformed);
    }

    [TestMethod]
    public void Parse_NullValue_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => EngineInterfaceBinding.Parse(null!));
}
