---
id: BL-5612
title: Let Curl.Api send a custom request with spaces, such as IMAP STORE and MOVE
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Options/Protocol/CurlMailOptions.cs, Curl.Api/Options/Protocol/CurlRequest.Protocol.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5612 — Let Curl.Api send a custom request with spaces, such as IMAP STORE and MOVE

## Goal

A `Curl.Api` user can send any `CURLOPT_CUSTOMREQUEST` text, including spaces (IMAP `STORE 1 +Flags \Seen`, `MOVE 1 Archive`, FTP and POP3 commands), as `curl -X` does.

## Context

From Surly cookbook bug report `2026-11-20T1022-Curl-bug-report.md` (batch 4 of the 1,000-recipe plan), finding 1. Build tested: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11; server Surl `763754785`. Check it still reproduces on the current branch first.

`HttpMethod` refuses a value with spaces (`FormatException`, a .NET rule), so IMAP `STORE` and `MOVE` cannot be sent as custom requests through the method. Add a custom-request property (for example `CurlRequest.CustomRequest` or one on the mail options) that maps straight to `CURLOPT_CUSTOMREQUEST` and wins over the method, and document it in the README with an IMAP `STORE` sample.

## Acceptance criteria

- [ ] A `Curl.Api` request to an IMAP test server with the custom request `STORE 1 +Flags \Seen` sends exactly that command and the response reports success; `MOVE 1 Archive` likewise.
- [ ] Unit tests in `Curl.Api.UnitTests` pin that the property sets `CURLOPT_CUSTOMREQUEST` unchanged, spaces and all.
- [ ] The README shows the property with an IMAP sample; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1022, finding 1.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 914993b6 f99fd184 14f2905f.
