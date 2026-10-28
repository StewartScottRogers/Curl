---
id: BL-3697
title: Fix CGR-168: Linux: %{ssl_verify_result} is 0 when --crlfile rejects the certificate (curl reports 23 "certificate revoked" or 12 "CRL has expired")
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/Cgr168Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3697 — Fix CGR-168: Linux: %{ssl_verify_result} is 0 when --crlfile rejects the certificate (curl reports 23 "certificate revoked" or 12 "CRL has expired")

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-168 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-168: Linux: `%{ssl_verify_result}` is 0 when `--crlfile` rejects the certificate (curl reports 23 "certificate revoked" or 12 "CRL has expired")

- **Severity:** Medium
- **Area:** tls-protocols-revocation
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable (Linux only, REF-LINUX 8.18.0; reproduced by the verifier, and the PORT stderr shows the value it should report)
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls-protocols-revocation TPR-08`

**Reproduction**

- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- WSL: `openssl s_server -accept <p> -cert pki/certs/rev3R.crt -key pki/srv.key -cert_chain pki/rca.crt -WWW`, then `curl -sS --cacert pki/rca.crt --crlfile pki/rev.crl.pem -o out -w '%{ssl_verify_result} %{exitcode}\n' https://localhost:<p>/index.txt`. For an expired CRL: make it with `openssl ca -config ca.cnf -gencrl -crlsec 1 -out expired.crl.pem`, wait 2 s, and use a good certificate. Verifier cases: `V_crl_rev_w`, `V_crl_expired_w`.

Commands (run each with REF and with PORT):

```sh
openssl s_server -accept <p> -cert pki/certs/rev3R.crt -key pki/srv.key -cert_chain pki/rca.crt -WWW
curl -sS --cacert pki/rca.crt --crlfile pki/rev.crl.pem -o out -w '%{ssl_verify_result} %{exitcode}\n' https://localhost:<p>/index.txt
openssl ca -config ca.cnf -gencrl -crlsec 1 -out expired.crl.pem
```

**Expected (real curl)**

- stdout `23 60` (revoked) or `12 60` (expired CRL). stderr `curl: (60) SSL certificate OpenSSL verify result: certificate revoked (23)` (or `CRL has expired (12)`).

**Actual (Curl)**

- stdout `0 60` in both cases. The stderr text, including `(23)`/`(12)`, is identical to REF, so PORT contradicts itself.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\ServerCertificateVerification.cs:176-191` (`Judge`). The CRL refusal causes the failure, but the PeerVerification observation that feeds `ssl_verify_result` is taken from the chain errors only.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr168Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-168 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
