---
id: BL-2070
title: Bring HttpProtocolHandler.ExecuteAsync, RetryOfAsync, HttpRequestHeadFormatter.AppendCustomHeaders and HttpRedirectLocation.AppendEncoded inside the gates
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2062]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2070 — Bring HttpProtocolHandler.ExecuteAsync, RetryOfAsync, HttpRequestHeadFormatter.AppendCustomHeaders and HttpRedirectLocation.AppendEncoded inside the gates

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no member outside a gate.

## Context

Measured after BL-2062 (lane 7, 2026-10-11): four members remain. `HttpRequestHeadFormatter.AppendCustomHeaders` (complexity 18, `HttpRequestHeadFormatter.cs:337`), `HttpProtocolHandler.ExecuteAsync` (14, `HttpProtocolHandler.cs:269`), `HttpProtocolHandler.RetryOfAsync` (12, and 91.67% branch coverage, one branch untested) and `HttpRedirectLocation.AppendEncoded` (12, `HttpRedirectLocation.cs:220`). Split each into well-named methods; behaviour does not change; cover the missing `RetryOfAsync` branch with a test.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no member outside a gate, at 100% line and branch coverage.
- [ ] No existing test was changed or removed; any new member has its own tests.
- [ ] `dotnet build` and the fast tests pass.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
