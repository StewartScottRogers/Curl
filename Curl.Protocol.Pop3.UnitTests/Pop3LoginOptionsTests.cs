namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins how <see cref="Pop3LoginOptions" /> reads the login options, as measured on curl
/// 8.21.0 in BL-548: <c>AUTH=USER</c> and <c>FOO=bar</c> are exit 3, <c>auth=+apop</c> works.
/// </summary>
[TestClass]
public sealed class Pop3LoginOptionsTests
{
    [TestMethod]
    [DataRow(null, "Any", null)]
    [DataRow("", "Any", null)]
    [DataRow("AUTH=*", "Any", null)]
    [DataRow("AUTH=+APOP", "Apop", null)]
    [DataRow("auth=+apop", "Apop", null)]
    [DataRow("Auth=plain", "Sasl", "plain")]
    [DataRow("AUTH=SCRAM-SHA-256", "Sasl", "SCRAM-SHA-256")]
    [DataRow("AUTH=PLAIN;AUTH=+APOP", "Apop", null)]
    [DataRow("AUTH=+APOP;", "Apop", null)]
    public void Read_LoginOptions_AllowTheirMethod(string? options, string method, string? mechanism)
    {
        Pop3LoginOptions? read = Pop3LoginOptions.Read(options);

        Assert.AreEqual(new Pop3LoginOptions(Enum.Parse<Pop3LoginMethod>(method), mechanism), read);
    }

    [TestMethod]
    [DataRow("AUTH=USER")]
    [DataRow("AUTH=")]
    [DataRow("AUTH")]
    [DataRow("FOO=bar")]
    [DataRow("AUTH=PLAIN;FOO=bar")]
    public void Read_LoginOptionsCurlRefuses_IsNull(string options)
    {
        Pop3LoginOptions? read = Pop3LoginOptions.Read(options);

        Assert.IsNull(read);
    }
}
