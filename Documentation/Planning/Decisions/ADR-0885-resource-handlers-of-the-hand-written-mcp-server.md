# ADR-0885: The resource handlers of the hand-written MCP server

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3381
- Decided by Claude under Stewart's delegation.

## Context

BL-3381 adds `ResourcesListHandler` and `ResourcesReadHandler` to
`Curl.Mcp.Protocol.UnitLibrary/Resources/` (ADR-0799), over the providers registered in
`McpServerRegistry` (ADR-0801). They must answer as the SDK 2.2.0 server does in the golden
conversations of BL-3374, which negotiate revision 2025-06-18; its `resources/list` and
`resources/read` sections (unchanged in substance in 2025-11-25) were read. `McpInitializeHandler`
writes one `capabilities` member per `IMcpCapabilityContributor` (ADR-0883), and the golden
`initialize` answers carry `"resources": {}` once.

## Decision

1. Only `ResourcesListHandler` contributes the `resources` capability, as `{}`. Were both
   handlers to contribute it, `initialize` would write the member twice; a server with
   `resources/read` but no `resources/list` is not one the host builds.
2. `resources/list` lists the registry's resources in the order their providers were added and
   ignores `cursor` and any other params: the list always fits one page, so no `nextCursor`.
3. `resources/read` takes the `uri` only when `params` is an object whose `uri` is a string.
   No params, no `uri`, a non-string `uri` or params that are not an object read as the empty
   URI, so each answers -32002 `Resource not found: ''.`, as `CurlAiHelpResourceHandlers` does
   for a request without parameters. A URI no provider listed, or one its provider reads as
   `null`, answers -32002 `Resource not found: '<uri>'.`, the golden missing-URI answer.
4. A read result is always one text content, the provider's `McpResourceContents`.

## Consequences

The golden `resources/list` result and missing-URI error are pinned as unit tests in
`Curl.Mcp.Protocol.UnitTests/Resources/`. The SDK may answer a non-string `uri` with -32602
rather than -32002; no golden file records that case, and -32002 keeps one rule for every
request that names no resource.
