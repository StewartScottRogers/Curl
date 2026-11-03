---
id: BL-4653
title: Fix OciRegistry conformance: unresolvable host and refused connection write curl's own error text
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary, Curl.Extended.Protocol.OciRegistry.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4653 — Fix OciRegistry conformance: unresolvable host and refused connection write curl's own error text

## Goal

`oci://nohost.invalid/x` ends with `curl: (6) Could not resolve host: nohost.invalid` and `oci://localhost:1/x` with `curl: (7) Failed to connect to localhost:1 after 0 ms: Could not connect to server`, curl 8.22.0's text (manual, EXIT CODES 6 and 7).

## Context

Found by BL-4612: the OCI extension wraps the engine's message, writing `curl: (6) Could not connect to nohost.invalid: Could not resolve host: nohost.invalid` and `curl: (7) Could not connect to localhost: Failed to connect to localhost:1 after 0 ms: ...`. The exit codes are right; drop the extra prefix. Listed in KnownFailures.txt as CurlSurface:unresolvable-host-oci-1 and CurlSurface:connection-refused-oci-1 (OciRegistryUrlErrorCases).

## Acceptance criteria

- [x] CurlSurface:unresolvable-host-oci-1 and CurlSurface:connection-refused-oci-1 pass and their KnownFailures.txt lines are removed.
- [x] A focused unit test in Curl.Extended.Protocol.OciRegistry.UnitTests pins the message (ADR-0482).
- [x] The library stays at 100% line and branch coverage, complexity at most 10; build clean and fast tests green.

## Notes

- OciHttpClient now passes the connector's ErrorMessage through unchanged (the `ErrorMessage!` pattern AzureStorage, GCS and DnsQuery use: a failed ConnectResult always carries curl's text), so the host prints curl's own `Could not resolve host:` and `Failed to connect to ...` lines. No ADR: this is restoring curl's text, not a new decision.
- Unit test SendAsync_ConnectorFails_ReturnsTheConnectorsExitCodeAndMessageUnwrapped pins it. Conformance.Extended: 1322 passed with both KnownFailures lines removed. Measure-CodeQuality (OciRegistry): 0 failing members. Full build clean; fast tests 115 projects passed, 0 failed. Change is platform-neutral (no paths or OS text).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 7043b855 3434f389 e7091c96.
- 2026-11-02: Doing -> Done. OCI unresolvable-host and refused-connection errors print curl's own text
