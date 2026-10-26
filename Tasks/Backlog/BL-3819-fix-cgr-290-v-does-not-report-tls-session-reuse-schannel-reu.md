---
id: BL-3819
title: Fix CGR-290: -v does not report TLS session reuse (* schannel: reusing existing credential handle on Windows, * SSL reusing session with ALPN '-' on L...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests/Cgr290Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3819 — Fix CGR-290: -v does not report TLS session reuse (* schannel: reusing existing credential handle on Windows, * SSL reusing session with ALPN '-' on L...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-290 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-290: `-v` does not report TLS session reuse (`* schannel: reusing existing credential handle` on Windows, `* SSL reusing session with ALPN '-'` on Linux)

- **Severity:** Low
- **Area:** tls-protocols-revocation
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls-protocols-revocation TPR-13`

**Reproduction**

- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- `openssl s_server -accept 21900 -cert TP\srv.crt -key TP\srv.key -www -tls1_2`, then `curl -sS -k -v https://localhost:21900/ https://localhost:21900/`. Verifier cases: `v_res12_v` (Windows), `V_res12_v` (Linux).

Commands (run each with REF and with PORT):

```sh
openssl s_server -accept 21900 -cert TP\srv.crt -key TP\srv.key -www -tls1_2
curl -sS -k -v https://localhost:21900/ https://localhost:21900/
```

**Expected (real curl)**

- Windows: `* schannel: reusing existing credential handle`. Linux: `* SSL reusing session with ALPN '-'`. The server shows `Reused` for the second connection.

**Actual (Curl)**

- The server also shows `Reused`, but `-v` has no reuse line.

**Suspected location in source**

- These strings do not occur in SCR\Curl's verbose output.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr290Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-290 of the third-party gap report.
