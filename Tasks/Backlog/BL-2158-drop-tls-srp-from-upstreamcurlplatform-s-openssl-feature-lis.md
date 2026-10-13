---
id: BL-2158
title: Drop TLS-SRP from UpstreamCurlPlatform's OpenSSL feature list: curl 8.22.0's lib/version.c no longer lists it
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2152]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2158 — Drop TLS-SRP from UpstreamCurlPlatform's OpenSSL feature list: curl 8.22.0's lib/version.c no longer lists it

## Goal

UpstreamCurlPlatform no longer expects TLS-SRP in the OpenSSL reference build's Features line.

## Context

curl 8.22.0's `lib/version.c` has no `TLS-SRP` feature (8.21.0 listed it under `USE_TLS_SRP`, checked 2026-10-13 from the curl-8_22_0 tag), so BL-2152 dropped it from `CurlVersionText.FeaturesLine`. `Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs` line 39 and `Curl.Conformance.UnitTests/UpstreamCurlPlatformTests.cs` line 13 still list it.

## Acceptance criteria

- [ ] `git grep -n TLS-SRP -- Curl.Conformance.UnitLibrary Curl.Conformance.UnitTests` finds nothing.
- [ ] `dotnet build` and the fast tests pass.

## Notes

## Log

- 2026-10-13: Created.
