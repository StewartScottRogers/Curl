namespace Curl.Mcp;

[TestClass]
public sealed class McpResponseCapTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void FromEnvironment_UnsetOrEmpty_GivesOneMebibyte(string? value)
    {
        McpResponseCap cap = McpResponseCap.FromEnvironment(_ => value);

        Assert.AreEqual(1048576, cap.MaxBytes);
    }

    [TestMethod]
    public void FromEnvironment_ReadsTheNamedVariable()
    {
        string? askedFor = null;

        McpResponseCap cap = McpResponseCap.FromEnvironment(name =>
        {
            askedFor = name;
            return "4096";
        });

        Assert.AreEqual("CURL_MCP_MAX_RESPONSE_BYTES", askedFor);
        Assert.AreEqual(4096, cap.MaxBytes);
    }

    [TestMethod]
    [DataRow("abc")]
    [DataRow("0")]
    [DataRow("-5")]
    [DataRow("1.5")]
    [DataRow(" 10")]
    [DataRow("99999999999")]
    public void FromEnvironment_NotAPositiveWholeNumber_FailsTheStartWithANamedError(string value)
    {
        McpConfigurationException error = Assert.ThrowsExactly<McpConfigurationException>(
            () => McpResponseCap.FromEnvironment(_ => value));

        Assert.AreEqual(
            $"CURL_MCP_MAX_RESPONSE_BYTES must be a positive whole number of bytes; got '{value}'.",
            error.Message);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Constructor_ZeroOrNegative_Throws(int maxBytes)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new McpResponseCap(maxBytes));
    }
}
