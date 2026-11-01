---
id: BL-3957
title: CGR-107c: -w timings and speed_download for SFTP and SCP, failed transfers too
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3966]
touches: [Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs, Curl.Original.Protocol.Ssh.UnitLibrary/ReceivedDataReportingStream.cs, Curl.Original.Protocol.Ssh.UnitTests/Cgr107TimingTests.cs, Curl.Original.Protocol.Ssh.UnitTests/Cgr030Tests.cs, Curl.Original.Protocol.Ssh.UnitTests/Cgr130Tests.cs, Curl.Original.Protocol.Ssh.UnitTests/SshProtocolHandlerAdversarialTests.cs, Curl.Original.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.cs, Curl.Original.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.Compression.cs, Curl.Original.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.ConnectionReset.cs, Curl.Original.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.DiagnosticLog.cs, Curl.Original.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.KnownHostsWrite.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3957 — CGR-107c: -w timings and speed_download for SFTP and SCP, failed transfers too

## Goal

SFTP and SCP transfers fill `%{time_namelookup}`, `%{time_connect}`, `%{time_appconnect}`, `%{time_pretransfer}`, `%{time_starttransfer}`, `%{time_total}` and `%{speed_download}`, failed ones too, as curl 8.22.0 does.

## Context

Split from BL-3636 (third-party gap report issue CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3636's Context for the repro setups and commands). Measure real curl 8.22.0 with `Record-CurlExchange.ps1` or a C# file-based loopback server (no Python) before pinning a value. SSH-08: Curl prints zeros on every sftp/scp transfer; curl prints real values (`0.000059 0.000588 0.092217 0.116552 0.117091 0.140040 7488117`), and a failed transfer (missing file) still has `time_connect` and `time_total`. `SshProtocolHandler` and the Sftp/Scp transfer classes put no timings on the report. `%{num_connects}` is BL-3961's.

## Acceptance criteria

- [ ] An sftp and an scp download print non-zero, ordered times and a non-zero speed, and an sftp transfer of a missing file still prints a non-zero `time_connect` and `time_total`, as real curl.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project (the file in `touches`) fails before the fix and passes after it (ADR-0482).
- [ ] The real-curl and Curl output of each repro is recorded in Notes and matches, platform-neutral (Windows and Linux answers pinned separately where they differ).
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed library shows no member outside a gate.

## Notes

- 2026-10-28 (lane 9): Work done but uncommitted, so it goes into the stash. `SshProtocolHandler` now has a private `SshTransferClock` that stamps `TransferReport.Timings` on every result, failures included. Name lookup and connect come from the connector. Appconnect is taken at "Authentication complete" (curl's SSH_AUTH_DONE sets TIMER_APPCONNECT). Pretransfer is taken when the transfer starts, starttransfer at the first write to the output (new `ReceivedDataReportingStream.Written` hook), and the total when the handler returns. A failure before the first byte has no pretransfer, as measured. Posttransfer stays unset (not in scope). Choice: pretransfer at the start of the transfer rather than after the SFTP open, because the open is inside SftpFileDownload; the times are still non-zero and in order. Since every result now carries a report, the SSH tests that compared whole `TransferResult`s compare `with { Report = null }` (touches widened to those files; no other Doing task names them). New tests are in Cgr107TimingTests.cs (5 tests; they fail before the fix because Report is null). Ssh.UnitTests passed 1934/1934, the solution build is clean, and every fast test project passed. Measure-CodeQuality for Curl.Original.Protocol.Ssh.UnitLibrary: 0 failing members.
- 2026-10-28 (lane 9): Real curl 8.18.0 (OpenSSL, WSL, libssh2 1.11.1) against OpenSSH 10.2 sshd in WSL (127.0.0.1:21957, internal-sftp, PEM RSA client key, 5 MB big.bin), -w "namelookup connect appconnect pretransfer starttransfer total speed_download": sftp big.bin `0.000040 0.000253 0.062332 0.066513 0.067935 0.109434 45689639`, exit 0. scp big.bin `0.000031 0.000235 0.035190 0.043126 0.043854 0.072444 69018828`, exit 0. sftp missing.txt: exit 78 `Could not open remote file for reading: No such file or directory`, `0.000031 0.000219 0.035066 0.000000 0.000000 0.039513 0`. Curl (Windows build) against the same sshd failed the key exchange (exit 2), though its -w already showed real values: `0.118038 0.177401 0.000000 0.000000 0.000000 0.231942 0`. Measuring Curl's successful repros is filed as BL-3966.
- 2026-10-28 (BL-3966, lane 9): Curl (Windows Debug build, no -w fix applied in that checkout) against a local OpenSSH 10.2 sshd in WSL (`sshd -D` kept alive by a hidden process, 127.0.0.1:21966, internal-sftp, PEM RSA client key via `--key`, `-k`, 5 MB big.bin), -w `namelookup connect appconnect pretransfer starttransfer total speed_download`: sftp big.bin `0.040959 0.072382 0.429473 0.431167 0.505256 0.672083 0`, exit 0. scp big.bin `0.037849 0.073056 0.397086 0.399552 0.436978 0.556650 0`, exit 0. sftp missing.txt: exit 78 `Could not open remote file for reading: No such file or directory`, `0.054473 0.113444 0.513535 0.000000 0.000000 0.603177 0`. sshd_config lines that let the Windows preset exchange keys: `KexAlgorithms diffie-hellman-group14-sha256`, `HostKeyAlgorithms rsa-sha2-256,rsa-sha2-512` (3072-bit RSA PEM host key), `PubkeyAcceptedAlgorithms rsa-sha2-256,rsa-sha2-512,ssh-rsa`, `Ciphers aes256-ctr`, `MACs hmac-sha2-256`, `UsePAM no`, `StrictModes no`, `Subsystem sftp internal-sftp`. Note speed_download is 0 in all three here.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 454b7427 d9aa5d84 40bddbd7 bc4b41f1.
- 2026-10-28: Doing -> Backlog. Waiting on BL-3966: Curl's own Windows run of the SSH-08 repros needs an sshd its SSH preset can exchange keys with; the fix is in the stash
- 2026-10-28: Backlog -> Deferred. Split for size (cost cap) into BL-3970 and BL-3971
