---
id: BL-336
title: Hand reusable HTTP connections back and report reuse per ADR-0050 in Curl.Protocol.Http
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-335, BL-173]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-336 — Hand reusable HTTP connections back and report reuse per ADR-0050 in Curl.Protocol.Http

## Goal

`HttpProtocolHandler` sets `ConnectTarget.PoolScheme`, marks a connection reusable when ADR-0050 says it persists, reports `%{num_connects}` `0` on a reused connection, writes curl's end-of-transfer `-v` line, and retries once on a fresh connection when a reused one fails before any response byte.

## Context

- ADR-0050, sections "How a handler hands a connection back", "When a connection is not reusable" and "`%{num_connects}` and `-v`". If the ADR and this task disagree, the ADR wins.
- `HttpProtocolHandler.TargetOf` sets `PoolScheme` to `http` or `https` (a forward proxy connection keys on the proxy as `Host`/`Port`, as today).
- Call `MarkReusable` only after the response is read to its end and `HttpConnectionPersistence.KeepsAlive` is true; never on failure, timeout, `--max-filesize` (exit 63), failed output write, unread body, or 101.
- Pass `newConnection: !connect.IsReused` where `HttpProtocolHandler.ExchangeAsync` is called with `true` today.
- End-of-transfer line through the transfer context's events as `ReportInfo`, after disposing the connection: `Connection #<N> to host <host>:<port> left intact` when marked, `shutting down connection #<N>` when the response did not persist, `closing connection #<N>` when the transfer failed (all measured in ADR-0050 on curl 8.21.0).
- Retry-once on a dead reused connection: measure the text curl 8.21.0 prints for it before pinning it, and record the command and bytes here under Notes.
- Tests use fakes of `IConnector`/`IConnection` returning `ConnectResult` with `IsReused`; no network.

## Acceptance criteria

- [ ] Tests pin `PoolScheme` `http`/`https` on the target the handler connects to.
- [ ] Tests pin that `MarkReusable` is called on the connection the handler was given for a `Content-Length` and a chunked keep-alive response, and not for `Connection: close`, HTTP/1.0 without keep-alive, a read-to-close body, exit 63, and a 101.
- [ ] Tests pin `ConnectionCount` `0` when the connector returns `IsReused = true` and `1` otherwise.
- [ ] Tests pin each of the three end-of-transfer lines byte for byte as ADR-0050 measured them.
- [ ] A test pins that a reused connection failing before the first response byte is retried once on a new connection, with the measured `-v` text.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Lane handed over mid-run while the factory's restart logic was fixed; partial work saved on branch factory/BL-336-wip, a stash commit: start with git cherry-pick --no-commit -m 1 factory/BL-336-wip and carry on from it.
