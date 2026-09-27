---
id: BL-260
title: Retry a request without Expect after a 417 reply, as curl does
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-175]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-260 — Retry a request without Expect after a 417 reply, as curl does

## Goal

A `417 Expectation Failed` reply to a request that carried `Expect: 100-continue` makes the handler resend the request without `Expect` and write only the second response, as curl 8.21.0 does.

## Context

- BL-175 (HttpContinueWaitConnection) treats any final status during the `100 Continue` wait as the response and leaves the body unsent. curl 8.21.0 instead, on a 417, resends the head without `Expect: 100-continue` followed by the body; measured in BL-175 with a server that answered 417 at once: the second request head and the 1048577-byte body followed on the same connection.
- Measure first with a loopback server: which connection the retry uses, what stdout, the header output and `%{size_request}` show, and what happens when the retry also gets 417.

## Acceptance criteria

- [ ] The retry is measured on curl 8.21.0 and the command and bytes are in Notes.
- [ ] A handler test replays a 417 then a 200 through the fakes (1-byte reads too) and matches the measured request bytes and output.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http*` reports no failing member.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
