# ADR-1238 — MCP requests carry curl's credentials and headers, and a 401 is exit 67

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4906
- Decided by Claude under Stewart's delegation.

## Context

The outside fitness review of 2026-11-11 (EXT-003) found that `mcp://` and `mcps://` sent no
`Authorization`, no `-H` header and no `User-Agent`: `McpStreamableHttpTransport` built each request
head from fixed fields only, so a server that needs a token (Model Context Protocol 2025-06-18,
"Authorization": every request carries `Authorization: Bearer <token>`) answered 401, and Curl ended
`curl: (8) The server answered with status 401.`. MCP is an extension (ADR-0999), so there is no
curl answer to match; the rule is to send the options as curl sends them on HTTP, as the port's gRPC
sends `-H` and `-A` and az/gs honour `--oauth2-bearer`.

## Decision

- `McpRequestHeaderFields` builds the fields every request (initialize, notifications, tools, the
  ending DELETE) carries after `Host`: `Authorization: Bearer <token>` from `--oauth2-bearer`, else
  `Authorization: Basic` from `-u` (or the URL's user information); `User-Agent` from `-A`, curl's own
  `curl/8.22.0` when not given and none for `-A ""`; then each `-H` value with curl's rules: `Name: value`
  replaces a field of that name (so `-H "Authorization: Bearer t"` stands in for `--oauth2-bearer`),
  `Name:` removes it, `Name;` sends it empty, and a value is cut at its first line end.
- A 401 that the credentials did not clear ends the transfer with exit 67 (`LoginDenied`), as an
  authentication failure; every other unexpected status stays exit 8. Curl does not run the OAuth
  authorization flow the 401's `WWW-Authenticate` points to: curl gets no token on its own on HTTP either.
- The head is written in Latin-1, so a `-H` or `-A` value outside ASCII is sent byte for byte rather
  than as `?`.

## Consequences

- MCP servers behind a bearer token, Basic authentication or an API-key header work with the options
  curl users already pass.
- `-H` cannot replace the transport's own `Accept`, `Content-Type`, `Content-Length`, `Mcp-Session-Id`
  or `MCP-Protocol-Version`: those carry the transport's rules, so a `-H` of one of them is sent as well.
