# ADR-1214: The realtime transfer surface pins in-band errors as exit 22 under `--fail`, and `webtransport` waits for its reference server

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4616

Decided by Claude under Stewart's delegation.

## Context

BL-4616 checks curl's generic transfer options for `grpc`, `grpcs`, `signalr`, `signalrs`, `socketio`, `socketios`,
`webtransport`, `mcp` and `mcps` (`Documentation/Conformance/Extended/CurlSurface-Transfer-Realtime.md`). Three
expected values needed a choice:

1. curl's manual says `--fail` and `--fail-with-body` act on HTTP response codes of 400 or more. The errors these
   extensions map to exit 22 arrive inside a successful HTTP exchange: a grpc-status other than 0 (ADR-1133), a
   SignalR completion with an error (ADR-1084), a refused Socket.IO namespace (ADR-1035) and an MCP tool result with
   `isError`, whose content the MCP client writes before failing (ADR-1074).
2. The `-v` connection line: curl 8.22.0 writes `* Established connection to <host> (<ip> port <port>) from ...`, not
   the older `* Connected to ...`, and Curl's engine already writes curl 8.22.0's line.
3. `webtransport` runs over HTTP/3, and the harness has no WebTransport reference server yet.

## Decision

1. The cases pin exit 22 and `curl: (22) <message>` for these errors with `--fail` and with `--fail-with-body`, the
   same as without either option, and the MCP case keeps the tool's content on standard output under both: the
   options change nothing for an error that is not an HTTP response code, so the extension's ADR answer stands.
2. The `-v` check looks for curl 8.22.0's `* Established connection to localhost (127.0.0.1 port N) from ` line, and
   for a TLS scheme the platform backend's line (`* schannel: ` on Windows, the Schannel build; `* SSL connection
   using ` elsewhere, the OpenSSL build).
3. Every `webtransport` row is `n/a`, naming BL-4607, which adds the WebTransport over HTTP/3 reference server; the
   rows become cases once it is Done.

## Consequences

The transfer surface agrees with each extension's own ADRs, and a change that made `--fail` suppress an extension's
in-band error or its body would fail these cases. `webtransport`'s transfer surface stays unmeasured until BL-4607.
