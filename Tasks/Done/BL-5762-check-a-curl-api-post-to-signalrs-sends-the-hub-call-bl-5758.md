---
id: BL-5762
title: Check a Curl.Api POST to signalrs:// sends the hub call (BL-5758 should cover it)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary, Curl.Api.UnitTests/Extensions/SignalRApiPostTests.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5762 — Check a Curl.Api POST to signalrs:// sends the hub call (BL-5758 should cover it)

## Goal

A `Curl.Api` `POST` with a hub invocation body to `signalr://` and `signalrs://` sends the invocation after the negotiate and returns the hub's answer, as `curl -d` does.

## Context

From Surly cookbook bug report `2026-11-22T1308-Curl-bug-report.md` (batch 12 of the 1,000-recipe plan), finding 2. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. That build predates BL-5758 (2026-11-22: every extension receives a Curl.Api body exactly as `-d`), so first check whether it still reproduces on the current branch; if it does not, pin it with a test and close.

Repro: `surl --cert server.pem --key server.key --user alice:s3cret signalrs://127.0.0.1:5001/`; a `Curl.Api` `POST` to `signalrs://127.0.0.1:5001/` with a hub invocation body gets the negotiate response only and no invocation reaches surl, over https or http. BL-5611 (2026-11-20) and BL-5758 (2026-11-22) both aimed at this. Check the current build against Surl's SignalR hub; if it still fails, find where the SignalR client takes the negotiate path for a `Curl.Api` request (a different request shape from `ExtensionBodyParityTests`'s, for example authentication set or an https base). Recipe `signalrs-log-in-to-a-hub-in-csharp`.

## Acceptance criteria

- [x] Against Surl's SignalR hub (or the test double), a `Curl.Api` `POST` with an invocation body to `signalr://` and `signalrs://`, with credentials, sends the invocation and returns the answer; the Notes record the run and whether it already worked on the current branch.
- [x] A unit test pins the cookbook's exact request shape.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Already worked on the current branch (BL-5758, ADR-1336): a Curl.Api POST to signalr:// or signalrs:// with credentials reaches the handler as the -T upload (UploadSchemes lists signalr), which SignalRProtocolHandler.CallBodyAsync reads whole as the invocation (Upload_IsSentAsTheInvocationExactlyAsDataWould). No live Surl run: no Surl in this lane; the pin is SignalRApiPostTests (Curl.Api.UnitTests), added to touches. No production change.

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1308, finding 2.
- 2026-11-22: Backlog -> Doing. Criteria at claim: df862df0 02aa84ae 3d836d5e.
- 2026-11-22: Doing -> Done. Already worked on the current branch via BL-5758; pinned by SignalRApiPostTests
