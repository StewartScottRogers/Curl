---
id: BL-1544
title: Make SslStreamTlsProviderTests' CommonName to VersionRange files write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457, BL-1543]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1544 — Make SslStreamTlsProviderTests' CommonName to VersionRange files write descriptive diagnostic output

## Goal

Every test declared in `Curl.Networking.UnitTests`' `SslStreamTlsProviderTests` partial files `.CommonName`, `.DescribeTrust`, `.HandshakeEvent`, `.PeerCertificates`, `.PinnedPublicKey`, `.RevocationBestEffort`, `.RevocationListFile`, `.SessionTickets`, `.Timings`, `.VerifyResult` and `.VersionRange` (11 files, 74 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs (TLS settings, certificates by subject and thumbprint), Act result and assertion context (plus `PHASE` timings for the TLS handshake), with no test's logic or assertions changed; with BL-1543 Done, the whole class then does.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- BL-1543 covers the class's other 8 files and must be Done first, so this task's check covers the whole class.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.SslStreamTlsProviderTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Resumed from lane 4's branch `factory/BL-1544-lane-4-20261006-200306` by cherry-pick.
  Its one conflict: master had since moved the Integration test
  `AuthenticateAsClientAsync_WhenTheServerSendsAnIntermediate_ReportsItAfterTheServersCertificate`
  out of `.PeerCertificates.cs` into the networking integration test project, so this
  task keeps master's side and no longer instruments it here.
- Counts across the 11 files, before and after (2026-10-07, after the move above):
  `Assert.` 180 -> 180, `[TestMethod` 73 -> 73, `[DataRow(` 47 -> 47.
- Class run with the detailed logger: 317 tests, 307 passed, 10 skipped by `OSCondition`;
  every run test writes an `END` line and none matches the zero-count pattern.
- No test printed a `SLOW:` line, so no follow-up task.
- Shared helpers now write the diagnostics their callers need: `ReportingHandshakeAsync`
  (HandshakeEvent), `PinReportingHandshakeAsync` (PinnedPublicKey), `EchoOverAsync`
  (SessionTickets) and `RevocationListOptions` (RevocationListFile) write ARRANGE, a
  `handshake` PHASE and ACT, so they became instance methods. Paths are written as
  `<test folder>/...` or a bare file name, never the temporary folder itself.
- Where a test called the method under test inside its `Assert` (VerifiedHostName,
  ListPeerCertificates, HasOnlyUnavailableRevocationStatus, FollowTicketRecordsAfterHandshake),
  the result is now taken once into a variable that both the ACT line and the unchanged
  assertion use, so nothing is called twice.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 4 could not integrate: push kept being refused. The work is on branch factory/BL-1544-lane-4-20261006-200306; start with git cherry-pick --no-commit factory/BL-1544-lane-4-20261006-200306 and fix it.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in SslStreamTlsProviderTests' CommonName to VersionRange files writes ARRANGE, ACT and ASSERT lines; with BL-1543 the whole class does.
