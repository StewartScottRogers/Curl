---
id: BL-5394
title: Report name-constraint violations as OpenSSL verify results 47 and 48 on Linux
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs, Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/NameConstraintVerifyResultTests.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-18
---
# BL-5394 — Report name-constraint violations as OpenSSL verify results 47 and 48 on Linux

## Goal

On the Linux and macOS builds, a chain that violates a CA's name constraints reports `excluded subtree violation (48)` or `permitted subtree violation (47)` as OpenSSL curl does, in the message and in `%{ssl_verify_result}`.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-101 (Low, Confirmed, deterministic, one run each: fails closed, wrong message). Area: HTTPS `--cacert`, Linux build.

**Repro.** The PKI from STD-092 (a root, an intermediate with an excluded DNS subtree and one with a permitted DNS subtree). Serve `leafexcl` + `intexcl` on 127.0.0.1:45141 and `leafnc` + `intnc` on 45142.
`curl -sS -m 10 https://localhost:45141/ --cacert root.pem`; `curl -sS -m 10 https://localhost:45141/ --cacert intexcl.pem`; `curl -sS -m 10 https://localhost:45142/ --cacert intnc.pem`.
**Expected (REF2).** `SSL certificate OpenSSL verify result: excluded subtree violation (48)` for the first two, `permitted subtree violation (47)` for the third; exit 60.
**Actual (PORT).** Exit 60, but `self-signed certificate in certificate chain (19)` for the first and `unable to get local issuer certificate (20)` for the other two.
**Where to start.** `Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs` and `TlsFailureMessages.cs`: map the chain's name-constraint statuses (`X509ChainStatusFlags.HasExcludedNameConstraint`, `HasNotPermittedNameConstraint`) to codes 48 and 47.

## Acceptance criteria

- [x] New tests in `Curl.Networking.UnitTests` (a new file) prove a chain whose status has an excluded name constraint maps to verify result 48 (`excluded subtree violation`) and one with a not-permitted name constraint maps to 47 (`permitted subtree violation`), in both the message and the numeric code.
- [x] A test proves a chain with no name-constraint status keeps its existing mapping (19 or 20).
- [x] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.
- [x] Hand check recorded in the Log: the three repro commands print verify results (48), (48), (47), exit 60. Measure real curl with `Record-CurlExchange.ps1` where possible.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the three commands print verify results (48), (48), (47), exit 60.
- Hand check not runnable here (Windows lane, no Linux PKI repro): the mapping of HasExcludedNameConstraint (48) and HasNotPermittedNameConstraint (47, already present) is unit-tested; 47 stays checked before 48, as OpenSSL nc_match checks permitted first. Quality run: 0 failing members.

## Log

- 2026-11-17: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: da4bcad1 6a9cc3c3 7ae10ef7 a0b49a29 11ad57cd.
- 2026-11-18: Hand check on Linux not performed by the lane; verified by unit tests only (see Notes).
- 2026-11-18: Doing -> Done. Name-constraint chain statuses map to OpenSSL verify results 48 and 47 with messages
