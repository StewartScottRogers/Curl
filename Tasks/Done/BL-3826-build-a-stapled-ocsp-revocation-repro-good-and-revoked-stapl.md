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
completed: 2026-10-26
---
# BL-3826 — Build a stapled-OCSP revocation repro (good and revoked staples) for CGR-014

## Goal

`Record-StapledOcspExchange.ps1` builds a throwaway revocation PKI (root `rca`, a good leaf and a revoked leaf for CN localhost, an OCSP responder certificate and a DER OCSP response for each leaf), serves each leaf with Git's `openssl s_server -WWW -status_file <staple>`, and records exit code, stdout and stderr of real curl and of Curl for the CGR-014 repro commands, so BL-3543 can pin both sides without rebuilding the setup.

## Context

CGR-014 in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`: on Windows, real curl 8.22.0 (Schannel) uses the stapled OCSP response; Curl does not. The report's `SCR` scratchpad (its `E\pki` with `ocg*`/`ocr*` certificates and `certs\<tag>.ocsp` staples) is gone. Each run must use fresh certificates (one set for real curl, one for Curl) so the Windows URL cache cannot leak between them, and the leaves must carry no CRL distribution point and an AIA OCSP URL that does not answer, so only the staple can decide revocation. Use `"C:\Program Files\Git\ucrt64\bin\openssl.exe"` (`openssl ca` or `openssl x509 -req` plus `openssl ocsp -index ... -rsigner ... -respout`). PowerShell only, no Python (CLAUDE.md). Write everything under a temporary directory and delete it afterwards.

Cases to record (each against REF `curl.exe` and PORT Curl): good staple with default checking; good staple with `--ssl-revoke-best-effort`; revoked staple with default checking; revoked staple with `--ssl-revoke-best-effort`; each also with `--cert-status`. Command: `curl -sS --cacert <rca.crt> [flags] https://localhost:<port>/index.txt`.

## Acceptance criteria

- [x] `Record-StapledOcspExchange.ps1` exists at the repository root, has comment-based help, and on Windows prints one line per case and side with the exit code, stdout and stderr.
- [x] A run of it is recorded in this task's Notes, showing real curl's results for every case listed above.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3543's lane-4 run (2026-10-26): BL-3543 cannot be measured without this setup, and the run's budget did not cover building it as well as the fix.

- Run of `.\Record-StapledOcspExchange.ps1` (2026-10-26, Windows, real curl 8.21.0 Schannel as REF; PORT is the freshly built Curl). Format `staple|flags|side|exit|stdout|stderr`:
  - REF good: default, `--ssl-revoke-best-effort`, `--cert-status`, both: exit 0, stdout `stapled ocsp ok`.
  - REF revoked: all four flag sets: exit 60, `schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked` (best-effort does not rescue a revoked staple).
  - PORT good and revoked: default and `--cert-status`: exit 60 `schannel: the revocation status is unknown`; with `--ssl-revoke-best-effort` (alone or with `--cert-status`): exit 0, stdout `stapled ocsp ok`, even for the revoked staple.
  So the gap BL-3543 closes: Curl ignores the staple (good staple fails as unknown; revoked staple is let through by best-effort).
- Decision: s_server restarted per staple, fresh PKI per side; fixed 1.5 s wait for s_server to listen. Curl located as the newest `Curl.Console\bin\**\curl.exe` unless `-PortCurl` is given.
- Ticked at completion: all three criteria met (build clean, fast tests green; CI platform check is n/a, script is Windows-only).

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: ec85a772 129ecda6 779790c0.
- 2026-10-26: Doing -> Done. Record-StapledOcspExchange.ps1 records good/revoked staple repro for REF and PORT
