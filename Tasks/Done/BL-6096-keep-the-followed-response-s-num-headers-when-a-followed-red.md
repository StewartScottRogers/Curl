---
id: BL-6096
title: Keep the followed response's %{num_headers} when a followed redirect hop gets no response
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6095]
touches: [Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Core.UnitTests/RedirectFollowerTimedOutHopResponseCodeTests.cs, Curl.Output.UnitTests/TransferWriteOutVariablesFollowedHeaderCountTests.cs, Curl.Protocol.Abstractions.UnitTests/TransferReportHeaderCountTests.cs, Curl.Core.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-1473-a-followed-hop-with-no-response-keeps-the-followed-responses-code-version-and-content-type.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-6096 — Keep the followed response's %{num_headers} when a followed redirect hop gets no response

## Goal

Under `-L`, a transfer that fails on a followed redirect hop before any response arrives reports the followed response's header count in `%{num_headers}`, as curl does, instead of `0`.

## Context

Measured in BL-6095 (2026-11-28) with `Record-CurlExchange.ps1` and `C:\Program Files\Git\ucrt64\bin\curl.exe`: a 302 with four headers, to a listener that never answers (`-sS -L -m 2`) or to a port nothing listens on, prints `%{num_headers}` `4` while `%{header{...}}` is empty and `%{header_json}` is `{}`; Curl prints `0`. `TransferWriteOutVariables` counts the report's header lists, and no `TransferReport` property counts headers without naming their source, so one is needed; `RedirectFollower.RedirectChain.WithFollowedResponse` is where the followed response's other fields are carried (ADR-1473).

## Acceptance criteria

- [x] Both repros print `4` for `%{num_headers}`, with `%{header_json}` still `{}`.
- [x] Focused unit tests in the changed libraries' own `.UnitTests` projects pin it (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Done directly rather than through the /feature agent stages: a one-property change, and the run's cost cap is small.
- Plan: `TransferReport.FollowedHeaderCount` and a computed `HeaderCount`; `RedirectFollower` sets the former, `TransferWriteOutVariables` prints the latter. A failed hop that counts header lines of its own prints those (ADR-1477).
- Widened `touches` by the Abstractions unit test, `Curl.Core.UnitLibrary/CLAUDE.md` and ADR-1473 (its Consequences said `%{num_headers}` still printed 0); BL-6093, the other task in Doing, names none of them.
- Both repros (302 with four headers; silent listener under `-L -m 2`, and a closed port) print `4|{}|302` from real curl and from Curl (exit 28 and 7).
- Quality: the full `-Library` run could not finish before the deadline (93 of 171 test projects after 60 minutes), so it was stopped, Curl.Output.UnitTests and Curl.Protocol.Abstractions.UnitTests were run with coverage into the same results directory, and the script scored it with `-SkipTestRun`: Core, Output and Abstractions 100% line and branch, 0 failing members.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing. Criteria at claim: 1d52f2be 9563dfe4 3d836d5e.
- 2026-11-28: Doing -> Done. A followed hop with no response now keeps the followed response's %{num_headers}
