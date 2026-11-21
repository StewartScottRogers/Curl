---
id: BL-5645
title: Drop the stray 0x02 after an IMAP refused-LOGIN message
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary/ImapAuthentication.cs, Curl.Original.Protocol.Imap.UnitLibrary/ImapSessionMessages.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5645 — Drop the stray 0x02 after an IMAP refused-LOGIN message

## Goal

After a refused IMAP `LOGIN`, the error message is exactly curl 8.22.0's, with no stray control byte.

## Context

From Surly cookbook bug report `2026-11-20T1836-Curl-bug-report.md` (batch 6 of the 1,000-recipe plan, and batch 5's CI on Linux and macOS), finding 6. Build tested: Curl `7d167fa10a` (`curl`, `curl-mcp`, `Curl.Api`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

The message after a refused `LOGIN` ends with a stray 0x02 (`Access denied.` followed by 0x02). Likely a byte of the server's tagged reply or a length byte carried into the message. Measure curl 8.22.0's stderr for a refused LOGIN (`Record-CurlExchange.ps1`) and match it in both the console and `Curl.Api`'s exception message.

## Acceptance criteria

- [ ] A refused IMAP `LOGIN` gives the same stderr as curl 8.22.0's recorded run, with no control byte, pinned by a test.
- [ ] `Curl.Api`'s exception message for the same case has no control byte, pinned by a test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1836, finding 6.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 56ff107d 465f86b3 3d836d5e.
