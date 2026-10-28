# ADR-0932 — The differential host runs the built Curl.Console against a loopback server on port 18080

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3260 (parent BL-3233, ADR-0782)
- Decided by Claude under Stewart's delegation.

## Context

ADR-0782 puts the differential runner's process, socket and file system work in
`Curl.Conformance.Differential.IntegrationTests`. That host needs a Curl binary to compare with the
reference curl, a port for `DifferentialHttpResponder` and its control port, and the tester's
`runner.py` rules for each run.

## Decision

- **Curl under test.** The project references `Curl.Console`, so its build output holds the `curl`
  app host beside the tests; `DifferentialBinaries.FindOurs` runs it. `CURL_DIFFERENTIAL_OURS`
  names another binary (a Native AOT publish, say) instead. Building it with the tests means a run
  never compares a stale binary. Without an app host it falls back to `dotnet curl.dll`.
- **Reference.** `CURL_DIFFERENTIAL_REFERENCE` names it (ADR-0782); without it the tests are
  Inconclusive.
- **Ports.** Routes on 127.0.0.1:18080 and control (`/reset`, `/log`) on 18081, as `runner.py`
  did, so a report's `Host:` lines and URLs match the tester's. The tests run only on demand on
  one machine, so a fixed port is safe.
- **Each run** follows `runner.py`: working directory `<root>/<A|B>/<name with \W as _>`, the case's
  files written first, `{U}` and `{CWD}` substituted, every `*proxy*` variable removed, `HOME`,
  `USERPROFILE`, `CURL_HOME` and `APPDATA` pointed at an empty `<root>/home`, standard input written
  and closed, a timeout killing the process tree and reported as exit `TIMEOUT`, files the run
  wrote or changed collected afterwards (relative paths in the platform's separator, as Python's
  `os.path.relpath`), and the wire log fetched from the control port after the run. Cases run one at
  a time, since the server's state and log are shared.
- **Report.** `DifferentialReportWriter` writes `report.txt` in the run's temporary root, whose path
  the test prints; the root is kept so the working directories can be inspected.

## Consequences

Case-family folders (`CliHttp/`, `CliProtocols/`, `CliTls/`) add test classes beside `Host/` that
build a `DifferentialCaseRunner` the same way. The first Curl run of a test pays .NET start-up, so
its case may be reported `### SLOW`.
