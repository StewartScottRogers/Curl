---
id: BL-4489
title: Conformance: the curl-proto-<scheme> helper program contract (ADR-1142) for the Plugin extension
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4501, BL-4502, BL-4503, BL-4463]
touches: [Curl.Conformance.Extended.UnitLibrary/Plugin, Curl.Conformance.Extended.UnitTests/Plugin, Documentation/Conformance/Extended/Plugin.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4489 — Conformance: the curl-proto-<scheme> helper program contract (ADR-1142) for the Plugin extension

## Goal

Curl's helper-program extension is measured against its own documented contract (ADR-1142, git's gitremote-helpers
adapted) through the real curl command line with in-memory stand-in helper processes, every requirement of the
contract is in a matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specification: the Plugin protocol has no external standard; its contract is ADR-1142
(`Documentation/Planning/Decisions/ADR-1142-plugin-protocol-hands-unknown-schemes-to-curl-proto-helpers.md`,
"Decision") with the gitremote-helpers sections it cites ("INVOCATION", "Capabilities", "Commands"). Each bullet of
ADR-1142's Decision is a set of requirements: helper discovery on `PATH` (RFC 3986 scheme names, `PATHEXT` and `;`
on Windows, `:` elsewhere, first directory wins, empty and missing entries skipped), never taking a curl scheme or
another extension's, `CURL_EXTENSIONS=0`, invocation with the URL as the one argument, the `capabilities` exchange
and mandatory `*` capabilities, `get`/`put` commands and the upload, the `ok`/`error` reply, line endings and the
8192-byte line limit, and each exit code (1, 7, 8, 56) with its message. Use IDs such as `Plugin:ADR1142-Invocation-1`.

BL-4463 lets a host inject the process starter and the environment the search reads; use both, with helper files
created in a temporary directory the test deletes, so no test starts a real process or reads the machine's `PATH`.
Discovery rules that differ by platform are pinned per platform with `[OSCondition]`.

Library: `Curl.Extended.Protocol.Plugin.UnitLibrary` (`CLAUDE.md`: `PluginProtocolExtension`, `HelperProgramSearch`,
`HelperProtocolHandler`, `HelperExchange`, `HelperProtocolException`).

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs,
the matrix format and the `KnownFailures.txt` rule. Measure against the contract, never against upstream curl. The
stand-in helper implements the contract from ADR-1142 itself and references no `Curl.Extended.Protocol.*` project.
One file per case group; nothing added to `Harness/`. This task touches only its own conformance files so protocols
run in parallel: a fault in its own stand-in or cases is fixed here, but a defect in the extension library is listed
in `KnownFailures.txt` with a filed High task `Fix Plugin conformance: <requirement>` (pipeline feature; touches the
library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's
`.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Plugin.md` gives every requirement of ADR-1142's Decision a `Plugin:` ID
      mapped to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Plugin/` run curl in process against stand-in helpers and assert
      the program and argument started, the bytes written to the helper, stdout, stderr and exit code for at least:
      discovery and precedence, a `curl-proto-http` ignored, `CURL_EXTENSIONS=0` (exit 1), get and put with `-T` and
      `-d`, an unknown mandatory capability, a missing `get` or `put` capability, `error <message>` (exit 56), a bad
      reply and an early end (exit 8), CRLF lines, a 8193-byte line, a non-zero helper exit (exit 56) and a helper
      that cannot start (exit 7).
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Plugin conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration` and none starts a real process.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Plugin/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test passes on Windows, Linux and macOS, with platform-specific discovery rules pinned
      per platform.

## Notes

## Log

- 2026-11-02: Created.
