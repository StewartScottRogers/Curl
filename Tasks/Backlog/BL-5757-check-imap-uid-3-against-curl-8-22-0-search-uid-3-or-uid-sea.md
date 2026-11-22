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
completed:
---
# BL-5757 — Check imap://...?UID 3:* against curl 8.22.0: SEARCH UID 3:* or UID SEARCH

## Goal

An IMAP URL whose search part starts with `UID` (`imap://h/INBOX?UID%203:*`) sends the same command as curl 8.22.0, and the output matches curl's.

## Context

From Surly cookbook bug report `2026-11-22T1116-Curl-bug-report.md` (batch 11 of the 1,000-recipe plan), finding 5 (check). Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

`curl -u ann@example.com:pw "imap://127.0.0.1:1143/INBOX?UID%203:*"`: surl's log shows `SEARCH UID 3:*`, answered with message sequence numbers. The cookbook expected `UID SEARCH` (RFC 5092's search part). Upstream curl sends the URL's query as `SEARCH <query>` (imap.c `imap_perform_search`) and does not turn a leading `UID` into `UID SEARCH`, as far as known; an IMAP server answers `SEARCH UID 3:*` with sequence numbers, per RFC 3501. Measure curl 8.22.0 for the exact command and output; if the port already matches, pin it with a test and say so in the Notes (the cookbook can use `-X "UID SEARCH UID 3:*"` for UIDs).

## Acceptance criteria

- [ ] The Notes record the command curl 8.22.0 sends and its output for `?UID%203:*`.
- [ ] The port sends the same command and gives the same output, pinned by a test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1116, finding 5 (check).
