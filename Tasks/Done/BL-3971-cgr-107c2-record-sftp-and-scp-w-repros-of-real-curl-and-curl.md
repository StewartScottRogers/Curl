---
id: BL-3971
title: CGR-107c2: Record SFTP and SCP -w repros of real curl and Curl, gate check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3966, BL-3970]
touches: [Curl.Protocol.Ssh.UnitTests/Cgr107TimingTests.cs, Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs, Curl.Protocol.Ssh.UnitTests/Cgr130Tests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3971 — CGR-107c2: Record SFTP and SCP -w repros of real curl and Curl, gate check

## Goal

The real-curl and Curl `-w` output of the SSH-08 repros (sftp download, scp download, sftp missing file) is recorded in Notes and shown to match in shape.

## Context

Split from BL-3957 (itself split from BL-3636, CGR-107). BL-3970 stamps the timings. BL-3957's Notes hold real curl 8.18.0 measurements and BL-3966's hold Curl's setup (WSL OpenSSH sshd, sshd_config lines for the Windows preset). Re-run Curl with the fix against that setup, using `-w "%{time_namelookup} %{time_connect} %{time_appconnect} %{time_pretransfer} %{time_starttransfer} %{time_total} %{speed_download}"`. Pin platform differences separately. Use no Python.

## Acceptance criteria

- [x] Notes record the real-curl and Curl output of the sftp download, scp download and sftp missing-file repros, with non-zero ordered times and a non-zero speed for the downloads and non-zero connect and total for the missing file.
- [x] Any platform-specific answer is pinned in its own test in `Curl.Protocol.Ssh.UnitTests/Cgr107TimingTests.cs`, platform-neutral otherwise.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3957 (over its cost cap).
- 2026-10-28 (lane 10): Real curl 8.18.0 (OpenSSL, WSL, libssh2 1.11.1, from BL-3957's Notes), -w `namelookup connect appconnect pretransfer starttransfer total speed_download`: sftp big.bin `0.000040 0.000253 0.062332 0.066513 0.067935 0.109434 45689639` exit 0; scp big.bin `0.000031 0.000235 0.035190 0.043126 0.043854 0.072444 69018828` exit 0; sftp missing.txt exit 78 `0.000031 0.000219 0.035066 0.000000 0.000000 0.039513 0`.
- 2026-10-28 (lane 10): Curl with BL-3970's timings first still gave `speed_download` 0 for both downloads. Cause: SshTransferClock and CountNewConnection (a session kept for the connection cache, as every console transfer's is) each built an empty `TransferReport`, hiding `BytesTransferred` from `%{size_download}` and `%{speed_download}`. Fix: `SshProtocolHandler.ReportOf` starts a new report with `DownloadSize = BytesTransferred`, as the engine's ConnectionEndPointRecorder does. Touches widened to `Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs` and `Curl.Protocol.Ssh.UnitTests/Cgr130Tests.cs` (kept-session path asserts); no other Doing task on origin/work/dark-factory named them. Tests: `Cgr107TimingTests.ExecuteAsync_Download_ReportsTheBytesAsTheDownloadSize` (sftp, scp) and DownloadSize asserts in Cgr130Tests.
- 2026-10-28 (lane 10): Curl (Windows Debug build, with the fix) against BL-3966's WSL OpenSSH 10.2 sshd (127.0.0.1:21966, `-k`, `--key`, 5 MB big.bin), same -w: sftp big.bin `0.030880 0.060230 0.338004 0.340481 0.440426 0.571289 9177281` exit 0; scp big.bin `0.039203 0.117007 0.877778 0.879386 1.097921 1.256516 4172553` exit 0; sftp missing.txt exit 78 `Could not open remote file for reading: No such file or directory`, `0.033271 0.116069 1.143859 0.000000 0.000000 1.197172 0`. Same shape as real curl: times non-zero and in order, speed non-zero for the downloads, pretransfer and starttransfer 0 with connect and total non-zero for the missing file. The values differ only in size (machine and build), not by platform, so no platform-specific test is needed; the new tests use the in-memory server and a fake clock and are platform-neutral, which keeps CI on Linux and macOS green.
- 2026-10-28 (lane 10): Full build clean; every fast test project passed (Ssh.UnitTests 1936). Measure-CodeQuality for Curl.Protocol.Ssh.UnitLibrary: 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: a26cc745 f32f26ac 779790c0.
- 2026-10-28: Doing -> Done. Curl's sftp and scp -w timings and speed_download match real curl's shape; repros recorded in Notes
