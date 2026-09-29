---
id: BL-881
title: Make the revoked stapled OCSP response test in Curl.Networking.UnitTests pass every run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-881 — Make the revoked stapled OCSP response test in Curl.Networking.UnitTests pass every run

## Goal

`AuthenticateAsClientAsync_WithCertStatusAndARevokedStapledResponse_FailsWithExit91AndTheReason (True)` in `Curl.Networking.UnitTests` passes on every run instead of failing now and then.

## Context

- Seen failing in the fast run by BL-822 (2026-09-28, "Alert instead of Handshake") and by BL-845 (2026-09-29): on Windows, `dotnet test Curl.Networking.UnitTests --no-build --filter "TestCategory!=Integration"` passed once and failed once in two back-to-back runs with no change to the code.
- The failure is a race, which suggests the loopback TLS server's alert and the client's handshake result arrive in either order. Find which side decides the outcome and make the test (or the code, if it misreports the reason) deterministic.
- CI blocks the dark factory's merge on a red run, so a flaky test can hold up a shift.

## Acceptance criteria

- [ ] The cause of the intermittent failure is named in Notes: which order of events makes it fail.
- [ ] The test passes 50 runs in a row: `for ($i = 0; $i -lt 50; $i++) { dotnet test Curl.Networking.UnitTests --no-build --filter "FullyQualifiedName~RevokedStapledResponse" }` shows no failure.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-29: Created.
