---
id: BL-6082
title: https: report %{time_total} of at least the -m value when -m ends a TLS handshake
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6019, BL-6081]
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpTransferDeadline.cs, Curl.Engine.UnitTests/Std231TimeTotalTests.cs, Curl.Original.Protocol.Http.UnitTests/HttpTransferDeadlineStd231Tests.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-6082 — https: report %{time_total} of at least the -m value when -m ends a TLS handshake

## Goal

An `https` transfer whose TLS handshake is ended by `-m` reports a `%{time_total}` of at least the `-m` value, as curl does.

## Context

The rest of fitness finding STD-231 after BL-6081; BL-6028 in `Tasks/Deferred` holds the finding verbatim, the measurements and the notes. Repro: a `TcpListener` on `127.0.0.1:29990` that is never accepted from, then `curl -s -m 2 -o /dev/null -w "[%{exitcode} t=%{time_total} nc=%{num_connects}]\n" https://127.0.0.1:29990/`; real curl prints `[28 t=2.0x nc=1]`, Curl printed `t=1.97..1.99`.

Cause found: the HTTP handler's own timings start after the `-m` watchdog's clock (the report already has `Timings`, so `EndPointReportingProtocolHandler.StampTimingsAndUploadSize` keeps them); plain `http://10.255.255.1/ -m 2` shows the same (`t=1.97`). curl's total counts from the transfer start, where `-m` starts. Likely fix: in the `-m` cancellation path, a total under `MaxTime` moves `Started` back to `completed - MaxTime`, or the HTTP deadline report (`HttpTransferDeadline.cs`) starts at the watchdog's start.

## Acceptance criteria

- [ ] The `https` repro command with `-m 2` prints `nc=1` and `t` of at least 2.
- [ ] The same command with `wss://` still prints `nc=1` and `t` of at least 2.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected time (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-6028. `HttpTransferDeadline.cs` and the two test files are named here from BL-6028's notes on where the remaining fix lies.

## Log

- 2026-11-27: Created.
