---
id: BL-3543
title: Fix CGR-014: Windows: stapled OCSP responses are ignored. A revoked staple is accepted under --ssl-revoke-best-effort, and a good staple still fails w...
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3826]
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/Cgr014Tests.cs, Curl.Networking.UnitLibrary/TlsClientRouting.cs, Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs, Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/TlsClientRoutingTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
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

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr014Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 lane 4: requeued before any code edit. The repro needs a revocation PKI with good and revoked OCSP staples served by `openssl s_server -status_file`; the report's SCR scratchpad is gone and nothing in the repository builds one, so BL-3826 builds it (`Record-StapledOcspExchange.ps1`) and this task depends on it.
- Findings for the fix: the default connection goes through `SslStream`, which exposes no stapled response (`TlsClientOptions.RequireCertificateStatus` doc; `TlsClientRouting` sends `--cert-status` to the hand-built handshake, which asks for the staple and judges it with `Curl.Tls.UnitLibrary/OcspStapleVerifier.cs`). `ServerCertificateVerification.ReadChainAnchors`/`SystemStoreChainPolicy` build an `X509ChainPolicy` with `RevocationMode.Online` and no staple, and `RevocationBestEffortTolerates` then accepts RevocationStatusUnknown. Likely fix directions: on the Schannel build, have the hand-built handshake ask for the staple (status_request) whenever revocation is checked and let a good/revoked staple decide the revocation verdict before the online check; or route through it only when a staple is present. Decide with an ADR once BL-3826 has measured real curl.- 2026-10-26 lane 3: probe (C# file-based app, outside the repo) over `Record-StapledOcspExchange.ps1`'s PKI: the certificate `SslStream`'s validation callback gets carries no `CERT_OCSP_RESPONSE_PROP_ID`, and its chain (and one rebuilt Online from it) reports RevocationStatusUnknown|OfflineRevocation for good and revoked staples alike. So the staple is reachable only through the hand-built handshake. Decision in ADR-0828: the Schannel build routes a revocation-checked `--cacert` connection (no `-k`, no `--ssl-no-revoke`) to the hand-built client (`TlsClientRouting.ReasonInBuild`), and `ServerCertificateVerification.Judge` takes the staple (`HandBuiltCertificateVerifier` passes `ServerCertificateChain.OcspResponse`): good masks an unknown/offline revocation status, revoked is exit 60 `TlsFailureMessages.SchannelChainRevoked` (exit 35 CRYPT_E_REVOKED without `--cacert`), anything else leaves the online check. The staple's issuer is looked for among the sent certificates and the chain built, since s_server sends only the leaf (first attempt failed for that reason).
- Touches widened (no task in Doing on origin/work/dark-factory names these files): `TlsClientRouting.cs` (the route), `HandBuiltCertificateVerifier.cs` (passes the staple), `TlsFailureMessages.cs` (the exit 60 revoked text lives there), `TlsClientRoutingTests.cs` (its plain `--cacert` row now adds `--ssl-no-revoke`, since on Windows a revocation-checked `--cacert` is no longer plain).
- Result, `.\Record-StapledOcspExchange.ps1` after the fix (real curl 8.21.0 Schannel as REF), `curl -sS --cacert rca.crt [flags] https://localhost:P/index.txt`, every flag set (none, `--ssl-revoke-best-effort`, `--cert-status`, both): good staple REF and PORT exit 0, stdout `stapled ocsp ok`, stderr empty; revoked staple REF and PORT exit 60, `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked` plus the same `More details here` block. Nothing written to files; request is one GET of `/index.txt` on both. Before: PORT good/default and `--cert-status` exit 60 "revocation status is unknown", revoked + best-effort exit 0.
- Unit tests: `Cgr014Tests` (Judge with good/revoked/unknown staples, the builds and flags that ignore a staple, and the routing row); the good-staple and revoked-under-best-effort tests fail without the fix. Fast tests green on the whole solution; Measure-CodeQuality for Curl.Networking.UnitLibrary: 100% line and branch, one member over complexity (`Judge`, 14), split into `RevocationCheckIncomplete` and `Refusal` (same code paths, re-tested green).
- Platform check: the new tests use in-memory certificates and `ReadChainStatus`, no paths or OS text; routing is tested through `ReasonInBuild` with the build named, so they pass on Linux and macOS.
- Known limits (ADR-0828 Consequences): the system-store path (no `--cacert`) keeps `SslStream` and so still ignores a staple; a good staple masks an intermediate's unknown status too.

## Log

- 2026-10-26: Created from CGR-014 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Waits on BL-3826: the stapled-OCSP repro setup CGR-014 needs to measure real curl
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. On Windows a good stapled OCSP response satisfies the --cacert revocation check, and a revoked one fails with exit 60 even under --ssl-revoke-best-effort, as curl does
