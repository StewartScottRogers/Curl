---
id: BL-173
title: Perform an HTTP and HTTPS GET through IConnector in HttpProtocolHandler
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-170, BL-171, BL-172, BL-160]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-173 — Perform an HTTP and HTTPS GET through IConnector in HttpProtocolHandler

## Goal

`HttpProtocolHandler(IConnector, IHttpAuthenticator, ICookieStore?)` claims `http` and `https`, connects through the injected connector, sends the request, writes headers to `HeaderOutput` and the body to `Output`, and returns a `TransferReport`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- https is `ConnectTarget(host, port, UseTls: true)`; the handler does no TLS of its own (ADR-0005). Default ports 80 and 443.
- `ConnectResult.Failed` codes (6, 7, 35, 60) pass through unchanged with their messages.
- Until BL-161's implementations exist, tests pass a fake `IHttpAuthenticator` that adds nothing.
- `TransferReport` (BL-160) carries response code, headers, sizes.

## Acceptance criteria

- [ ] `SupportedSchemes` is `http` and `https`; tests show ports 80/443 by default and `UseTls` true only for https via `QueueConnector`.
- [ ] Connector failures 6, 7, 35 and 60 are returned with the connector's exit code and message.
- [ ] Response headers reach `HeaderOutput` exactly as received; the body reaches `Output`.
- [ ] `TransferResult.Report` carries the response code, headers, header size and download size for a scripted exchange.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

**Partial work from a cut-off run (2026-09-26):** local branch `factory/BL-173-wip` holds one commit of it. Start with `git cherry-pick --no-commit factory/BL-173-wip`, review it, then carry on from there rather than starting over.

- Plan item: H5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Shift stopped while waiting for tokens (limit reset early); partial work saved on branch factory/BL-173-wip
- 2026-09-26: Backlog -> Doing.
