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
completed: 2026-09-29
---
# BL-881 — Make the revoked stapled OCSP response test in Curl.Networking.UnitTests pass every run

## Goal

`AuthenticateAsClientAsync_WithCertStatusAndARevokedStapledResponse_FailsWithExit91AndTheReason (True)` in `Curl.Networking.UnitTests` passes on every run instead of failing now and then.

## Context

- Seen failing in the fast run by BL-822 (2026-09-28, "Alert instead of Handshake") and by BL-845 (2026-09-29): on Windows, `dotnet test Curl.Networking.UnitTests --no-build --filter "TestCategory!=Integration"` passed once and failed once in two back-to-back runs with no change to the code.
- The failure is a race, which suggests the loopback TLS server's alert and the client's handshake result arrive in either order. Find which side decides the outcome and make the test (or the code, if it misreports the reason) deterministic.
- CI blocks the dark factory's merge on a red run, so a flaky test can hold up a shift.

## Acceptance criteria

- [x] The cause of the intermittent failure is named in Notes: which order of events makes it fail.
- [x] The test passes 50 runs in a row: `for ($i = 0; $i -lt 50; $i++) { dotnet test Curl.Networking.UnitTests --no-build --filter "FullyQualifiedName~RevokedStapledResponse" }` shows no failure.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- **Cause.** The race was between the fake server reading the client's alert and the test
  disposing the server's stream. On a revoked staple the client sends a `bad_certificate`
  alert (protected with its handshake keys) and returns exit 91; the test then disposes
  `server` and awaits the server task, ignoring I/O and TLS-alert exceptions.
  `Tls13RecordTestServer.ReceiveClientFinishedAsync` asserted the client's next record was
  `Handshake`. When the test disposed the stream first, the server's read threw
  `ObjectDisposedException` (ignored) and the test passed. When the server task read the
  alert first (usual in a quiet run, occasional under full-suite load), it failed
  `Assert.AreEqual(Handshake, Alert)`. `AssertFailedException` is not ignored, so the test
  failed. That is the "Alert instead of Handshake" BL-822 saw. The production code was
  right: the client's exit code and message never changed.
- **Already fixed by BL-821** (commit 4c568be6, 2026-09-29 05:28). It made the fake server
  throw `TlsAlertException` on a client alert, and `IgnoreServerFailureAsync` ignores that
  exception, so both orders now pass. Every failure recorded (BL-822, BL-845, BL-564)
  happened on code before that commit.
- **Verified.** The server file from before BL-821 (3691b69c) was put back temporarily and
  20 filtered runs were made: 19 failed, 1 passed. That confirms the cause. With the
  current file, 50 filtered runs in a row passed, and so did `dotnet build Curl.slnx
  -warnaserror` (0 warnings) and the full fast run. No code change was needed.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Revoked stapled OCSP test passes every run; the fake server's Handshake assert on the client's alert was the race, fixed by BL-821
