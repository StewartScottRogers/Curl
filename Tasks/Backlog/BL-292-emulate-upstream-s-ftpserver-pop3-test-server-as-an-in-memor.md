---
id: BL-292
title: Emulate upstream's ftpserver POP3 test server as an in-memory connector
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-147]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-292 — Emulate upstream's ftpserver POP3 test server as an in-memory connector

## Goal

Vendored upstream cases with `<server>pop3</server>` run through Curl against an in-memory emulation of upstream's `ftpserver.pl` POP3 mode instead of being skipped, and every one that passes is on `PassingUpstreamCases.txt`.

## Context

- ADR-0013, decision 7: server emulations are follow-up tasks filed once the runner (BL-147) exists.
- On 2026-09-26, 50 cases were skipped with "the harness has no value for %POP3PORT" (count from BL-147's run; some also need other servers).
- Upstream: `tests/ftpserver.pl` at `curl-8_21_0`. Model the connector on `SwsHttpServerConnector`; add `pop3` to `UpstreamCaseScreening`'s servers and give `%POP3PORT` a value in `UpstreamCaseRunner`.
- Curl's `pop3` handler must be registered in `CurlComposition.CreateProtocolHandlers` for cases to pass; if it is not, the cases fail with a first difference, which is still progress on the skip count.

## Acceptance criteria

- [ ] An in-memory `IConnector` in `Curl.Conformance.UnitLibrary` answers POP3 commands as `ftpserver.pl` does for the `<reply>` and `<servercmd>` forms the vendored `pop3` cases use, and records received bytes for `<verify><protocol>`.
- [ ] `pop3` cases are no longer skipped for the missing server or `%POP3PORT`; any form not emulated skips the case with a named reason.
- [ ] Every `pop3` case that passes is added to `PassingUpstreamCases.txt`, and the pass rate in `Curl.Conformance.UnitTests/CLAUDE.md` is updated.
- [ ] 100% line and branch coverage and complexity at most 10 in `Curl.Conformance.UnitLibrary`, per `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-26: Created.
