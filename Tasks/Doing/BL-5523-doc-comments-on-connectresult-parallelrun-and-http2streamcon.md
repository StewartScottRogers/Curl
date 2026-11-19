---
id: BL-5523
title: Doc comments on ConnectResult, ParallelRun and Http2StreamConnection say what the code does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/ConnectResult.cs, Curl.Console/ParallelRun.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5523 — Doc comments on ConnectResult, ParallelRun and Http2StreamConnection say what the code does

## Goal

Three doc comments that this shift's changes made false say what their code now does, as CLAUDE.md's "say what it does, do what it says" requires.

## Context

Found by the shift-end review of 2026-11-18 (ADR-0710), pr-review-toolkit type-design-analyzer and code-reviewer (Important); each is confirmed against the code.

1. **`Curl.Protocol.Abstractions.UnitLibrary/ConnectResult.cs:141-155`.** The docs of `ConnectReplyHeadersStored` and `ProxyConnectHeaderBytes` say "`0` when … the connect failed".
   - `ConnectResult.Failed` now sets both (around lines 337-338), and `TcpConnector.TunnelFailure` passes the refused CONNECT reply's header count and bytes.
   - The first doc also says "the CONNECT that opened a tunnel".
   - `Failed`'s parameter is named `proxyConnectHeadersStored`, while `Opened`'s parameter and the property are named `connectReplyHeadersStored`, which is two names for one concept.
2. **`Curl.Console/ParallelRun.cs` (around lines 182-205).**
   - `NoteFatalSetupFailureOfLastHeld` is `void` and only records the failure. Its summary describes ending the run, and it has a `<returns>` for a task.
   - That text belongs to `AbandonFirstBatchOnFatalFailureAsync`, which has no doc comment.
3. **`Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:614`.** The summary of `IsEndedBy` still says a frame ends the response when "the final head came and the request wants no body". The method is now just `frame.IsEndStream`; BL-5468 moved the `-I` ending to the first DATA frame or the peer closing. The `EndsAtFinalHead` property doc (around line 64) needs checking against the same change.

## Acceptance criteria

- [ ] The docs of `ConnectResult.ConnectReplyHeadersStored` and `ProxyConnectHeaderBytes` state that a refused CONNECT reports its reply's header count and bytes.
- [ ] `ConnectResult.Failed`'s parameter is named `connectReplyHeadersStored`.
- [ ] `NoteFatalSetupFailureOfLastHeld`'s doc says it records the failure, with no `<returns>`.
- [ ] `AbandonFirstBatchOnFatalFailureAsync` carries the doc that describes ending the run.
- [ ] `Http2StreamConnection.IsEndedBy` and `EndsAtFinalHead` docs match the BL-5468 behaviour.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: b36c7937 e2507838 707efe9c c0b71b45 fd0d28df 3d836d5e.
