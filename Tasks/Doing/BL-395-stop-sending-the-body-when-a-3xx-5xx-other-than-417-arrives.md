---
id: BL-395
title: Stop sending the body when a 3xx-5xx other than 417 arrives mid-upload
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-319]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-395 — Stop sending the body when a 3xx-5xx other than 417 arrives mid-upload

## Goal

A final status of 300 or above other than 417 that arrives once the `100 Continue` wait ran out, while a POST or PUT body is being sent, stops the sending and closes the connection after the response, as curl 8.21.0 does (`HTTP error before end of send, stop sending`).

## Context

- BL-319 made `HttpRequestBodyWriter`, under `HttpContinueWaitConnection.SendUnlessExpectationFailedAsync`, stop at the first piece a 417 beats; every other status that arrives mid-upload still lets the whole body go.
- curl's `http.c` stops sending for any status of 300 or above (not during authentication negotiation, not when the body will be rewound) and marks the connection to close; a 2xx keeps sending.
- Measure first with the BL-319 loopback server answering 500 and 301 after reading body bytes: bytes sent, `%{size_request}`, `%{size_upload}`, `%{num_connects}`, `-D`, and `-L` for the 301.

## Acceptance criteria

- [ ] The 500 and 301 cases are measured on curl 8.21.0 and the commands and bytes are in Notes.
- [ ] A handler test replays each through `GatedConnection` with `StallsWritesOnceReleased` (1-byte reads too) and matches the measured requests, connections and output.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http*` reports no failing member.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
