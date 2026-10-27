---
id: BL-3817
title: Fix CGR-288: Windows --ftp-ssl-ccc: curl's Schannel build often clears the control channel on a later connection and completes; PORT fails every URL w...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/Cgr288Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3817 — Fix CGR-288: Windows --ftp-ssl-ccc: curl's Schannel build often clears the control channel on a later connection and completes; PORT fails every URL w...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-288 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-288: Windows `--ftp-ssl-ccc`: curl's Schannel build often clears the control channel on a later connection and completes; PORT fails every URL with exit 81

- **Severity:** Low (CCC on Windows; REF itself fails the first attempt and is nondeterministic)
- **Area:** tls-protocols-revocation
- **Platform(s):** Windows
- **Confidence:** Confirmed. Detail: Confirmed (verifier reproduced it in two cases; REF's success is not deterministic)
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls-protocols-revocation TPR-11`

**Reproduction**

- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- `E\srv\out\tsrv.exe ftp 21900 pfx=TP\srv.pfx updir=E\up`. The server accepts CCC, sends close_notify, skips the client's TLS records and continues in plaintext. Then run `curl -sS -k --ssl-reqd --ftp-ssl-ccc ftp://localhost:21900/file.txt ftp://localhost:21900/file2.txt` (or three URLs). Verifier cases: `v_ccc_two`, `v_ccc_three`. With a single URL both exit 81.

Commands (run each with REF and with PORT):

```sh
E\srv\out\tsrv.exe ftp 21900 pfx=TP\srv.pfx updir=E\up
curl -sS -k --ssl-reqd --ftp-ssl-ccc ftp://localhost:21900/file.txt ftp://localhost:21900/file2.txt
```

**Expected (real curl)**

- The first connection fails with `curl: (81) Failed to clear the command channel (CCC)`. On the next connections the server logs `CCC down: skipped 1 TLS records from client`, then plaintext `PWD`/`EPSV`/`RETR`. The overall exit is 0 and the later files are printed (`hello ftp`). The original run `b5_ccc13_two_v4` shows that REF can also fail every attempt.

**Actual (Curl)**

- The server sees a reset right after `CCC` on every connection. Every URL fails with exit 81 (`Failed to clear the command channel (CCC)` once per URL), and nothing is printed.

**Suspected location in source**

- `Curl.Protocol.Ftp.UnitLibrary\FtpSession.cs:713-745` (`ClearControlTlsAsync`). Its comment says that CCC "never can" succeed on curl's Schannel build. Also `Connection.ClearTlsAsync`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr288Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-288 of the third-party gap report.
