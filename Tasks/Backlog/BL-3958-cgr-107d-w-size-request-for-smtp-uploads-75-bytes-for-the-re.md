---
id: BL-3958
title: CGR-107d: -w size_request for SMTP uploads (75 bytes for the report's repro)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary/SmtpProtocolHandler.cs, Curl.Protocol.Smtp.UnitTests/Cgr107Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3958 — CGR-107d: -w size_request for SMTP uploads (75 bytes for the report's repro)

## Goal

An SMTP upload's `%{size_request}` is what curl 8.22.0 counts.

## Context

Split from BL-3636 (third-party gap report issue CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3636's Context for the repro setups and commands). Measure real curl 8.22.0 with `Record-CurlExchange.ps1` or a C# file-based loopback server (no Python) before pinning a value. OP-11: `curl -w "%{size_request}" smtp://... --mail-from a@b --mail-rcpt c@d -T msg_crlf.txt` prints 75 with curl and 0 with Curl. Measure which bytes curl counts (commands only, or the message too). POP3 and IMAP already match.

## Acceptance criteria

- [ ] The repro prints the same `size_request` as real curl.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project (the file in `touches`) fails before the fix and passes after it (ADR-0482).
- [ ] The real-curl and Curl output of each repro is recorded in Notes and matches, platform-neutral (Windows and Linux answers pinned separately where they differ).
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed library shows no member outside a gate.

## Notes

## Log

- 2026-10-28: Created.
