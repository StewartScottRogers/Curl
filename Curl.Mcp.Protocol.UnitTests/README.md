# Curl.Mcp.Protocol.UnitTests

MSTest tests for [`Curl.Mcp.Protocol.UnitLibrary`](../Curl.Mcp.Protocol.UnitLibrary), the protocol
core of the hand-written MCP server. Today the one test checks that the library references no
package and no other Curl project; tests for each protocol type arrive with the type.

Run them with `dotnet test Curl.Mcp.Protocol.UnitTests --filter "TestCategory!=Integration"`.
