---
id: BL-2234
title: Run the HSTS-through-proxy cases 446 and 780-783 that skip for the Debug feature
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2234 — Run the HSTS-through-proxy cases 446 and 780-783 that skip for the Debug feature

## Goal

Make upstream cases 446, 780, 781, 782 and 783 (HSTS through the http-proxy to the HTTPS stand-in) run in the conformance harness instead of skipping with "Curl lacks the feature Debug".

## Context

Found by BL-2225, which made the http-proxy stand-in tunnel a `CONNECT` to `%HTTPSPORT` into `HttpsServerConnector`: of the seven cases it unscreened, 1849 and 3106 now pass, and these five skip for their `<features>` `Debug`. Upstream's debug build lets the cases fix the HSTS clock (read each case's `<setenv>` / `<features>` to confirm what Debug stands for, likely `CURL_TIME`). `UpstreamCurlPlatform.WithFeatures` already adds `Debug` for the httpsig cases, whose fixed clock the tests supply (BL-2183); decide whether the same stand-in can supply the HSTS clock (and whether Curl honours the variable), and record the decision in an ADR if the behaviour is new.

## Acceptance criteria

- [ ] Cases 446, 780, 781, 782 and 783 no longer skip for `Debug`; each that passes is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, each that fails is named in Notes with a follow-up task per distinct cause.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; the fast tests are green; `Curl.Conformance.UnitLibrary/CLAUDE.md` says how the Debug feature is stood in for these cases.

## Notes

## Log

- 2026-10-14: Created.
