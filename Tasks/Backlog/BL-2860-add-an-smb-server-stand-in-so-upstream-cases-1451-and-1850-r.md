---
id: BL-2860
title: Add an SMB server stand-in so upstream cases 1451 and 1850 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitLibrary/UpstreamTestVariableSubstitution.cs, Curl.Conformance.UnitLibrary/SmbServerConnector.cs, Curl.Conformance.UnitTests/SmbServerConnectorTests.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2860 — Add an SMB server stand-in so upstream cases 1451 and 1850 run

## Goal

The case runner serves upstream's `smb` server in memory over `Curl.Protocol.Smb.UnitLibrary`'s protocol, so upstream cases 1451 (basic SMB request) and 1850 (two SMB requests in the same command line) run, and each is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason.

## Context

Gap scorecard 2026-10-19_1329 has both `excluded | reference-lacks:smb` (the reference build has no SMB). Measured 2026-10-19 on Windows 1451 is skipped with `the harness has no value for %SMBPORT`, and 1850 names `smb` the same way. Upstream's `runtests.pl` starts `tests/smbserver.py`-style `smbserver` (curl 8.22.0 `tests/smbserver.py`, impacket-based; https://github.com/curl/curl/tree/curl-8_22_0/tests) on `%SMBPORT` and serves the files of the case's `<reply><data>` under `TESTS/`; the cases use `-u 'curltest:curltest' smb://%HOSTIP:%SMBPORT/TESTS/<n>`. Curl implements SMB (`Curl.Protocol.Smb.UnitLibrary`, with NTLM in `Curl.Ntlm.UnitLibrary`); a server stand-in needs only what these two cases exercise: the SMB negotiate, NTLM session setup, tree connect to `TESTS` and a read of the named file. Read `test1451` and `test1850` for the exact bytes. Write the stand-in as an `IConnector`/connection in `Curl.Conformance.UnitLibrary` in the style of the existing stand-ins (`MqttServerConnector`, `TftpServerConnector`) with its own `*Tests`; add `SMBPORT` to `UpstreamTestVariableSubstitution.cs`. Hand-writing it is the answer, never a package (root CLAUDE.md).

## Acceptance criteria

- [ ] A new `SmbServerConnector` (name per the harness's pattern) serves negotiate, NTLM session setup, tree connect and file read from the case's reply data, with unit tests that drive it from `Curl.Protocol.Smb.UnitLibrary`'s client (no network) and cover every branch.
- [ ] `UpstreamTestVariableSubstitution` gives `SMBPORT` a value and `UpstreamCaseScreening` lists `smb` as an emulated server; unit tests pin both.
- [ ] Cases 1451 and 1850 end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`, or the first difference is in Notes and a follow-up task is filed.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
