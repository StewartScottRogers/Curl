# ADR-0804: The hand-written `transfer` tool reads a missing or `null` field as omitted

- Status: Accepted
- Date: 2026-10-23
- Decided by Claude under Stewart's delegation (BL-3384)

## Context

`TransferMcpTool` (`Curl.Mcp/Tools`) replaces the SDK-attributed `CurlTransferTool` on the
hand-written server abstractions. With no SDK binder it reads the `tools/call` arguments itself,
after `McpTransferRequestCheck.CheckArgumentShapes` has refused any wrong shape (ADR-0771). Two
answers of the SDK tool, pinned in BL-3374's golden files, were artefacts of its binder rather
than a contract: a missing `url` gave the bare `An error occurred invoking 'transfer'.`
(`tools-call-transfer-missing-url.json`), and an explicit `"method": null` bound `null` over the
`"GET"` default, so the transfer was refused as `'' is not an HTTP method token`. Real curl has no
counterpart; this is the MCP server's own contract, so there is nothing to measure.

## Decision

Every field that is missing or JSON `null` takes the default the SDK method's signature gave an
omitted one: `method` `"GET"`, `headers` empty, `followRedirects` and `insecure` false, the rest
none. A missing or `null` `url` is the empty URL, which `McpTransferRequestCheck` refuses as the
structured `InvalidRequest` failure `Invalid url: it is empty.` - the same result shape every other
refusal has. Arguments that are not a JSON object are read as no fields at all.

## Consequences

- An agent that leaves out `url` learns which field to fix, instead of the SDK's bare message.
- `"method": null` sends a GET, as the schema's `"default": "GET"` says.
- Every other answer, the wrong-shape refusals included, is byte for byte the SDK tool's, which
  `TransferMcpToolTests` pins.
