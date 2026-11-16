---
id: BL-5294
title: Write --libcurl's setopt calls in upstream's order
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/LibcurlSourceCode.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.TransferFiles.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.TransferOptions.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.ProtocolOptions.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.ProxyTlsAndAuthentication.cs, Curl.Cli.UnitTests/LibcurlSetoptOrderTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5294 — Write --libcurl's setopt calls in upstream's order

## Goal

The C program `--libcurl` writes lists its `curl_easy_setopt` calls in the order upstream curl 8.21.0's tool sets them, so upstream test cases 1402, 1404, 1405, 1406, 1407 and 1465 pass with the port's built `curl.exe`.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 17 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-06, beside upstream curl 8.21.0, the oracle: where the two differ on the same upstream test case, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Upstream-Test-Cases.md`, UTC-3.
- **Cases (6):** `test1402`, `test1404`, `test1405`, `test1406`, `test1407`, `test1465`, played by Surl's `CurlPortBesideUpstreamCurlOverUpstreamTestCasesTests`, Surl commit `7bf55f26a8eecda5f936db7e32f596770ee7adf5`.
- **Command line, e.g.:** `curl http://127.0.0.1:<port>/we/want/1402 --libcurl log/test1402.c -d "foo=bar" -d "baz=quux"`; `curl pop3://127.0.0.1:<port>/1407 -l -u user:secret --libcurl log/test1407.c`.
- **Upstream curl 8.21.0 (correct):** reference build, SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`: all six pass; the generated file matches the case's `<file>`.
- **Port (defect):** the transfers agree (`stdout`, `protocol`, `data`, `upload` pass); the generated file differs, first at: test1402 line 16 (`CURLOPT_USERAGENT` where `CURLOPT_POSTFIELDS` is expected); test1404 line 27 (`CURLOPT_USERAGENT` where `mime1 = curl_mime_init(curl);`); test1405 line 28 (`CURLOPT_QUOTE` where `CURLOPT_FTP_SKIP_PASV_IP`); test1406 line 21 (`CURLOPT_USERAGENT` where `CURLOPT_UPLOAD`); test1407 line 16 (`CURLOPT_USERPWD` where `CURLOPT_DIRLISTONLY`); test1465 line 16 (`CURLOPT_USERAGENT` where the binary `CURLOPT_POSTFIELDS`).
- **What decides:** curl 8.21.0 `docs/cmdline-opts/libcurl.md`; the cases' `<file>` sections (`tests/data/data1402.c` and the rest) pin the exact text, option order included.
- **Where to start:** `Curl.Cli.UnitLibrary/LibcurlSourceCode*.cs`.
- **Ratchet puzzle:** all six are already listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, yet the built `curl.exe` at `b42c319` writes a different file when Surl plays them. Find out why the port's runner does not see this (for example, the `<file>` comparison is skipped or reorders lines) and record it in Notes; if the runner itself is wrong, file a follow-up task for it.

## Acceptance criteria

- [ ] For each of upstream cases 1402, 1404, 1405, 1406, 1407 and 1465, the built `curl.exe` writes the case's `<file>` section byte for byte (after upstream's own `<stripfile>` rules), shown by a port test per case.
- [ ] Notes say why the port's own runner listed these six cases as passing while the built executable failed them.

## Notes

## Log

- 2026-11-16: Created.
