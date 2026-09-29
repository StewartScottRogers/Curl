---
id: BL-532
title: Add a scripted binary exchange mode to Record-CurlExchange.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-532 — Add a scripted binary exchange mode to Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -Script <file>` serves one connection from a script of steps (`read` until N bytes or an idle gap, `send` escaped bytes, `close`), so binary request-reply protocols such as LDAP's BER messages and SMB's frames can be measured against the reference curl, with every byte curl sent written to `request.bin` and a hex transcript to `transcript.txt`.

## Context

- Needed to measure `ldap://` (audit row 37) and `smb://` (row 39) before any bytes are pinned. The HTTP mode reads until `CRLF CRLF`, which binary protocols never send.
- Reuse the escape syntax `-Response` already documents (`\xHH` and friends) and the `-Tls` certificate for `ldaps://`.

## Acceptance criteria

- [ ] `.PARAMETER Script` documents the step syntax with an example, in the script header.
- [ ] A script that reads one message and sends a canned LDAP `BindResponse` (success) then reads the search request and closes records the reference curl's `ldap://127.0.0.1:<P>/dc=example` bind and search bytes in `request.bin`.
- [ ] A `read` step that times out ends the session and is noted in `transcript.txt`, without hanging the script.
- [ ] The existing HTTP and `-Ftp` modes produce identical files before and after the change.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
