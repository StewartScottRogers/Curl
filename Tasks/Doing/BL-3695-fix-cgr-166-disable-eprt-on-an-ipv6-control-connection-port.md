---
id: BL-3695
title: Fix CGR-166: --disable-eprt on an IPv6 control connection: PORT sends PORT h,h,h,h,p,p where curl still sends EPRT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/Cgr166Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3695 — Fix CGR-166: --disable-eprt on an IPv6 control connection: PORT sends PORT h,h,h,h,p,p where curl still sends EPRT

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-166 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-166: `--disable-eprt` on an IPv6 control connection: PORT sends `PORT h,h,h,h,p,p` where curl still sends EPRT

- **Severity:** Medium
- **Area:** tls-protocols-revocation
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows REF 8.22.0 and Linux)
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls-protocols-revocation TPR-06`

**Reproduction**

- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- `E\srv\out\tsrv.exe ftp 21900 tls=none`, then `curl -sS -P 127.0.0.1 --disable-eprt ftp://localhost:21900/file.txt`. `localhost` resolves to `::1` first, so the control connection is IPv6. Verifier cases: Windows `v_eprt_v6`, plus the control `v_eprt_v4` (`ftp://127.0.0.1`, matches); Linux `V_eprt_v6`.

Commands (run each with REF and with PORT):

```sh
E\srv\out\tsrv.exe ftp 21900 tls=none
curl -sS -P 127.0.0.1 --disable-eprt ftp://localhost:21900/file.txt
```

**Expected (real curl)**

- `EPRT |1|127.0.0.1|<port>|`. curl re-enables EPRT on an IPv6 control connection.

**Actual (Curl)**

- `PORT 127,0,0,1,<p1>,<p2>`. It decides from the family of the `-P` data address, not from the control connection. The transfer succeeds against this server.

**Suspected location in source**

- `Curl.Protocol.Ftp.UnitLibrary\FtpSession.cs:1446-1447`. In `if ((context.FtpUseEprt && !eprtRefused) || ipv6)`, `ipv6` is the family of the data address.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr166Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-166 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
