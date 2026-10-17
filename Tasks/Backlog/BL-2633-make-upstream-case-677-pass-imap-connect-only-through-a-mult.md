---
id: BL-2633
title: Make upstream case 677 pass: IMAP CONNECT_ONLY through a multi handle then curl_easy_send
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2623]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2633 — Make upstream case 677 pass: IMAP CONNECT_ONLY through a multi handle then curl_easy_send

## Goal

Upstream case 677 passes through the runner.

## Context

Found by BL-2535, which ported the libtest program and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. In the first pass find the project at fault and add it to `touches` before any edit. test677 (lib677: IMAP `CURLOPT_CONNECT_ONLY` on a multi handle, `CURLINFO_ACTIVESOCKET` after the done message, then `curl_easy_send` of `A1 IDLE` when `curl_multi_wait` reports the socket writable): `differs at byte 0: expected "A001 CAPABILITY\r\n", got the end` - the same cause as BL-2623 (connect-only through a multi handle never connects); after it, check `curl_multi_wait` fills an extra socket's `revents`.

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
