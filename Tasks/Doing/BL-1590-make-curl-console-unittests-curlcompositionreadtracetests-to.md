---
id: BL-1590
title: Make Curl.Console.UnitTests' CurlCompositionReadTraceTests to CurlCompositionTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1590 — Make Curl.Console.UnitTests' CurlCompositionReadTraceTests to CurlCompositionTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (14 files, 109 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CurlCompositionReadTraceTests.cs`, `CurlCompositionRtspTests.cs`, `CurlCompositionSaslDelegationTests.cs`, `CurlCompositionSmtpNtlmTests.cs`, `CurlCompositionSmtpTests.cs`, `CurlCompositionSmtpTraceTests.cs`, `CurlCompositionSocksTraceTests.cs`, `CurlCompositionSshTests.cs`, `CurlCompositionSshTraceTests.cs`, `CurlCompositionSshVerboseTests.cs`, `CurlCompositionSslTraceTests.cs`, `CurlCompositionTests.cs`, `CurlCompositionTests.ProxyPinnedPublicKey.cs`, `CurlCompositionTests.ProxyTlsHandshake.cs`.

## Context

- Split from BL-1461 (one task per range of files, as its Notes direct); BL-1461 keeps the whole-project checks and depends on this task. Follow BL-1461's Context: what matters in this project (command line, the scripted connector's script, request bytes, stdout and stderr, exit code), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1541 and BL-1542 show the pattern in `Curl.Networking.UnitTests`.
- "These classes' filter" below is one `FullyQualifiedName~Curl.Console.<Class>.` term per class the listed files declare (a partial class's files count once), joined with `|`.

## Acceptance criteria

- [x] `dotnet test Curl.Console.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Console.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the 14 files, before -> after: `Assert.` 257 -> 257, `[TestMethod` 109 -> 109, `[DataRow(` 100 -> 100.
- The filtered detailed run: 181 tests, 178 passed and 3 skipped by OS condition. All 178 that ran print an `END` line, and none has a zero arrange, act or assert count.
- No test printed a `SLOW:` line.
- `Phase("handshake")` wraps the proxy TLS handshakes in `CurlCompositionTests.ProxyPinnedPublicKey.cs` and `.ProxyTlsHandshake.cs`. In the handshake file, the shared assertion helper now returns both results and its callers describe them through `DescribeProxyAndOrigin`; the assertions are the same.
- Nothing OS-dependent is printed: temp paths appear as file names only, and line endings are escaped or `` is stripped.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
