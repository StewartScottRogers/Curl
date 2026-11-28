---
id: BL-6096
title: Keep the followed response's %{num_headers} when a followed redirect hop gets no response
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6095]
touches: [Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Core.UnitTests/RedirectFollowerTimedOutHopResponseCodeTests.cs, Curl.Output.UnitTests/TransferWriteOutVariablesFollowedHeaderCountTests.cs]
requirement: none
created: 2026-11-28
completed:
---
# BL-6096 — Keep the followed response's %{num_headers} when a followed redirect hop gets no response

## Goal

Under `-L`, a transfer that fails on a followed redirect hop before any response arrives reports the followed response's header count in `%{num_headers}`, as curl does, instead of `0`.

## Context

Measured in BL-6095 (2026-11-28) with `Record-CurlExchange.ps1` and `C:\Program Files\Git\ucrt64\bin\curl.exe`: a 302 with four headers, to a listener that never answers (`-sS -L -m 2`) or to a port nothing listens on, prints `%{num_headers}` `4` while `%{header{...}}` is empty and `%{header_json}` is `{}`; Curl prints `0`. `TransferWriteOutVariables` counts the report's header lists, and no `TransferReport` property counts headers without naming their source, so one is needed; `RedirectFollower.RedirectChain.WithFollowedResponse` is where the followed response's other fields are carried (ADR-1473).

## Acceptance criteria

- [ ] Both repros print `4` for `%{num_headers}`, with `%{header_json}` still `{}`.
- [ ] Focused unit tests in the changed libraries' own `.UnitTests` projects pin it (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
