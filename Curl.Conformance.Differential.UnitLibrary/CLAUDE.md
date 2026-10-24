# Curl.Conformance.Differential.UnitLibrary

The core of the differential conformance runner (ADR-0782): it runs one command line through
Curl and through a reference curl 8.22.0 binary and reports every difference in the tester's
`report.txt` shape. Namespace `Curl.Conformance.Differential`.

This library holds only what needs no process, socket or file system, so it stays at 100%
coverage; starting the binaries, the loopback servers and the working directories live in
`Curl.Conformance.Differential.IntegrationTests` (BL-3232 and the replay tasks after it).

## Shape

- `DifferentialCaseFileReader` reads a JSON case file into `DifferentialCase`s with
  `JsonDocument` (never a reflection serialiser: AOT), splitting each `command` with
  `CommandLineSplitter` (Python `shlex.split` rules).
- `DifferentialRunResult` is what one binary did; `DifferentialOutputNormaliser` rewrites its
  outputs as the tester's `norm()` did and drops the reference debug build's
  `sendrecv_dl()` / `Curl_sendrecv()` lines.
- `DifferentialComparer` turns two normalised runs into a `DifferentialCaseOutcome`;
  `DifferentialReportWriter` writes outcomes as `### DIFF` and `### SLOW` entries, with
  outputs as Python `repr()` literals (`PythonLiteral`).
- `ReferenceBuildLimitation` names a known fault of the reference build; a case tagged with
  one is not run against it (`DifferentialCase.RunsAgainstReferenceBuild`).
- `Api/` (ADR-0790): `ApiDifferentialCases.All`, each an `ApiDifferentialCase` pairing an
  `ApiDifferentialRequest` (a `Curl.Api` request described without referencing `Curl.Api`) with
  its equivalent curl command line; `ApiDifferentialOutput` writes the `Curl.Api` side's body and
  response code as that command line's `-w '\n%{response_code}'` does.
