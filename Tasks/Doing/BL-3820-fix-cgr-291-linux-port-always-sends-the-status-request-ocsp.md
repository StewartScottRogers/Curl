---
id: BL-3820
title: Fix CGR-291: Linux: PORT always sends the status_request (OCSP stapling) extension; curl sends it only with --cert-status
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ClientHelloProfileMapping.cs, Curl.Networking.UnitTests/Cgr291Tests.cs, Curl.Tls.UnitLibrary/ClientHelloProfile.cs, Curl.Tls.UnitTests/Cgr291Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3820 — Fix CGR-291: Linux: PORT always sends the status_request (OCSP stapling) extension; curl sends it only with --cert-status

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-291 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-291: Linux: PORT always sends the status_request (OCSP stapling) extension; curl sends it only with `--cert-status`

- **Severity:** Low
- **Area:** tls-protocols-revocation
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable (Linux only, REF-LINUX 8.18.0)
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls-protocols-revocation TPR-14`

**Reproduction**

- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- WSL: `openssl s_server -accept <p> -cert pki/certs/ocg11R.crt -key pki/srv.key -cert_chain pki/rca.crt -WWW -status_verbose -status_file pki/certs/ocg11R.ocsp`. The server prints `cert_status: callback called` only when the ClientHello carries status_request. Then run `curl -sS --cacert pki/rca.crt https://localhost:<p>/index.txt` (no `--cert-status`), and the same with `-k`. Verifier cases: `V_st_req_nocs`, `V_st_req_nocs_k`.

Commands (run each with REF and with PORT):

```sh
openssl s_server -accept <p> -cert pki/certs/ocg11R.crt -key pki/srv.key -cert_chain pki/rca.crt -WWW -status_verbose -status_file pki/certs/ocg11R.ocsp
curl -sS --cacert pki/rca.crt https://localhost:<p>/index.txt
```

**Expected (real curl)**

- No `cert_status: callback called` line, so no staple is requested.

**Actual (Curl)**

- The server logs `cert_status: callback called` and sends the staple. The ClientHello differs; the transfer results match.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\ClientHelloProfileMapping.cs:51-52` adds status_request only for `--cert-status`, so the measured Linux `ClientHelloProfile` (`Curl.Tls.UnitLibrary\ClientHelloProfile.cs`) must already contain it. Not checked further.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr291Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-291 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
