---
id: BL-3543
title: Fix CGR-014: Windows: stapled OCSP responses are ignored. A revoked staple is accepted under --ssl-revoke-best-effort, and a good staple still fails w...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/Cgr014Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3543 — Fix CGR-014: Windows: stapled OCSP responses are ignored. A revoked staple is accepted under --ssl-revoke-best-effort, and a good staple still fails w...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-014 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-014: Windows: stapled OCSP responses are ignored. A revoked staple is accepted under `--ssl-revoke-best-effort`, and a good staple still fails with "revocation status is unknown"

- **Severity:** Critical (security: the transfer succeeds against a server whose own staple says its certificate is revoked)
- **Area:** tls-protocols-revocation
- **Platform(s):** Windows
- **Confidence:** Confirmed. Detail: Confirmed (verifier reproduced it with new certificates ocg11/ocg12/ocr11/ocr12)
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls-protocols-revocation TPR-02`

**Reproduction**

- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- `"C:\Program Files\Git\ucrt64\bin\openssl.exe" s_server -accept 21900 -cert E\pki\certs\ocr11R.crt -key E\pki\srv.key -cert_chain E\pki\rca.crt -WWW -status_file E\pki\certs\ocr11R.ocsp`, run from a directory that contains `index.txt`. Then run `curl -sS --cacert E\pki\rca.crt --ssl-revoke-best-effort https://localhost:21900/index.txt`. For a good staple, use `ocg11R.crt` / `ocg11R.ocsp` without `--ssl-revoke-best-effort`. Each side gets its own fresh certificate (`…R` for REF, `…P` for PORT), so the Windows URL cache cannot leak between them. Verifier cases: `v_st_good_default`, `v_st_good_default_2`, `v_st_rev_best`, `v_st_rev_default` (`E\v.ps1 -Only staple`). Original cases: `b3_st_*`, `b5_st_*`.

Commands (run each with REF and with PORT):

```sh
curl -sS --cacert E\pki\rca.crt --ssl-revoke-best-effort https://localhost:21900/index.txt
E\v.ps1 -Only staple
```

**Expected (real curl)**

- Schannel uses the staple. Good staple, default checking: exit 0 with `hello rev`. Revoked staple, default or best-effort checking: exit 60 `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`.

**Actual (Curl)**

- It never uses the staple. Good staple, default checking: exit 60 `curl: (60) schannel: the revocation status is unknown`. Revoked staple with `--ssl-revoke-best-effort`: exit 0, and it prints `hello rev`. Revoked staple, default checking: exit 60 with the "revocation status is unknown" text. `--cert-status` is a no-op on both sides on Windows.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\ServerCertificateVerification.cs:108-126` (`ReadChainAnchors`). It builds its own `X509ChainPolicy` with `RevocationMode.Online` and never gives it the stapled response. Lines 386-392 (`RevocationBestEffortTolerates`) then mask the resulting RevocationStatusUnknown/OfflineRevocation.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr014Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-014 of the third-party gap report.
