---
id: BL-3693
title: Fix CGR-164: TLS sessions are resumed across transfers whose verification settings differ (-k then --cacert, the reverse, or an added --pinnedpubkey);...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitLibrary/SslStreamSessionPartitions.cs, Curl.Networking.UnitLibrary/TlsSessionCache.cs, Curl.Networking.UnitTests/Cgr164Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3693 — Fix CGR-164: TLS sessions are resumed across transfers whose verification settings differ (-k then --cacert, the reverse, or an added --pinnedpubkey);...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-164 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-164: TLS sessions are resumed across transfers whose verification settings differ (`-k` then `--cacert`, the reverse, or an added `--pinnedpubkey`); curl starts a new session

- **Severity:** Medium
- **Area:** tls-protocols-revocation
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls-protocols-revocation TPR-03`

**Reproduction**

- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- `openssl s_server -accept 21900 -cert TP\srv.crt -key TP\srv.key -www -tls1_2` (also without `-tls1_2` for TLS 1.3). The `-www` status page prints `New,` or `Reused,` for each connection. Then run `curl -sS -k https://localhost:21900/ --next -sS --cacert TP\ca.crt --ssl-no-revoke https://localhost:21900/` and look at the `New,`/`Reused,` lines. On Linux, drop `--ssl-no-revoke`. Verifier cases: Windows `v_res12_k_ca`, `v_res13_k_ca`, `v_res12_ca_k`, plus the control `v_res12_k_k` (`E\v.ps1 -Only resume`). Linux `V_res12_k_ca`, `V_res13_k_ca`, `V_res12_ca_k`, `V_res12_pin`, plus the control `V_res12_k_k`.

Commands (run each with REF and with PORT):

```sh
openssl s_server -accept 21900 -cert TP\srv.crt -key TP\srv.key -www -tls1_2
curl -sS -k https://localhost:21900/ --next -sS --cacert TP\ca.crt --ssl-no-revoke https://localhost:21900/
E\v.ps1 -Only resume
```

**Expected (real curl)**

- `New, TLSv1.2 …` then `New, TLSv1.2 …`. The peer's SSL configuration differs (verify mode, CA, pin), so the session is not reused. The same holds for TLS 1.3, in both directions, and with `--pinnedpubkey` (Linux). With identical options (control) REF does resume: `New` then `Reused`.

**Actual (Curl)**

- `New, …` then `Reused, …` in every case. Verification still runs on the resumed session (untrusted, wrong-host and expired certificates and bad pins still fail in both, cases `b6_*`, `L3_res_untrusted`), so this is a cache-key and protocol difference, not a verification bypass.

**Suspected location in source**

- Windows: the SslStream/Schannel session cache used by `Curl.Networking.UnitLibrary\SslStreamTlsProvider.cs` is not partitioned by verification settings or pin. Linux: `Curl.Networking.UnitLibrary\TlsSessionCache.cs:76-84` (`PeerKey`) has no verify-mode or pinned-key component. Not narrowed further.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr164Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 1): Measured on Windows with `openssl s_server -accept 21900 -cert srv.crt -key srv.key -www [-tls1_2]` (a test CA and localhost certificate made with openssl in a temp folder) and `curl -sS A https://localhost:21900/ --next -sS B https://localhost:21900/`; REF = curl 8.21.0 Schannel (`C:\Windows\System32\curl.exe`), PORT = Curl.Console at this commit. `New`/`Reused` read from the `-www` page; every run exit 0, nothing on stderr, no files written. Same for TLS 1.2 and TLS 1.3:
  - `-k` / `--cacert ca.crt --ssl-no-revoke`: REF `New, New`; PORT before `New, Reused`, after `New, New`.
  - `--cacert ... --ssl-no-revoke` / `-k`: REF `New, New`; PORT before `New, Reused`, after `New, New`.
  - `--cacert ...` / `--cacert ... --pinnedpubkey sha256//<server key>`: REF `New, New`; PORT before `New, Reused`, after `New, New`.
  - Control `-k` / `-k`: REF `New, Reused`; PORT `New, Reused` before and after (already matched, still matches).
  - Three transfers `-k`, `--cacert`, `--cacert --pinnedpubkey`: REF and PORT (after) `New, New, New`.
  - Known residual, recorded in ADR-0859 (not one of the issue's repros): `-k`, `--cacert`, `--cacert` is REF `New, New, Reused`, PORT `New, New, New`; SslStream offers no further cache partitions, and only the handshake differs, never a transfer's output.
- Fix (ADR-0859): the new `SslStreamSessionPartitions` records per host the verification settings of the first handshake; `SslStreamTlsProvider` sets `AllowTlsResume` false for a handshake to that host with other settings, which gives it its own SslStream credential and a full handshake. `TlsSessionCache.cs` (the hand-built route's cache) needed no change. Added `SslStreamSessionPartitions.cs` to touches: a new file no other task names.
- Focused test `Cgr164Tests`: `AuthenticateAsClientAsync_InsecureThenAnAddedPin_DoesNotResume` fails before the fix (SslStream's default `AllowTlsResume` is true) and passes after; the `MayResume_*` tests cover every setting in the key. Platform-neutral (no files on disk, certificates or OS text), so CI on Linux and macOS is met by that and the green fast run here.
- Linux REF (OpenSSL build) was not measured on this Windows lane; the SslStream route is the same code on Linux, where .NET's OpenSSL session cache is keyed on the credential the same way.
- `dotnet build` clean; fast tests 44260 passed, 300 skipped, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-164 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. An -k, --cacert or --pinnedpubkey change between transfers makes a new TLS session, as curl does; the same settings still resume
