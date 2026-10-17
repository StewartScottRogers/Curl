---
id: BL-2584
title: Curl.Engine: send Proxy-Authorization from CURLOPT_PROXYUSERNAME and PROXYPASSWORD in a CONNECT (test503)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2584 — Curl.Engine: send Proxy-Authorization from CURLOPT_PROXYUSERNAME and PROXYPASSWORD in a CONNECT (test503)

## Goal

A Libcurl handle with `CURLOPT_PROXYUSERNAME` and `CURLOPT_PROXYPASSWORD` sends `Proxy-Authorization: Basic` in its `CONNECT`, as libcurl 8.22.0 does.

## Context

Found by BL-2527 running test503 through the ported `lib503`: `<verify><proxy>` differs at line 3, expected `Proxy-Authorization: Basic dGVzdCUyMDppbmclNDE=`, got `Proxy-Connection: Keep-Alive`. The program sets `CURLOPT_PROXY`, `CURLOPT_HTTPPROXYTUNNEL`, `CURLOPT_PROXYUSERNAME` `test%20` and `CURLOPT_PROXYPASSWORD` `ing%41` (sent as given, not URL-decoded).

## Acceptance criteria

- [ ] test503 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 6c7087d7 904b57ec 3d836d5e.
