---
id: BL-3879
title: OpenSSL build: a legacy --tls-max with --ciphers @SECLEVEL=0 connects as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitTests/HandBuiltTlsProviderTests.LegacyVersions.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3879 — OpenSSL build: a legacy --tls-max with --ciphers @SECLEVEL=0 connects as curl does

## Goal

With `--ciphers` ending at `@SECLEVEL=0`, Curl's OpenSSL build reaches a server speaking only TLS 1.0 or 1.1 under a `--tls-max 1.0` or `1.1` ceiling, as curl's OpenSSL build does, instead of refusing with `no protocols available`.

## Context

ADR-0364 (BL-1152) makes `HandBuiltTlsProvider` refuse a legacy ceiling before any hello in the OpenSSL build (`RefusesItsVersionRange`), as OpenSSL 3's default security level does. ADR-0827 (BL-3623, CGR-094) found that `@SECLEVEL=0` in `--ciphers` lifts that level for a legacy minimum (measured with curl 8.18.0 on OpenSSL 3.5.5: `curl -k --tlsv1.1 --ciphers DEFAULT@SECLEVEL=0` against `openssl s_server -WWW -tls1_1 -cipher DEFAULT@SECLEVEL=0` fetches the file) and applies it there through `LowersSecurityLevelToZero`; the legacy ceiling was left as it was. Measure `curl -sS -k --tlsv1.0 --tls-max 1.1 --ciphers DEFAULT@SECLEVEL=0` (and `--tls-max 1.0`) against `-tls1_1` and `-tls1` servers in WSL first, then make `RefusesItsVersionRange` honour the same rule if curl connects.

## Acceptance criteria

- [ ] The measured commands and real curl's results are recorded in Notes.
- [ ] Curl's OpenSSL build gives the same exit code, stdout and stderr for each measured command, pinned by a unit test in `Curl.Networking.UnitTests`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created from BL-3623.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 8f93876d 6e1f7f05 779790c0.
