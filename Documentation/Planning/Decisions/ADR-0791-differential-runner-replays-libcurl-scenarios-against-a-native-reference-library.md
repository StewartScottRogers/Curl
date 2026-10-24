# ADR-0791 — The differential runner replays the Libcurl scenarios against a native reference library

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3236 (parent BL-3217, runner ADR-0782)
- Decided by Claude under Stewart's delegation.

## Context

The tester's `scen.json` (3,289 scenarios: URL API `U`, URL part `P`, escape `E`, unescape `N`,
date `D`, error texts `S`, version `V`) ran through Curl's Libcurl with the C# `LibTest` driver
and through libcurl 8.22.0 (`libcurl-4.dll` from Git for Windows' ucrt64) with Python ctypes
(`libtest.py`), and the two outputs were diffed line by line (`lib_diff.txt`).

## Decision

- **Core.** `Curl.Conformance.Differential.UnitLibrary/Libcurl/Scenarios` reads `scen.json`
  (`LibcurlScenarioFileReader`), replays one scenario against an `ILibcurlScenarioTarget` into
  `LibTest`'s lines (`LibcurlScenarioReplayer`) and writes the `lib_diff.txt`-shaped report
  (`LibcurlScenarioReportWriter`): counts per kind, then an `OURS` and a `REAL` line per differing
  line, keeping only the differing ` |` fields of a `U` line so a difference past the tester's
  700-character cut still shows.
- **Loading the reference.** The integration test loads the reference library from the path in
  `CURL_DIFFERENTIAL_REFERENCE_LIBCURL` with `NativeLibrary.Load` and calls its exports through
  delegates from `Marshal.GetDelegateForFunctionPointer`, not `DllImport` with a resolver: an
  assembly has only one resolver, which the other Libcurl replay tasks would contend for, and
  delegates need no unsafe code. Curl's side is the managed `Libcurl` API, as `LibTest` used it.
  Without the variable the test is Inconclusive (ADR-0782's on-demand rule); the report goes to
  `CURL_DIFFERENTIAL_LIBCURL_REPORT` or a temporary directory.
- **Data.** `scen.json` is committed as `LibcurlScenarios/LibcurlScenarios.json`, embedded in the
  integration tests project, so the run needs no file outside the repository.
- **Named exclusions.** A `U` scenario whose host is non-ASCII, or a `P` scenario that sets a
  non-ASCII host, carries `ReferenceBuildLimitation.BrokenIdn` and is not replayed (the
  reference's libidn2 punycode is wrong). `D` scenarios past 2038 or before 1901 differ only by the
  reference's 32-bit `time_t` (`ThirtyTwoBitTimeT`); they stay in the run so the report shows them,
  and are not filed. The `V` line differs by the reference build's library list and is not filed.

## Consequences

The scenarios replay in under a second. A `curl_url_get` result that Curl's managed API returns as
a string cannot carry bytes that are not UTF-8, which shows as U+FFFD differences (BL-3299).
