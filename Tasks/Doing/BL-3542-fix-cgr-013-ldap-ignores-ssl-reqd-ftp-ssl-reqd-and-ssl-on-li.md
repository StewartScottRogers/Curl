---
id: BL-3542
title: Fix CGR-013: LDAP ignores --ssl-reqd / --ftp-ssl-reqd (and --ssl on Linux). PORT sends the simple bind, including the password, in cleartext where cur...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Protocol.Ldap.UnitTests/Cgr013Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3542 — Fix CGR-013: LDAP ignores --ssl-reqd / --ftp-ssl-reqd (and --ssl on Linux). PORT sends the simple bind, including the password, in cleartext where cur...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-013 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-013: LDAP ignores `--ssl-reqd` / `--ftp-ssl-reqd` (and `--ssl` on Linux). PORT sends the simple bind, including the password, in cleartext where curl refuses (Windows) or does StartTLS (Linux)

- **Severity:** Critical (security: credentials sent in clear although TLS was required)
- **Area:** tls-protocols-revocation
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (verifier reproduced it on Windows and Linux; the Windows result is from REF 8.22.0)
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls-protocols-revocation TPR-01`

**Reproduction**

- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- *(Windows):* `E\srv\out\tsrv.exe ldap 21900 pfx=TP\srv.pfx tls=starttls`. This is a scripted LDAP server that logs every BER message and answers the StartTLS extended operation. Then run `curl -sS -k --ssl-reqd -u cn=admin:secret ldap://localhost:21900/dc=test`. Verifier cases: `v_ldap_reqd_u`, `v_ldap_ssl_u` (`E\v.ps1 -Only ldap`). Original cases: `b4_ldap_sslreqd`, `b5_ldap_reqd_u`, `b5_ldap_reqd_2`, `b5_ldap_ftpsslreqd`.
- *(Linux):* `~/tpr/srv/tsrv ldap <p> pfx=~/tpr/tp/srv.pfx tls=starttls`, then the same command with `/usr/bin/curl` and `~/curlnet/curl`. Verifier cases: `V_ldap_reqd_u`, `V_ldap_ssl_u` (`E\linux\lv.sh`).

Commands (run each with REF and with PORT):

```sh
E\srv\out\tsrv.exe ldap 21900 pfx=TP\srv.pfx tls=starttls
curl -sS -k --ssl-reqd -u cn=admin:secret ldap://localhost:21900/dc=test
E\v.ps1 -Only ldap
~/curlnet/curl
E\linux\lv.sh
```

**Expected (real curl)**

- On Windows (WinLDAP build) with `--ssl-reqd`: exit 4 `curl: (4) LDAP local: explicit TLS not supported`. It connects but sends nothing. On Linux (OpenLDAP build) with `--ssl-reqd` or `--ssl`, it first sends the StartTLS extended request `1.3.6.1.4.1.1466.20037` in plaintext (`77188016312E332E362E312E342E312E313436362E3230303337`), completes the TLS handshake, and then sends the bind, search and unbind inside TLS (`Cs:` lines).

**Actual (Curl)**

- On both platforms it never starts TLS. The first plaintext message is the bind `…60150201030408636E3D61646D696E8006736563726574`: a simple bind for DN `cn=admin` with password `secret`, in clear (`C:` lines). The search and unbind follow, and it exits 0 with the entry printed. The required-TLS level is not consulted at all.

**Notes**

- *Note (verifier):* On Windows with plain `--ssl` (TLS optional), REF also binds in plaintext, so that variant is not a difference on Windows. It differs only on Linux, where REF upgrades with StartTLS.

**Suspected location in source**

- `Curl.Protocol.Ldap.UnitLibrary\LdapProtocolHandler.cs:189-195` (`TargetOf`). `useTls` is set only for the `ldaps` scheme. The library has no StartTLS operation, and no UseSslFailed or "explicit TLS not supported" path.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr013Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-013 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
