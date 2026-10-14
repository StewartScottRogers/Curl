---
id: BL-2184
title: Bring RedirectFollower.HopMail and HttpProtocolHandler.WriteHeadersAsync to 100% branch coverage and CurlUrlHost.TryNormalizeName under complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2184 — Bring RedirectFollower.HopMail and HttpProtocolHandler.WriteHeadersAsync to 100% branch coverage and CurlUrlHost.TryNormalizeName under complexity 10

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary,Curl.Protocol.Http.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary` reports no failing member.

## Context

Measured by BL-2182's run on 2026-10-13, none of them changed by it:
`RedirectFollower.HopMail` (`Curl.Core.UnitLibrary\RedirectFollower.cs:635`) at 90% branch,
`HttpProtocolHandler.WriteHeadersAsync` (`HttpProtocolHandler.cs:2790`) at 83.33% branch, and
`CurlUrlHost.TryNormalizeName` (`CurlUrlHost.cs:87`) at complexity 12.

## Acceptance criteria

- [ ] The three members meet every gate: 100% line and branch coverage, complexity at most 10, CRAP at most 30.

## Notes

## Log

- 2026-10-13: Created.
