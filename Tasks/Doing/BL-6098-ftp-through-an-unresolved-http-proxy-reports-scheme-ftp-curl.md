---
id: BL-6098
title: ftp through an unresolved HTTP proxy reports %{scheme} ftp; curl reports http
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/RoutingFtpProtocolHandler.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.Scheme.cs, Curl.Engine.UnitTests/RoutingFtpProtocolHandlerSchemeTests.cs]
requirement: none
created: 2026-11-28
completed:
---
# BL-6098 — ftp through an unresolved HTTP proxy reports %{scheme} ftp; curl reports http

## Goal

An `ftp` URL sent through an HTTP proxy that cannot be resolved reports `%{scheme}` as `http`, as curl does.

## Context

Found while doing BL-6041 (2026-11-28, Windows, real curl 8.22.0 Schannel):
`-s -x http://nonexistent.invalid:1 -w "%{exitcode}|%{scheme}" ftp://example.test/` prints
`5|http` from real curl and `5|ftp` from Curl. `RoutingFtpProtocolHandler` hands such a
transfer to the HTTP handler; `TransferInfoFromReport.Scheme.cs` picks the scheme. Name the
test file in `touches` anew if the fix lands elsewhere.

## Acceptance criteria

- [ ] The command line above prints `5|http`, matching real curl.
- [ ] A focused unit test in `Curl.Engine.UnitTests` pins the behaviour (ADR-0482).
- [ ] `Measure-CodeQuality.ps1` reports no member this task changes or adds outside a gate.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing. Criteria at claim: e71714e0 31332653 d08bff81 3d836d5e.
