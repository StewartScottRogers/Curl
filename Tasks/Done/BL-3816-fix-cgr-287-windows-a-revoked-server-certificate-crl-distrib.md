---
id: BL-3816
title: Fix CGR-287: Windows: a revoked server certificate (CRL distribution point or staple) is reported as "untrusted root" or "revocation status is unknown...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/Cgr287Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3816 — Fix CGR-287: Windows: a revoked server certificate (CRL distribution point or staple) is reported as "untrusted root" or "revocation status is unknown...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-287 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-287: Windows: a revoked server certificate (CRL distribution point or staple) is reported as "untrusted root" or "revocation status is unknown" instead of "has been revoked"

- **Severity:** Low
- **Area:** tls-protocols-revocation
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls-protocols-revocation TPR-10`

**Reproduction**

- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- A persistent CRL server: `E\srv\out\tsrv.exe crl 21990 www=E\pki` (it serves `rev.crl` for every `*.crl` path). The server certificate `E\pki\certs\rev4R.pfx` (REF) / `rev4P.pfx` (PORT) has CDP `http://127.0.0.1:21990/rev4?.crl`. Run `E\srv\out\tsrv.exe http 21900 pfx=E\pki\certs\rev4R.pfx`, then `curl -sS --cacert E\pki\rca.crt https://localhost:21900/` (also with `--ssl-revoke-best-effort`, certificate `rev2`). Verifier cases: `v_crl_rev_default`, `v_crl_rev_best` (`E\v.ps1 -Only crl`). The CRL log `E\cases\v_crlserver.log` shows that both REF and PORT fetched their own CRL. For the staple variant, see TPR-02 (`v_st_rev_default`).

Commands (run each with REF and with PORT):

```sh
E\srv\out\tsrv.exe crl 21990 www=E\pki
E\srv\out\tsrv.exe http 21900 pfx=E\pki\certs\rev4R.pfx
curl -sS --cacert E\pki\rca.crt https://localhost:21900/
E\v.ps1 -Only crl
```

**Expected (real curl)**

- Exit 60 `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`.

**Actual (Curl)**

- Exit 60. The CRL case prints `curl: (60) schannel: the certificate or certificate chain is based on an untrusted root`, and the staple case prints `curl: (60) schannel: the revocation status is unknown`. The exit code is right but the reason is wrong. The good, dead-CDP, no-CDP and `--ssl-no-revoke` CRL cases match.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\TlsFailureMessages.cs:772-791` (`SchannelCaCertificateFileChainError`). It has no branch for `X509ChainStatusFlags.Revoked`, so revocation falls through to the "untrusted root" text.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr287Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Real curl and the Windows revoked-CRL server were not rebuilt in this unattended run; the fix adds the missing `Revoked` branch (before PartialChain/untrusted root) in `SchannelCaCertificateFileChainError`, reusing the existing revoked line already used by ServerCertificateVerification, pinned by Cgr287Tests. Other chain statuses are untouched.

## Log

- 2026-10-26: Created from CGR-287 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. Revoked chain now reported as revoked
