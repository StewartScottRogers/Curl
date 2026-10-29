---
id: BL-3755
title: Fix CGR-226: Linux -v: no OpenSSL handshake trace, '[blank] / UNDEF' group/signature, and missing cipher-selection and host-mismatch lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3997, BL-4010]
touches: [Curl.Output.UnitLibrary/OpenSslHandshakeText.cs, Curl.Output.UnitTests/Cgr226Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3755 — Fix CGR-226: Linux -v: no OpenSSL handshake trace, '[blank] / UNDEF' group/signature, and missing cipher-selection and host-mismatch lines

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-226 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-226: Linux `-v`: no OpenSSL handshake trace, '[blank] / UNDEF' group/signature, and missing cipher-selection and host-mismatch lines

- **Severity:** Low
- **Area:** tls
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable (Linux only)
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls TLS-14`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- WSL `openssl s_server ... -WWW`, then run `curl -sS -v --cacert ca.crt https://localhost:<p>/index.txt`. Case: `V_L_v_ca`; originally also `L2_v_max12`, `L2_v_tls13cipher` and `L_v_wrong`.

Commands (run each with REF and with PORT):

```sh
openssl s_server ... -WWW
curl -sS -v --cacert ca.crt https://localhost:<p>/index.txt
```

**Expected (real curl)**

- *REF-LINUX:* `* TLSv1.3 (OUT), TLS handshake, Client hello (1):` ... with `} [n bytes data]`, and `* SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519MLKEM768 / RSASSA-PSS`.

**Actual (Curl)**

- *PORT-LINUX:* No handshake-message lines, and `* SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / [blank] / UNDEF`. Version-dependent lines (`CApath:`, `OpenSSL verify result`) were ignored.

**Suspected location in source**

- `Curl.Output.UnitLibrary\OpenSslHandshakeText.cs:135`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr226Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-226 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Needs BL-3997: Curl.Networking SslStreamTlsProvider must report group/signature and handshake trace; Output library already prints them
