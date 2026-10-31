# ADR-1074 — MCP URLs list tools without data and call one with a JSON data body

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4159.

## Context

BL-4159 adds `McpProtocolHandler` in `Curl.Extension.Protocol.McpClient.UnitLibrary/Client`, an extension (ADR-0999) that lets
curl list and call the tools of a Model Context Protocol server. Upstream curl has no MCP, so there is no curl output to
match; the Model Context Protocol specification, revision 2025-06-18 (the latest dated revision), decides the wire. The
JSON-RPC wire format (BL-4214), the Streamable HTTP transport (BL-4215) and the stdio transport (BL-4216) were already
in the library; this decision covers the URL forms, what a transfer does, its output and its exit codes.

## Decision

- **Schemes.** `mcp://host[:port]/endpoint[?query]` speaks Streamable HTTP over plain TCP (default port 80);
  `mcps://` adds TLS (default port 443). Both connect through curl's connector, so `--proxy`, `-v` connection events and
  `%{remote_ip}` behave as for curl's own schemes. The path and query are sent as the request target, unchanged.
- **Local-process form.** `mcp+stdio://localhost/command?arg1&arg2` starts the program and speaks the stdio transport.
  The percent-decoded path is the program: one segment (`/npx`) or a drive path (`/C:/x.exe`) loses its leading slash,
  a longer path (`/usr/local/bin/server`) is kept absolute. Each `&`-separated query part, percent-decoded, is one
  argument. The host must be `localhost`: curl's URL parser reads `mcp+stdio:///npx` as the host `npx`, so an empty
  host cannot be told apart from a remote one. Any other host, or no program, ends with exit 3.
- **What a transfer does.** Every transfer sends `initialize` (protocol version 2025-06-18, no client capabilities,
  `clientInfo` `curl` `1.0.0`), takes the server's chosen version for `MCP-Protocol-Version`, sends
  `notifications/initialized`, then:
  - without `-d`, sends `tools/list`, following every `nextCursor`, and writes one line per tool: its name, then a tab
    and its description with line breaks turned into spaces when it has one;
  - with `-d`, reads the data as `{"name":"tool","arguments":{...}}` (the `params` of `tools/call`; `arguments`
    defaults to `{}`), sends `tools/call`, and writes each content item on its own line: a text item's text, any other
    item as its JSON.
  It then ends the session: a DELETE with `Mcp-Session-Id` over HTTP, closing standard input (then a kill after five
  seconds) over stdio.
- **Exit codes.** Data that is not such an object: 43, before connecting. A connect failure: the connector's code. A
  program that cannot start: 7, as for a server curl cannot reach. A JSON-RPC error response (text
  `MCP server error <code>: <message>`), an HTTP status or reply the transport refuses, a malformed message or a server
  that exits before answering: 8. A tool result with `isError: true`: its content is written and the transfer ends with
  22, the code curl uses when the server answered but reported failure, so scripts can tell it from success.
- **Version.** Only 2025-06-18 is asked for. A server that answers with another version is still used: listing and
  calling tools is the same in 2025-03-26 and 2025-06-18, and the spec leaves disconnecting to the client.

## Left out, and why

- Resources, prompts, completion, sampling, roots and elicitation: the task asks for tools only; each is its own
  extension task if wanted.
- The standalone GET event stream of Streamable HTTP and resumption with `Last-Event-ID`: a single list or call needs
  neither, since its response arrives on the POST.
- Authorization (the spec's OAuth 2.1 flow): `-H "Authorization: Bearer ..."` is not yet passed through; filing it
  needs the transport to take extra headers.
- Server requests to the client (sampling, elicitation) are skipped while waiting for the response, as the client
  declares none of those capabilities.

## Consequences

- `curl mcp://host/mcp` lists tools and `curl -d '{"name":"x"}' mcp://host/mcp` calls one, with no option added.
- `curl --extensions` and `curl --ai-help extensions` describe the three schemes; with `CURL_EXTENSIONS=0` an `mcp`
  URL fails as curl fails an unsupported scheme (exit 1, `Protocol "mcp" not supported`).
