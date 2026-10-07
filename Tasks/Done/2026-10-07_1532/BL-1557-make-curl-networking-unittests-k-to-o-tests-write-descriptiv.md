---
id: BL-1557
title: Make Curl.Networking.UnitTests' K to O tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1557 — Make Curl.Networking.UnitTests' K to O tests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests`' `K*`, `L*`, `N*` and `O*` test files (`KerberosDnsSrvLookupTests` through `OpenSslVerifyResultTests`: 15 files, 112 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.K|FullyQualifiedName~Curl.Networking.L|FullyQualifiedName~Curl.Networking.N|FullyQualifiedName~Curl.Networking.O" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- 2026-10-07: All 15 files now write diagnostics: the K to O run printed 244 `END` lines (112 methods with their data rows), every one with non-zero arrange, act and assert counts; 244 passed.
- Counts before -> after (Assert. / [TestMethod / [DataRow(), all unchanged: KerberosDnsSrvLookup 3/2/0; KerberosHttpAnchorLoader 20/13/0; KerberosKdcProxyHttpsTransport 22/9/5; its HttpAnchors part 17/6/0; KerberosKdcSocketTransport 20/7/0; LocalBindException 1/1/2; LocalBindingTcpDialer 23/12/4; LocalBindLines 11/11/9; NameResolutionFailure 9/5/0; NetworkDiagnosticLog 6/5/0; OpenSslCipherSuites 26/18/60; OpenSslCommonNameRefusal 4/4/0; OpenSslGroupList 24/9/52; OpenSslSignatureAlgorithmList 4/3/12; OpenSslVerifyResult 7/7/12.
- Nothing printed depends on the operating system (choice): the anchor loader's temporary directory prints as `<temp dir>`, anchor files as `FILE:<anchor file>`, `BindFailed`'s errno and system message as `<errno>` and `<system message>`, and TLS or file-system failures as their exception types only, since their words are the platform's own.
- PHASE lines time the KDC proxy posts and handshakes, the self-signed chain builds and the KDC client's UDP-to-TCP exchange.
- No test printed a `SLOW:` line; the slowest took 221 ms (the RSA-keyed KDC proxy handshakes), so no follow-up task.
- `dotnet build Curl.Networking.UnitTests -warnaserror` clean; `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"`: 3035 passed, 28 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 112 K to O test methods in Curl.Networking.UnitTests write ARRANGE, ACT and ASSERT diagnostics; build clean, fast tests green
