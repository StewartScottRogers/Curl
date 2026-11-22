---
id: BL-5755
title: Make a Curl.Api body to webtransport:// be sent, as curl -d does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary]
requirement: none
created: 2026-11-22
completed:
---
# BL-5755 — Make a Curl.Api body to webtransport:// be sent, as curl -d does

## Goal

A `Curl.Api` `POST` with a body to a `webtransport://` URL sends the body on the session and returns what the server sends back, as `curl -d @file webtransport://...` does.

## Context

From Surly cookbook bug report `2026-11-22T1116-Curl-bug-report.md` (batch 11 of the 1,000-recipe plan), finding 3. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

`surl --cert server.pem --key server.key --user a:b webtransport://127.0.0.1:4433/`; a `Curl.Api` `POST` with a 100-byte body gets status 200 with an empty body, and surl records no data. `curl -d @file webtransport://...` echoes it. Same family as BL-5611, BL-5681 and the MCP task filed with this one: the WebTransport extension reads `-d` data that a `Curl.Api` body does not supply in the same form. Cover the stream, `?wt=uni` and `?wt=datagram` modes. Cookbook recipe: `webtransport-send-datagrams-and-streams-in-csharp` (retitled "Opening a WebTransport Session in C#" until this lands).

## Acceptance criteria

- [ ] Against Surl's WebTransport echo (or the test double), a `Curl.Api` `POST` with a body gets it echoed back on a stream, with `?wt=uni`, and with `?wt=datagram`; the Notes record the runs.
- [ ] A unit test pins that a `Curl.Api` body reaches the WebTransport client as `-d` data.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1116, finding 3.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 8ab14af5 80c1f77b 3d836d5e.
