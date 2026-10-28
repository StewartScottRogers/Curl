---
id: BL-3752
title: Fix CGR-223: A TLS alert after the handshake (certificate required / unknown CA) is reported as a generic 'Failure when receiving data from the peer'
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/Cgr223Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3752 — Fix CGR-223: A TLS alert after the handshake (certificate required / unknown CA) is reported as a generic 'Failure when receiving data from the peer'

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-223 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-223: A TLS alert after the handshake (certificate required / unknown CA) is reported as a generic 'Failure when receiving data from the peer'

- **Severity:** Low
- **Area:** tls
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls TLS-11`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- `openssl s_server ... -WWW -Verify 1 -CAfile ca.crt -verify_return_error` (TLS 1.3), then run `curl -sS -k https://localhost:<p>/index.txt`. On Linux, also run `--cert cli.crt --key cli.key` against `-CAfile ca2.crt`. Cases: `V_mtls_nocert`, `V_L_mtls_nocert`, `V_L_mtls_wrongca`.

Commands (run each with REF and with PORT):

```sh
openssl s_server ... -WWW -Verify 1 -CAfile ca.crt -verify_return_error
curl -sS -k https://localhost:<p>/index.txt
```

**Expected (real curl)**

- On Windows: `curl: (56) schannel: failed to read data from server: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received (e.g. handshake failed). More detail may be available in the Windows System event log.` On Linux: `curl: (56) OpenSSL SSL_read: OpenSSL/3.5.5: error:0A00045C:SSL routines::tlsv13 alert certificate required, errno 0` (or `...0A000418...tlsv1 alert unknown ca, errno 0`).

**Actual (Curl)**

- `curl: (56) Failure when receiving data from the peer` on both platforms. The exit code matches.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\TlsFailureMessages.cs` (no alert text on the read path).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr223Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-223 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
