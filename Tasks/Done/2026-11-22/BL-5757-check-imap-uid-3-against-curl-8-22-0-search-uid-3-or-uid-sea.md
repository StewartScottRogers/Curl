---
id: BL-5757
title: Check imap://...?UID 3:* against curl 8.22.0: SEARCH UID 3:* or UID SEARCH
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary/ImapProtocolHandler.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5757 — Check imap://...?UID 3:* against curl 8.22.0: SEARCH UID 3:* or UID SEARCH

## Goal

An IMAP URL whose search part starts with `UID` (`imap://h/INBOX?UID%203:*`) sends the same command as curl 8.22.0, and the output matches curl's.

## Context

From Surly cookbook bug report `2026-11-22T1116-Curl-bug-report.md` (batch 11 of the 1,000-recipe plan), finding 5 (check). Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

`curl -u ann@example.com:pw "imap://127.0.0.1:1143/INBOX?UID%203:*"`: surl's log shows `SEARCH UID 3:*`, answered with message sequence numbers. The cookbook expected `UID SEARCH` (RFC 5092's search part). Upstream curl sends the URL's query as `SEARCH <query>` (imap.c `imap_perform_search`) and does not turn a leading `UID` into `UID SEARCH`, as far as known; an IMAP server answers `SEARCH UID 3:*` with sequence numbers, per RFC 3501. Measure curl 8.22.0 for the exact command and output; if the port already matches, pin it with a test and say so in the Notes (the cookbook can use `-X "UID SEARCH UID 3:*"` for UIDs).

## Acceptance criteria

- [x] The Notes record the command curl 8.22.0 sends and its output for `?UID%203:*`.
- [x] The port sends the same command and gives the same output, pinned by a test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured with Record-CurlExchange.ps1 against installed curl 8.23.0 (Schannel; the 8.22.0 binary is not at hand): `imap://h/INBOX?UID%203:*` sends `A004 SEARCH UID 3:*` (no `UID SEARCH`), server reply `* SEARCH 1 2`, stdout `* SEARCH 1 2`, exit 0. The port already matched (`SEARCH ` + decoded query), so no production change; pinned by a new DataRow in ImapProtocolHandlerListTests. The cookbook should use `-X "UID SEARCH UID 3:*"` for UIDs.
- Linux/macOS: the test is platform-neutral. No production code changed, so no quality run.

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1116, finding 5 (check).
- 2026-11-22: Backlog -> Doing. Criteria at claim: 23e4f52d f81ea227 3d836d5e.
- 2026-11-22: Doing -> Done. Port already matches curl; pinned by test
