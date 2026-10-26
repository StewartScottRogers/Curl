---
id: BL-3826
title: Build a stapled-OCSP revocation repro (good and revoked staples) for CGR-014
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-StapledOcspExchange.ps1]
requirement: none
created: 2026-10-26
completed:
---
# BL-3826 — Build a stapled-OCSP revocation repro (good and revoked staples) for CGR-014

## Goal

`Record-StapledOcspExchange.ps1` builds a throwaway revocation PKI (root `rca`, a good leaf and a revoked leaf for CN localhost, an OCSP responder certificate and a DER OCSP response for each leaf), serves each leaf with Git's `openssl s_server -WWW -status_file <staple>`, and records exit code, stdout and stderr of real curl and of Curl for the CGR-014 repro commands, so BL-3543 can pin both sides without rebuilding the setup.

## Context

CGR-014 in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`: on Windows, real curl 8.22.0 (Schannel) uses the stapled OCSP response; Curl does not. The report's `SCR` scratchpad (its `E\pki` with `ocg*`/`ocr*` certificates and `certs\<tag>.ocsp` staples) is gone. Each run must use fresh certificates (one set for real curl, one for Curl) so the Windows URL cache cannot leak between them, and the leaves must carry no CRL distribution point and an AIA OCSP URL that does not answer, so only the staple can decide revocation. Use `"C:\Program Files\Git\ucrt64\bin\openssl.exe"` (`openssl ca` or `openssl x509 -req` plus `openssl ocsp -index ... -rsigner ... -respout`). PowerShell only, no Python (CLAUDE.md). Write everything under a temporary directory and delete it afterwards.

Cases to record (each against REF `curl.exe` and PORT Curl): good staple with default checking; good staple with `--ssl-revoke-best-effort`; revoked staple with default checking; revoked staple with `--ssl-revoke-best-effort`; each also with `--cert-status`. Command: `curl -sS --cacert <rca.crt> [flags] https://localhost:<port>/index.txt`.

## Acceptance criteria

- [ ] `Record-StapledOcspExchange.ps1` exists at the repository root, has comment-based help, and on Windows prints one line per case and side with the exit code, stdout and stderr.
- [ ] A run of it is recorded in this task's Notes, showing real curl's results for every case listed above.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3543's lane-4 run (2026-10-26): BL-3543 cannot be measured without this setup, and the run's budget did not cover building it as well as the fix.

## Log

- 2026-10-26: Created.
