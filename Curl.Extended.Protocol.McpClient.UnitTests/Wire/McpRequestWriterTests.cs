using System.Text;

namespace Curl.Extended.Protocol.McpClient.Wire;

/// <summary>
/// Each test pins the bytes of one client message the Model Context Protocol 2025-06-18 defines,
/// and names the specification section that defines it.
/// </summary>
[TestClass]
public sealed class McpRequestWriterTests
{
    [TestMethod]
    public void WriteInitialize_LifecycleInitialization_WritesProtocolVersionCapabilitiesAndClientInfo()
    {
        // "Lifecycle > Initialization": the client's first request is initialize with these params.
        var request = new McpRequestWriter().WriteInitialize("curl", "8.17.0");

        Assert.AreEqual(1L, request.Id);
        AssertBytes(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"8.17.0"}}}""",
            request.Bytes);
    }

    [TestMethod]
    public void WriteInitializedNotification_LifecycleInitialization_WritesANotificationWithNoId()
    {
        // "Lifecycle > Initialization" and "Base Protocol > Overview > Notifications": no id member.
        AssertBytes("""{"jsonrpc":"2.0","method":"notifications/initialized"}""", McpRequestWriter.WriteInitializedNotification());
    }

    [TestMethod]
    public void WriteListTools_ListingToolsFirstPage_WritesNoParams()
    {
        // "Server Features > Tools > Listing Tools": the first page carries no cursor.
        var request = new McpRequestWriter().WriteListTools(cursor: null);

        AssertBytes("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", request.Bytes);
    }

    [TestMethod]
    public void WriteListTools_ListingToolsNextPage_WritesTheCursor()
    {
        // "Server Features > Tools > Listing Tools": the specification's example request.
        var request = new McpRequestWriter().WriteListTools("optional-cursor-value");

        AssertBytes("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{"cursor":"optional-cursor-value"}}""", request.Bytes);
    }

    [TestMethod]
    public void WriteCallTool_CallingTools_WritesNameAndArgumentsObject()
    {
        // "Server Features > Tools > Calling Tools": the specification's example request.
        var request = new McpRequestWriter().WriteCallTool("get_weather", """{ "location": "New York" }""");

        AssertBytes(
            """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"get_weather","arguments":{"location":"New York"}}}""",
            request.Bytes);
    }

    [TestMethod]
    public void WriteRequests_BaseProtocolRequests_TakeIncreasingIds()
    {
        // "Base Protocol > Overview > Requests": the id must not have been used before in the session.
        var writer = new McpRequestWriter();

        var ids = new[]
        {
            writer.WriteInitialize("curl", "8.17.0").Id,
            writer.WriteListTools(null).Id,
            writer.WriteCallTool("t", "{}").Id,
        };

        CollectionAssert.AreEqual(new[] { 1L, 2L, 3L }, ids);
    }

    [TestMethod]
    public void WriteCallTool_CallingToolsArgumentsNotAnObject_Throws()
    {
        // "Server Features > Tools > Calling Tools": arguments is an object.
        var exception = Assert.ThrowsExactly<ArgumentException>(() => new McpRequestWriter().WriteCallTool("t", "[1]"));

        Assert.AreEqual("argumentsJson", exception.ParamName);
    }

    [TestMethod]
    public void WriteCallTool_CallingToolsArgumentsNotJson_Throws()
    {
        // "Server Features > Tools > Calling Tools": arguments is a JSON object.
        var exception = Assert.ThrowsExactly<ArgumentException>(() => new McpRequestWriter().WriteCallTool("t", "{"));

        Assert.AreEqual("argumentsJson", exception.ParamName);
    }

    [TestMethod]
    public void WriteCallTool_CallingToolsFailedCall_DoesNotTakeAnId()
    {
        // "Base Protocol > Overview > Requests": a request never written does not use up an id.
        var writer = new McpRequestWriter();
        Assert.ThrowsExactly<ArgumentException>(() => writer.WriteCallTool("t", "1"));

        Assert.AreEqual(1L, writer.WriteListTools(null).Id);
    }

    [TestMethod]
    public void WriteMethods_LifecycleAndToolsNullArguments_Throw()
    {
        // "Lifecycle > Initialization" and "Server Features > Tools > Calling Tools": these members are required strings.
        var writer = new McpRequestWriter();

        Assert.ThrowsExactly<ArgumentNullException>(() => writer.WriteInitialize(null!, "1"));
        Assert.ThrowsExactly<ArgumentNullException>(() => writer.WriteInitialize("curl", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => writer.WriteCallTool(null!, "{}"));
        Assert.ThrowsExactly<ArgumentNullException>(() => writer.WriteCallTool("t", null!));
    }

    private static void AssertBytes(string expected, byte[] actual) =>
        Assert.AreEqual(expected, Encoding.UTF8.GetString(actual));
}
