---
id: BL-5615
title: Report the SMTP reply code as StatusCode for a Curl.Api custom SMTP command such as NOOP
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Messages/CurlClient.SendAsync.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5615 — Report the SMTP reply code as StatusCode for a Curl.Api custom SMTP command such as NOOP

## Goal

A `Curl.Api` SMTP request with a custom command (`NOOP`, `VRFY`, `EXPN`, `HELP`) reports the server's reply code in `StatusCode`, as `curl -X NOOP -w %{response_code}` prints 250.

## Context

From Surly cookbook bug report `2026-11-20T1022-Curl-bug-report.md` (batch 4 of the 1,000-recipe plan), finding 4. Build tested: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11; server Surl `763754785`. Check it still reproduces on the current branch first.

A `NOOP` custom request gives `StatusCode` 0; `curl -X NOOP -w %{response_code} smtp://...` prints 250. `StatusCode` should carry `CURLINFO_RESPONSE_CODE` for mail protocols as it does for HTTP. Check IMAP and POP3 custom requests at the same time.

## Acceptance criteria

- [ ] Against an SMTP test server, a `Curl.Api` `NOOP` request reports `StatusCode` 250, pinned by a test.
- [ ] IMAP and POP3 custom requests report the same response code as `curl -w %{response_code}` does for them.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1022, finding 4.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 2491c0c4 28e934ce 3d836d5e.
