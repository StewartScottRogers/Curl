# Curl.Mcp.UnitTests

The MSTest tests of both `Curl.Mcp` (the MCP server host) and `Curl.Mcp.UnitLibrary` (its tool
logic), ADR-0548. Test classes take the namespace of the type they test, `Curl.Mcp`.

Unit tests only: build the host's service graph in process and never start the real stdio
transport. Tests that start the server over real standard input and output belong in
`Curl.Mcp.IntegrationTests` (ADR-0421).

Only `Curl.Mcp` may take the MCP SDK packages; this project takes `MSTest` alone and reaches
the SDK types through its reference to `Curl.Mcp`.
