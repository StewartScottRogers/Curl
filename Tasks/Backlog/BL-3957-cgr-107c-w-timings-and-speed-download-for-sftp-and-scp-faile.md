---
id: BL-3957
title: CGR-107c: -w timings and speed_download for SFTP and SCP, failed transfers too
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs, Curl.Protocol.Ssh.UnitTests/Cgr107TimingTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3957 — CGR-107c: -w timings and speed_download for SFTP and SCP, failed transfers too

## Goal

SFTP and SCP transfers fill `%{time_namelookup}`, `%{time_connect}`, `%{time_appconnect}`, `%{time_pretransfer}`, `%{time_starttransfer}`, `%{time_total}` and `%{speed_download}`, failed ones too, as curl 8.22.0 does.

## Context

Split from BL-3636 (third-party gap report issue CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3636's Context for the repro setups and commands). Measure real curl 8.22.0 with `Record-CurlExchange.ps1` or a C# file-based loopback server (no Python) before pinning a value. SSH-08: Curl prints zeros on every sftp/scp transfer; curl prints real values (`0.000059 0.000588 0.092217 0.116552 0.117091 0.140040 7488117`), and a failed transfer (missing file) still has `time_connect` and `time_total`. `SshProtocolHandler` and the Sftp/Scp transfer classes put no timings on the report. `%{num_connects}` is BL-3955's.

## Acceptance criteria

- [ ] An sftp and an scp download print non-zero, ordered times and a non-zero speed, and an sftp transfer of a missing file still prints a non-zero `time_connect` and `time_total`, as real curl.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project (the file in `touches`) fails before the fix and passes after it (ADR-0482).
- [ ] The real-curl and Curl output of each repro is recorded in Notes and matches, platform-neutral (Windows and Linux answers pinned separately where they differ).
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed library shows no member outside a gate.

## Notes

## Log

- 2026-10-28: Created.
