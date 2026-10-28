---
id: BL-3964
title: Follow a redirect to a UTF-8 non-ASCII Location host on Windows as curl does (upstream test1448, CGR-059)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3963]
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-28
completed:
---
# BL-3964 — Follow a redirect to a UTF-8 non-ASCII Location host on Windows as curl does (upstream test1448, CGR-059)

## Goal

On Windows, a redirect whose `Location` host is typed as UTF-8 (upstream test1448, `åäö.se`) is followed or refused exactly as real curl 8.22.0 does.

## Context

Second part of BL-3953 (CGR-059). After BL-3963 makes `CurlUrlHost` read a host as Windows-1252 on Windows, upstream test1448 (redirect to a UTF-8 IDN `åäö.se`) no longer follows the redirect. Real curl sees the raw UTF-8 bytes of a `Location` host, so first measure real Windows curl on test1448 with Record-CurlExchange.ps1, then decide where a `Location` host enters `CurlUrlHost` and which reading it gets. Keep or hold the case in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` per platform, as the measurement says.

## Acceptance criteria

- [ ] Real curl and Curl, run on test1448 and one more `Location` host with a non-ASCII byte, give the same exit code, stdout bytes, stderr text and request bytes on Windows; the commands and results are recorded in Notes.
- [ ] A focused unit test in `Curl.Protocol.Abstractions.UnitTests` pins how a `Location` host is read and fails before the fix.
- [ ] The test1448 line in `PassingUpstreamCases.txt` is right for the measured answer on each platform, and the ratchet check passes.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3953, which went over its cost cap.
- BL-3963 took test1448 off `PassingUpstreamCases.txt` (it failed on Windows once a host is read as Windows-1252), so CI stays green until this task lands; put it back for the platforms the measurement says. Measured by BL-3963: real Windows curl 8.22.0 ignores `LC_ALL=C.UTF-8` (`%C3%BC` still gives `xn--14-ria7423a`), so the test's `<setenv>` does not make it read UTF-8.
- Continues the stashed work of BL-3953.

## Log

- 2026-10-28: Created.
