---
id: BL-3969
title: CGR-107c2: Record SFTP and SCP -w repros of real curl and Curl, gate check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3966, BL-3968]
touches: [Curl.Protocol.Ssh.UnitTests/Cgr107TimingTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3969 — CGR-107c2: Record SFTP and SCP -w repros of real curl and Curl, gate check

## Goal

The real-curl and Curl `-w` output of the SSH-08 repros (sftp download, scp download, sftp missing file) is recorded in Notes and shown to match in shape.

## Context

Split from BL-3957 (itself split from BL-3636, CGR-107). BL-3968 stamps the timings. BL-3957's Notes hold real curl 8.18.0 measurements and BL-3966's hold Curl's setup (WSL OpenSSH sshd, sshd_config lines for the Windows preset). Re-run Curl with the fix against that setup, using `-w "%{time_namelookup} %{time_connect} %{time_appconnect} %{time_pretransfer} %{time_starttransfer} %{time_total} %{speed_download}"`. Pin platform differences separately. Use no Python.

## Acceptance criteria

- [ ] Notes record the real-curl and Curl output of the sftp download, scp download and sftp missing-file repros, with non-zero ordered times and a non-zero speed for the downloads and non-zero connect and total for the missing file.
- [ ] Any platform-specific answer is pinned in its own test in `Curl.Protocol.Ssh.UnitTests/Cgr107TimingTests.cs`, platform-neutral otherwise.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3957 (over its cost cap).

## Log

- 2026-10-28: Created.
