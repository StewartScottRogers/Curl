---
id: BL-2695
title: Fix the MIME boundary of a libtest engine as upstream's debug build does (upstream test1972)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2695 — Fix the MIME boundary of a libtest engine as upstream's debug build does (upstream test1972)

## Goal

Upstream case 1972 passes: the ratchet's libtest engine builds MIME bodies with the fixed boundary upstream's debug build uses (`------------------------qrstuvwxyz0123456789AB`), so a MIME post signed with AWS SigV4 matches byte for byte.

## Context

Found by BL-2675 (AWS SigV4 libtest cases with the harness's Debug stand-in and an epoch signing clock). test1972 (lib1972) now runs and differs at line 8: expected `--------------------------qrstuvwxyz0123456789AB`, got a random boundary. Upstream's debug build gives a fixed boundary in tests (find the debug-only hook in lib/rand.c or lib/mime.c of curl 8.22.0 first); the engine has ManagedEasyTransfer.CreateFormBoundary as an internal hook (ADR-0627). Give UpstreamConformanceTests.CreateEngine's engine the fixed boundary (an engine-level option beside signingClock, BL-2675), and check what the curl tool's ratchet already does for -F cases. Run it with `CURL_UPSTREAM_CASE=1972`.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=1972` with NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust passes, and 1972 is on Curl.Conformance.UnitTests/PassingUpstreamCases.txt.
- [ ] A focused unit test in Curl.Engine.UnitTests pins the engine's fixed boundary (ADR-0482).
- [ ] dotnet build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
