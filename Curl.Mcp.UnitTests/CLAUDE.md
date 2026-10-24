# Curl.Mcp.UnitTests

The MSTest tests of both `Curl.Mcp` (the MCP server host) and `Curl.Mcp.UnitLibrary` (its tool
logic), ADR-0548. Test classes take the namespace of the type they test, `Curl.Mcp`.

Unit tests only: build the host's registry and handlers in process and drive the host over in-memory streams, never the real stdio
transport. Tests that start the server over real standard input and output belong in
`Curl.Mcp.IntegrationTests` (ADR-0421).

This project takes `MSTest` alone; no test uses the MCP SDK (BL-3388).
