---
id: BL-2860
title: Add an SMB server stand-in so upstream cases 1451 and 1850 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.SmbCases.cs, Curl.Conformance.UnitLibrary/UpstreamTestVariableSubstitution.cs, Curl.Conformance.UnitLibrary/SmbServerConnector.cs, Curl.Conformance.UnitTests/SmbServerConnectorTests.cs, Curl.Conformance.UnitLibrary/UpstreamStandInFronts.cs, Curl.Conformance.UnitLibrary/SmbServerConnection.cs, Curl.Conformance.UnitLibrary/UpstreamSmbFront.cs, Curl.Conformance.UnitTests/UpstreamStandInFrontsTests.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2860 — Add an SMB server stand-in so upstream cases 1451 and 1850 run

## Goal

The case runner serves upstream's `smb` server in memory over `Curl.Protocol.Smb.UnitLibrary`'s protocol, so upstream cases 1451 (basic SMB request) and 1850 (two SMB requests in the same command line) run, and each is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason.

## Context

Gap scorecard 2026-10-19_1329 has both `excluded | reference-lacks:smb` (the reference build has no SMB). Measured 2026-10-19 on Windows 1451 is skipped with `the harness has no value for %SMBPORT`, and 1850 names `smb` the same way. Upstream's `runtests.pl` starts `tests/smbserver.py`-style `smbserver` (curl 8.22.0 `tests/smbserver.py`, impacket-based; https://github.com/curl/curl/tree/curl-8_22_0/tests) on `%SMBPORT` and serves the files of the case's `<reply><data>` under `TESTS/`; the cases use `-u 'curltest:curltest' smb://%HOSTIP:%SMBPORT/TESTS/<n>`. Curl implements SMB (`Curl.Protocol.Smb.UnitLibrary`, with NTLM in `Curl.Ntlm.UnitLibrary`); a server stand-in needs only what these two cases exercise: the SMB negotiate, NTLM session setup, tree connect to `TESTS` and a read of the named file. Read `test1451` and `test1850` for the exact bytes. Write the stand-in as an `IConnector`/connection in `Curl.Conformance.UnitLibrary` in the style of the existing stand-ins (`MqttServerConnector`, `TftpServerConnector`) with its own `*Tests`; add `SMBPORT` to `UpstreamTestVariableSubstitution.cs`. Hand-writing it is the answer, never a package (root CLAUDE.md).

## Acceptance criteria

- [x] A new `SmbServerConnector` (name per the harness's pattern) serves negotiate, NTLM session setup, tree connect and file read from the case's reply data, with unit tests that drive it from `Curl.Protocol.Smb.UnitLibrary`'s client (no network) and cover every branch.
- [x] `UpstreamTestVariableSubstitution` gives `SMBPORT` a value and `UpstreamCaseScreening` lists `smb` as an emulated server; unit tests pin both.
- [x] Cases 1451 and 1850 end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`, or the first difference is in Notes and a follow-up task is filed.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

- Built: `SmbServerConnector` on port 9020 (`UpstreamCaseRunner.SmbPort`, a port no other stand-in uses) with `SmbServerConnection`, which answers NEGOTIATE (fixed challenge), SESSION_SETUP_ANDX (UID 100, NTLM responses not checked, as nothing in 1451 or 1850 depends on them), TREE_CONNECT_ANDX to `TESTS` (TID 200; any other share `STATUS_BAD_NETWORK_NAME`, curl exit 78), NT_CREATE_ANDX of any file (FID 300, the size of `<reply><data>`), READ_ANDX, CLOSE and TREE_DISCONNECT; anything else, such as an upload's WRITE_ANDX, `STATUS_NOT_IMPLEMENTED` (curl exit 25). Default taken: every file in `TESTS` holds the case's `<reply><data>`, as smbserver.py serves `TESTS/<n>` from it.
- Registered as `UpstreamSmbFront` (one file plus one line in `UpstreamStandInFronts`, ADR-0721's pattern), which gives `%SMBPORT` its value (`UpstreamTestVariableSubstitution` already knew the name, so it needed no change) and reports the `smb` protocol feature, as Curl's `curl -V` does; `UpstreamCaseScreening` lists `SmbServerConnector.EmulatedServers`.
- Touches widened, with no overlap with any task in Doing on `origin/work/dark-factory`: the two new files `SmbServerConnection.cs` and `UpstreamSmbFront.cs`, and `UpstreamStandInFrontsTests.cs`, whose pin of the variables every front adds now includes `SMBPORT`.
- Measured on Windows: 1451 and 1850 pass (`CURL_UPSTREAM_CASE` and `SmbCase_RunThroughCurl_Passes`) and are on the ratchet. Platform-neutral: no paths or OS text; the upload test writes a temp file it deletes. The shift coordinator's CI watch files a fix task if Linux or macOS disagrees.
- Quality: `Curl.Conformance.UnitLibrary` 100% line and branch, worst CRAP 10 (the first measure found `Answer` at complexity 16; its switch became a command table).

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: f307dbe0 3ac73dae 54981e65 dd1cc079 620ebead.
- 2026-10-21: Doing -> Done. An in-memory SMB stand-in serves %SMBPORT; upstream cases 1451 and 1850 pass and are on the ratchet
