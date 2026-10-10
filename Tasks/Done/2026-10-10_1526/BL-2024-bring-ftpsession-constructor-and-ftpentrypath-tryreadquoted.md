---
id: BL-2024
title: Bring FtpSession constructor and FtpEntryPath.TryReadQuoted under complexity 10 (BL-2023 follow-up)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2024 — Bring FtpSession constructor and FtpEntryPath.TryReadQuoted under complexity 10 (BL-2023 follow-up)

## Goal

Measure-CodeQuality reports no member of Curl.Protocol.Ftp.UnitLibrary over complexity 10 or CRAP 30, with line and branch coverage still 100%.

## Context

BL-2023 left two members over 10 in the coverage report (CA1502 in the build already passed): `FtpEntryPath.TryReadQuoted` (14, BL-1983) and the `FtpSession` primary constructor (12, its field initializers' branches).

## Acceptance criteria

- [x] `FtpEntryPath.TryReadQuoted` and the `FtpSession` constructor are each at complexity 10 or under in Measure-CodeQuality's report.
- [x] Measure-CodeQuality reports 100% line and branch coverage and 0 failing members for Curl.Protocol.Ftp.UnitLibrary.
- [x] `dotnet build` is clean and the fast tests pass, behaviour unchanged.

## Notes

Pure refactor, so the feature pipeline's plan/conformance stages were skipped: no behaviour changes, existing tests cover every path. `TryReadQuoted` now takes its control-character stop into the loop condition through `IsControl`, and the doubled-quote check is `IsDoubledQuote`. The constructor's four branching field initializers (`controlPeerAddress`, `maxFileSize`, `ignoresContentLength`, `transferType`) call static helpers `PeerAddressOr`, `MaxFileSizeOf`, `IgnoresContentLength`, `TransferTypeOf`. Measured after: Ftp library 100% line / 100% branch, 355 members, 0 failing, worst CRAP 10.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. FtpEntryPath.TryReadQuoted and the FtpSession constructor under complexity 10; Ftp library 100/100, worst CRAP 10
