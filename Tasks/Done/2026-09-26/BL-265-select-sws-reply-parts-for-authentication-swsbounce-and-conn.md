---
id: BL-265
title: Select sws reply parts for authentication, swsbounce and CONNECT in the sws emulation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-146]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-265 — Select sws reply parts for authentication, swsbounce and CONNECT in the sws emulation

## Goal

`SwsHttpServerConnector` picks the reply part for authenticated, bounced and CONNECT requests
the way sws does, so HTTP authentication and proxy cases get the reply upstream serves.

## Context

- BL-146 selects `<data>` / `<dataN>` from the path only (`SwsHttpReplySelector`).
- Upstream: `tests/server/sws.c` at `curl-8_21_0`, `sws_ProcessRequest`, `sws_send_doc` and
  `service_connection`:
  - `Authorization: Digest` adds 1000 to the part number once per request; NTLM type-1
    (`TlRMTVNTUAAB`) adds 1001 and type-3 (`TlRMTVNTUAAD`) adds 1002; `Authorization: Basic`
    with a part already at 1000 or more adds 1; `Authorization: Negotiate` counts up from the
    previous part.
  - A reply containing `swsbounce` makes the next request for the same test get the previous
    part number plus one (state kept across connections).
  - A `CONNECT host:port HTTP/x.y` request is answered from `<connect>` / `<connectN>`.

## Acceptance criteria

- [x] Tests show each rule above selects the part sws selects (Digest to `<data1000>`, NTLM
      type-1 to `<data1001>`, type-3 to `<data1002>`, Basic after 1000 to `<data1001>`,
      Negotiate counting up, swsbounce to part + 1, CONNECT to `<connect>`).
- [x] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10,
      per `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Plan: `SwsHttpReplySelector` computes the part as sws does - path number, then the first
  matching authorization rule, then the bounce - and picks `connect` / `data` by
  `SwsHttpRequestLine.IsConnect`. It now takes the whole `SwsServerCommands` (it needs
  `ReplyMode` as well as `ClosesAfterEveryReply`).
- Measured, not recalled: `tests/server/sws.c` at `curl-8_21_0` fetched from GitHub. Three
  things differ from the task's summary and the code follows the source: the bounce gives the
  next request the previous part plus one whatever it asked for (only a changed test number
  cancels it); there is no "number after the last dot" rule at this tag, so a `CONNECT` is
  always part 0 before authorization; and `req->open = persistent` at the end of
  `sws_send_doc` overrides the close for an HTTP/1.0 `CONNECT` and for `Connection: close`,
  so neither closes the connection.
- Decision recorded in ADR-0047 (decided by Claude under Stewart's delegation): the emulation
  serves one case, so every request is taken to name it; the Negotiate counter and the bounce
  live in the selector, shared by the connector's connections.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0047 and its README row; no
  task in Doing names it.
- Code review (code-reviewer agent) found that sws's header loop returns before the
  authorization rules for a `Transfer-Encoding: chunked` request and at an unreadable
  `Content-Length` (confirmed in `sws.c`); `SwsHttpRequestFraming.ReachesAuthorizationRules`
  now carries that out, with tests, and the docs and ADR say so.
- The emulation does not check `ReplyMode` before remembering a bounce: sws skips it under
  `idle` and `stream`, but `<servercmd>` covers every request of a case, so no reply of such a
  case could be changed by it. A guard would be code no test can observe.
- Not done, by choice: the selector's state is not locked. sws is single-threaded and the
  harness drives one request at a time; revisit if a case ever runs connections concurrently.
- `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 100% line, 100% branch,
  0 failing members, worst CRAP 10 (after splitting `Select` and `AuthorizedPartNumber`,
  first measured at complexity 16 and 12).
- `dotnet build`: 0 warnings, 0 errors. Fast tests: all 16 test assemblies pass
  (Conformance 288). `dotnet format --verify-no-changes` is clean for both Conformance
  projects; the solution-wide check reports end-of-line errors in
  `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Authentication.cs`, a file this
  task does not touch.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. The sws emulation selects reply parts by Authorization (Negotiate, Digest, NTLM, Basic), swsbounce and CONNECT as sws does
