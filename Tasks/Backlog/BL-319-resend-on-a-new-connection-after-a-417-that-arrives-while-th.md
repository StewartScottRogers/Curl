---
id: BL-319
title: Resend on a new connection after a 417 that arrives while the body is being sent
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-260]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-319 — Resend on a new connection after a 417 that arrives while the body is being sent

## Goal

A `417 Expectation Failed` that arrives after the one-second `100 Continue` wait ran out, while or after the body is sent, makes the handler close the connection and resend the request without `Expect` on a new connection, as curl 8.21.0 does.

## Context

- BL-260 resends only after a 417 that arrives during the wait, with the body unsent (`HttpProtocolHandler.RetriesWithoutExpect`, `bodyLeftUnsent`).
- In one racy BL-260 run curl 8.21.0 logged `Got HTTP failure 417 while sending data`, shut the connection down and resent on a second connection (`%{num_connects}` 2). In curl's source this path also rewinds the body (`http_perhapsrewind`), so a body that cannot be rewound (stdin) likely fails instead: measure it.
- Measure first with a loopback server that answers 417 only after reading some body bytes: the connections used, stdout, `-D`, `%{size_request}`, `%{size_upload}`, and what `-T -` does.

## Acceptance criteria

- [ ] The case is measured on curl 8.21.0 and the commands and bytes are in Notes.
- [ ] A handler test replays a 417 that arrives after the wait through the fakes (1-byte reads too) and matches the measured requests, connections and output.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http*` reports no failing member.

## Notes

## Log

- 2026-09-26: Created.
