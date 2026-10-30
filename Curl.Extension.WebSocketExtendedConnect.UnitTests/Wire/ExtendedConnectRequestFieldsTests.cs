using Curl.Extension.WebSocketExtendedConnect.Wire;
using Curl.Http2;

namespace Curl.Extension.WebSocketExtendedConnect.UnitTests.Wire;

[TestClass]
public sealed class ExtendedConnectRequestFieldsTests
{
    [TestMethod]
    public void Build_Rfc8441Section4_GivesConnectWithProtocolSchemePathAuthorityAndVersion()
    {
        IReadOnlyList<HeaderField> fields = ExtendedConnectRequestFields.Build("https", "/chat?room=1", "server.example.com", []);

        CollectionAssert.AreEqual(
            new HeaderField[]
            {
                new(":method", "CONNECT"),
                new(":protocol", "websocket"),
                new(":scheme", "https"),
                new(":path", "/chat?room=1"),
                new(":authority", "server.example.com"),
                new("sec-websocket-version", "13"),
            },
            fields.ToArray());
    }

    [TestMethod]
    public void Build_Rfc9220Section3_GivesTheSameFieldsOverHttp3()
    {
        IReadOnlyList<HeaderField> fields = ExtendedConnectRequestFields.Build("http", "/", "server.example.com:8080", []);

        Assert.AreEqual(new HeaderField(":scheme", "http"), fields[2]);
        Assert.AreEqual(new HeaderField(":authority", "server.example.com:8080"), fields[4]);
        Assert.IsFalse(fields.Any(field => field.Name == "sec-websocket-key"));
    }

    [TestMethod]
    public void Build_WithSubprotocols_AddsThemCommaSeparatedLast()
    {
        IReadOnlyList<HeaderField> fields = ExtendedConnectRequestFields.Build("https", "/", "h", ["chat", "superchat"]);

        Assert.HasCount(7, fields);
        Assert.AreEqual(new HeaderField("sec-websocket-protocol", "chat, superchat"), fields[6]);
    }
}
