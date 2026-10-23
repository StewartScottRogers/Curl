---
id: BL-3235
title: Replay the tester's CLI TLS cases in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3233]
touches: [Curl.Conformance.Differential.UnitLibrary/Tls, Curl.Conformance.Differential.UnitTests/Tls, Curl.Conformance.Differential.UnitLibrary/Cases/CliTls, Curl.Conformance.Differential.IntegrationTests/CliTls]
requirement: none
created: 2026-10-22
completed: 2026-10-23
---
# BL-3235 — Replay the tester's CLI TLS cases in the differential runner

## Goal

The differential runner replays the tester's CLI TLS cases (`cases_tls.py`, 70 cases) on a C# port of `tls_server.py` using the tester's `pki` certificates.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. Compare with `report_tls.txt`. Certificates are platform-sensitive: keep any unit test that loads one platform-neutral or pin each platform's answer.

## Acceptance criteria

- [x] The TLS case file and server port exist, and one run's report is recorded under Notes with each new difference filed.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.
- Delivered: `Cases/CliTls/CliTlsCases.json` (all 70 `cases_tls.py` cases, `skip_wire` as `compareWire:false`, `cmp_err=False` as `compareStderr:false`); `Tls/DifferentialTlsEndpoint` and `Tls/DifferentialTlsEndpoints` (runner.py's `PORTS` table with each server's `CERT`/`MINV`/`MAXV`/`CLIENTAUTH`, and `Substitute`, which writes `{T}`...`{RSA}` as `127.0.0.1:port` and `{PKI}` with `/`, as runner.py's `sub()` did); `IntegrationTests/CliTls/DifferentialTlsLoopbackServer` (the `tls_server.py` port: SslStream over the shared `DifferentialHttpResponder` routes on 18100-18160, client certificate checked against `ca.pem` with a custom-root chain) and `CliTlsDifferentialTests`, which also starts the plain loopback server on 18080 for the `{U}` cases.
- Decisions (sensible defaults, no ADR needed): the certificates are not committed; `CURL_DIFFERENTIAL_PKI` names the tester's `pki` directory and the test is Inconclusive without it, beside `CURL_DIFFERENTIAL_REFERENCE` (committing private keys is worse, and `tls-pin-good` pins the tester's own server key). Servers without MINV/MAXV accept TLS 1.2 and 1.3, Python's default server context. Placeholders are substituted in the case before the shared `DifferentialProcessRunner` (Host/, outside this task's touches) sees it. `tls-http2-over-http1-server` and `tls-public-http2` carry `NoHttp2`: the reference build exits 2 for `--http2`.
- Run 2026-10-23 (reference `C:\Program Files\Git\ucrt64\bin\curl.exe`, pki `Z:\repos\Curl.difftest\2026-10-22\pki`): 12 DIFF before the NoHttp2 tags, 10 after. Each filed or already covered:
  - tls-wrong-ca (real 0, Curl 60 "certificate chain is incomplete") -> BL-3284.
  - tls-mtls-p12 (real 35 SEC_E_UNKNOWN_CREDENTIALS, Curl 0) -> BL-3285.
  - tls-cert-status (real 0, Curl 91 "No OCSP response received") -> BL-3286.
  - tls12-server-tls13only, tls-public-badssl-rc4, tls-public-badssl-dh480 (SEC_E_ILLEGAL_MESSAGE alert text) and tls-max-1.2-vs-1.3 (real SEC_E_UNSUPPORTED_FUNCTION) -> BL-3287.
  - tls-public-http2-multi (`%{num_connects}` second transfer real 0, Curl 1) and tls-public-compressed-br (size_download 109162 vs 108854) -> BL-3288.
  - tls-proxy-insecure-http-tunnel-fail ("Connection was reset" vs "aborted") -> already BL-3282.
  - tls-ca-missing-file, tls-public-badssl-revoked, -pinning and -idn from the tester's report_tls.txt no longer differ.
- The library's quality run: 0 failing members. Unit tests: 234 passed in Curl.Conformance.Differential.UnitTests; the new tests read only the embedded case file and in-memory records, so they are platform-neutral. `dotnet format --verify-no-changes` flags final newlines in existing `Http/Routes/Redirects/*Route.cs` files this task did not touch; left to whoever next touches them.
- BL-3284's ID may collide with the FTP port task another lane filed; the shift's dedupe renumbers it.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 79805936 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. The differential runner replays the 70 CLI TLS cases on a C# port of tls_server.py; 10 differences filed as BL-3284 to BL-3288 or covered by BL-3282
