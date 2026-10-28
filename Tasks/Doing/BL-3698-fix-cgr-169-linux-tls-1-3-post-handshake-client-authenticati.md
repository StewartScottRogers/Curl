---
id: BL-3698
title: Fix CGR-169: Linux: TLS 1.3 post-handshake client authentication without a client certificate fails on the server side for PORT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitTests/Cgr169Tests.cs, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests/Cgr169Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3698 — Fix CGR-169: Linux: TLS 1.3 post-handshake client authentication without a client certificate fails on the server side for PORT

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-169 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-169: Linux: TLS 1.3 post-handshake client authentication without a client certificate fails on the server side for PORT

- **Severity:** Medium
- **Area:** tls-protocols-revocation
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable (Linux only; the verifier reproduced the server-side failure, but there is no real-server end-to-end failure)
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls-protocols-revocation TPR-09`

**Reproduction**

- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- WSL: `~/tpr/srv/tsrv http <p> pfx=~/tpr/tp/srv.pfx mode=reneg` (TLS 1.3). After the request the server calls `SslStream.NegotiateClientCertificateAsync`, which sends a post-handshake CertificateRequest. Then run `curl -sS -k https://localhost:<p>/`. Verifier case: `V_pha13_nocert`. Controls that match: `L2_pha13_cert` (with `--cert`/`--key`) and TLS 1.2 renegotiation (`L1_reneg12`, `b4_reneg12*`).

Commands (run each with REF and with PORT):

```sh
curl -sS -k https://localhost:<p>/
```

**Expected (real curl)**

- Server log `[2] RENEG done clientcert=none`. curl answers with an empty Certificate and Finished. Exit 0, body `reneg`.

**Actual (Curl)**

- Server log `[2] RENEG EXC SslException: Operation failed with error - 0.` The OpenSSL-backed server does not accept PORT's reply. The client exits 0 only because this test server ignores the failure and answers anyway. A server that needs a valid PHA reply (for example per-location `SSLVerifyClient optional` over TLS 1.3) would abort.

**Suspected location in source**

- Probably the hand-built TLS 1.3 client's handling of a post-handshake CertificateRequest when no client certificate is configured (`Curl.Tls.UnitLibrary`, `Curl.Networking.UnitLibrary\HandBuiltTlsProvider.cs`). Not narrowed further.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr169Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-169 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
