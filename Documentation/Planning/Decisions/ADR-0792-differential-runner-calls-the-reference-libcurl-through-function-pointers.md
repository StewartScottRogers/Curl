# ADR-0792: The differential runner calls the reference libcurl through function pointers, in process beside Curl's Libcurl

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3237
- Decided by Claude under Stewart's delegation.

## Context

The tester replayed Libcurl easy-handle cases (`e_in.json`, `h_in.json`, `h_round.json`) by
driving libcurl 8.22.0's `libcurl-4.dll` from Python `ctypes` and Curl's Libcurl from a C#
console (`LibTest easy`), comparing one text line per round. Python is not allowed here, and
BL-3236, which might have settled how the reference library is loaded, had not landed.

## Decision

- `Curl.Conformance.Differential.IntegrationTests` references `Libcurl` and runs it in
  process; it loads the reference shared library named by
  `CURL_DIFFERENTIAL_REFERENCE_LIBRARY` with `NativeLibrary.Load` and calls it through
  unmanaged function pointers (`delegate* unmanaged`, `AllowUnsafeBlocks` in that test project
  only). The write and header callbacks are `[UnmanagedCallersOnly]` methods whose user data is
  a `GCHandle`. No marshalling delegates, no reflection: BCL only.
- The variadic `curl_easy_setopt` and `curl_easy_getinfo` are called with one pointer-sized
  argument after the fixed ones, which x64 Windows and x64 System V pass as they pass a fixed
  argument. A `long` info value is read as 32 bits on Windows, 64 elsewhere.
- Both libraries' results are written with `LibcurlEasyLineWriter` in the tester's line shape and
  compared by `LibcurlEasyComparer` as `easytest.py` did, in
  `Curl.Conformance.Differential.UnitLibrary/Libcurl/Easy` (100% coverage, no native code).
- `h_in.json` (570 KB) is rebuilt by `LibcurlHeaderCases` from `hdrtest.py`'s lists rather than
  embedded; `e_in.json` and `h_round.json` are embedded as they are.
- The cases' fixed `127.0.0.1:18080` is moved to a free port pair at run time, because other
  runs on the machine may hold 18080.
- Unset `CURL_DIFFERENTIAL_REFERENCE_LIBRARY` makes the test inconclusive, as
  `CURL_DIFFERENTIAL_REFERENCE` does for the CLI cases (ADR-0782).

## Consequences

The runner works wherever the reference library is an x64 build; an Arm64 reference would need
the variadic arguments passed on the stack, which this does not do. `t1.json` is a printf input
(`LibTest printf`), not an easy-handle one, and is left with the rest of `easytest.py`'s case
list to BL-3303.
