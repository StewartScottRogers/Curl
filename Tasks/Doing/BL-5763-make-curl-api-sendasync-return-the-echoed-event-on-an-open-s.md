---
id: BL-5763
title: Make Curl.Api SendAsync return the echoed event on an open Socket.IO session instead of timing out
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary]
requirement: none
created: 2026-11-22
completed:
---
# BL-5763 — Make Curl.Api SendAsync return the echoed event on an open Socket.IO session instead of timing out

## Goal

A `Curl.Api` `SendAsync` that emits a Socket.IO event on `socketio://` or `socketios://` returns the echoed event (the same output `curl -d` writes) instead of blocking until the timeout (28).

## Context

From Surly cookbook bug report `2026-11-22T1308-Curl-bug-report.md` (batch 12 of the 1,000-recipe plan), finding 3. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. That build predates BL-5758 (2026-11-22: every extension receives a Curl.Api body exactly as `-d`), so first check whether it still reproduces on the current branch; if it does not, pin it with a test and close.

Repro: `surl --cert server.pem --key server.key --user alice:s3cret socketios://127.0.0.1:5002/`; `Curl.Api` `SendAsync` with a Socket.IO event and `Timeout = 5 s` throws `CurlException` 28, though surl logs the join and the event. So the event is sent (likely BL-5758's rule works) but the client keeps the session open waiting for more, where the console ends after the echo (or `Curl.Api` waits for the whole body before `SendAsync` returns while the console streams it). Find which: compare with how the console ends a Socket.IO emit (`--max-time`, a count, or the ack) and make `Curl.Api` return the same output on the same condition; if the console also waits for the timeout, `Curl.Api` should stream the response body so the caller sees the echo before the timeout. Recipe `socketio-log-in-to-a-namespace-in-csharp`.

## Acceptance criteria

- [ ] Against Surl's Socket.IO endpoint (or the test double), a `Curl.Api` emit returns the echoed event within a second, or delivers it on the response stream before the timeout, matching the console; the Notes record which and why.
- [ ] A unit test pins it with an injected `TimeProvider`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1308, finding 3.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 0cf2f8a5 7500a68d 3d836d5e.
