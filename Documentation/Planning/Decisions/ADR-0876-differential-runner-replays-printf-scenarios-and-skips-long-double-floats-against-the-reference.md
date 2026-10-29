# ADR-0876: The differential runner replays printf scenarios and does not run long double float conversions against the reference

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3311
- Decided by Claude under Stewart's delegation.

## Context

BL-3311 replays the tester's printf inputs (`t1.json`, `pf_in.json`: 35,983 scenarios, each a
format string and up to ten typed arguments) through Curl's `curl_msnprintf` and the reference
libcurl 8.22.0's, loaded in process as in ADR-0792. `curl_msnprintf` is variadic, and the tester
called it through Python `ctypes`. Its report held 109 differences, every one a reference call
that ended `EXC OSError`: an access violation inside libcurl on a floating-point conversion with
the `L` (`long double`) length modifier (`%Lf`, `%12.2LE`, ...). Python catches that; .NET cannot,
so the same call would end the test host.

## Decision

- `Curl.Conformance.Differential.UnitLibrary/Libcurl/Printf` reads the inputs
  (`LibcurlPrintfScenarioReader`, embedded as `Cases/LibcurlPrintf/ZeroPaddedNan.json` and
  `PrintfScenarios.json`), writes each call's `count HEX` line (`LibcurlPrintfLineWriter`), and
  compares and reports (`LibcurlPrintfComparer`, in the tester's `pf_report.txt` shape).
- The reference driver calls `curl_msnprintf` through one function pointer with ten `long` variadic
  slots after the buffer, size and format: on x64 Windows every variadic argument is one 8-byte
  slot and the callee reads a `double` from that slot's bits, so a `double` is passed as its bit
  pattern and unused slots as 0. That convention holds only on Windows, so the Integration test is
  marked `[OSCondition(OperatingSystems.Windows)]`.
- A scenario whose format has an `L` floating-point conversion (`LibcurlPrintfScenario.CrashesReference`)
  is not run against the reference build and is counted as "not run", never as a difference: real
  libcurl's answer there is a crash, which Curl is not asked to reproduce.

## Consequences

One run on 2026-10-23 against Git for Windows' `libcurl-4.dll` (8.22.0): `t1` 1 scenario, 1
identical; `pf_in` 35,982 scenarios, 35,873 identical, 0 different, 109 not run. Curl's L
conversions are still exercised on Curl's side, so an exception there would show as a crash only if
it reached the host; they are compared with nothing.
