---
id: BL-1165
title: Pin that --trace-config imap and pop3 write no lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1165 — Pin that --trace-config imap and pop3 write no lines

## Goal

Tests pin that `-v --trace-config imap` and `-v --trace-config pop3` write exactly `-v`'s lines for an IMAP fetch and a POP3 retrieve, as curl 8.21.0 does.

## Context

- Split from BL-1104 (ADR-0318). curl 8.21.0's IMAP and POP3 handlers write no trace lines of their own (measured, Notes), so the work is tests, like BL-649 pinned `tls` and `http/1` as none.

## Acceptance criteria

- [x] A test runs an IMAP fetch with `-v --trace-config imap` and with `-v` alone and asserts identical stderr.
- [x] The same for a POP3 retrieve with `pop3`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

Measured 2026-10-02 (BL-1104), curl 8.21.0 Schannel: `Record-CurlExchange.ps1 -Imap -CurlArgs '-sS','--trace-config','imap','-v','-u','u:p','imap://127.0.0.1:P/INBOX;UID=1'` and `-Pop3 ... '--trace-config','pop3','-v','-u','u:p','pop3://127.0.0.1:P/1'` both exit 0 with no `[IMAP]` or `[POP3]` line, only the usual `-v` lines.

Delivered 2026-10-02: `RunAsync_VerboseUidFetchUnderTraceConfigImap_WritesExactlyTheVerboseLines` and `RunAsync_VerboseRetrUnderTraceConfigPop3_WritesExactlyTheVerboseLines` (Curl.Console.UnitTests) run the same scripted transfer with `-sv` and with `-sv --trace-config imap|pop3` (lower and upper case) and assert identical stderr; the `-v` lines themselves stay pinned by the existing measured tests. Test-only, so the `feature` pipeline reduced to tests plus verify; no ADR needed.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Tests pin that -v --trace-config imap and pop3 write exactly -v's lines
