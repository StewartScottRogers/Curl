---
id: BL-3879
title: OpenSSL build: a legacy --tls-max with --ciphers @SECLEVEL=0 connects as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitTests/HandBuiltTlsProviderTests.LegacyVersions.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3879 — OpenSSL build: a legacy --tls-max with --ciphers @SECLEVEL=0 connects as curl does

## Goal

With `--ciphers` ending at `@SECLEVEL=0`, Curl's OpenSSL build reaches a server speaking only TLS 1.0 or 1.1 under a `--tls-max 1.0` or `1.1` ceiling, as curl's OpenSSL build does, instead of refusing with `no protocols available`.

## Context

ADR-0364 (BL-1152) makes `HandBuiltTlsProvider` refuse a legacy ceiling before any hello in the OpenSSL build (`RefusesItsVersionRange`), as OpenSSL 3's default security level does. ADR-0827 (BL-3623, CGR-094) found that `@SECLEVEL=0` in `--ciphers` lifts that level for a legacy minimum (measured with curl 8.18.0 on OpenSSL 3.5.5: `curl -k --tlsv1.1 --ciphers DEFAULT@SECLEVEL=0` against `openssl s_server -WWW -tls1_1 -cipher DEFAULT@SECLEVEL=0` fetches the file) and applies it there through `LowersSecurityLevelToZero`; the legacy ceiling was left as it was. Measure `curl -sS -k --tlsv1.0 --tls-max 1.1 --ciphers DEFAULT@SECLEVEL=0` (and `--tls-max 1.0`) against `-tls1_1` and `-tls1` servers in WSL first, then make `RefusesItsVersionRange` honour the same rule if curl connects.

## Acceptance criteria

- [x] The measured commands and real curl's results are recorded in Notes.
- [x] Curl's OpenSSL build gives the same exit code, stdout and stderr for each measured command, pinned by a unit test in `Curl.Networking.UnitTests`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured 2026-10-28 in WSL with curl 8.18.0 (OpenSSL 3.5.5): `curl -sS -k --tlsv1.0 --tls-max <max> [--ciphers DEFAULT@SECLEVEL=0] https://localhost:48443/f.txt` against `openssl s_server -WWW -<srv> -cipher DEFAULT@SECLEVEL=0`:
  - `-tls1_1`, max 1.1, SECLEVEL=0: exit 0, the file on stdout, stderr empty. Without: exit 35 `curl: (35) TLS connect error: error:0A0000BF:SSL routines::no protocols available`.
  - `-tls1_1`, max 1.0, SECLEVEL=0: exit 35 `curl: (35) TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version`. Without: exit 35 `no protocols available`.
  - `-tls1`, max 1.1, SECLEVEL=0: exit 0, the file. Without: exit 35 `no protocols available`.
  - `-tls1`, max 1.0, SECLEVEL=0: exit 0, the file. Without: exit 35 `no protocols available`.
- Decision (ADR-0939): `RefusesItsVersionRange` holds only when `LowersSecurityLevelToZero(--ciphers)` does not. The four SECLEVEL=0 rows are pinned in `HandBuiltTlsProviderTests.LegacyVersions` (exit code and error text, which the console prints as `curl: (35) <text>`; stdout is the transfer body); the rows without it stay pinned by ADR-0364's existing tests.
- Added `Curl.Networking.UnitLibrary/CLAUDE.md` to touches to keep its ADR-0364 sentence true; no Doing task on origin/work/dark-factory names it.
- Pipeline `feature` run directly: a one-condition change with its plan given in Context, so no architect stage was worth its cost.
- Verified: dotnet build clean, fast tests green (46 test runs, 0 failed; Curl.Networking.UnitTests 3706 passed), Measure-CodeQuality -Library Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-10-26: Created from BL-3623.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 8f93876d 6e1f7f05 779790c0.
- 2026-10-28: Doing -> Done. OpenSSL build: --ciphers ending at @SECLEVEL=0 lifts the legacy --tls-max refusal, as curl 8.18.0 connects
