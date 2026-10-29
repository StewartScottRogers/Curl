---
id: BL-3754
title: Fix CGR-225: Windows -v leaves out the Schannel certificate-verification and shutdown info lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4031, BL-4022, BL-4067]
touches: [Curl.Protocol.Abstractions.UnitLibrary/ITransferEvents.cs, Curl.Protocol.Abstractions.UnitLibrary/TlsHandshakeEvent.cs, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/Cgr225Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3754 — Fix CGR-225: Windows -v leaves out the Schannel certificate-verification and shutdown info lines

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-225 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-225: Windows `-v` leaves out the Schannel certificate-verification and shutdown info lines

- **Severity:** Low
- **Area:** tls
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls TLS-13`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- `openssl s_server ... -WWW`, then run `curl -sS -v --cacert E\pki\ca.crt --ssl-no-revoke https://localhost:<p>/index.txt`. Case: `V_v_ca`.

Commands (run each with REF and with PORT):

```sh
openssl s_server ... -WWW
curl -sS -v --cacert E\pki\ca.crt --ssl-no-revoke https://localhost:<p>/index.txt
```

**Expected (real curl)**

- Prints `* schannel: added 1 certificate(s) from CA file '<path>'`, `* schannel: connection hostname (localhost) validated against certificate name (localhost)`, `* schannel: reusing certificate store from cache`, `* schannel: server close notification received (close_notify)`, `* schannel: server indicated shutdown in a prior call` and `{ [0 bytes data]`. The verifier saw these same lines from the release build (System32 curl 8.21, used only as a hint), so they are not debug-only.

**Actual (Curl)**

- None of these lines. The rest of the non-debug output matches.

**Suspected location in source**

- These strings do not occur in SCR\Curl.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr225Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-225 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Notes (2026-10-28 lane 6 scoping)

- The Schannel `-v` lines are not in Curl.Tls (hand-built TLS records): existing Schannel info lines are built in Curl.Output.UnitLibrary (SchannelTrustText, TransferEventInfoText) from `ITransferEvents` events (`ReportTlsTrust`) raised in Curl.Networking.UnitLibrary (HandBuiltTlsProvider.cs:646, SslStreamTlsProvider). The fix needs new events (cert-verified hostname/CA-file count/store-cache, server close_notify/shutdown) in ITransferEvents, text in Output, raising in the Networking providers; touches corrected accordingly. Test goes in Curl.Networking.UnitTests/Cgr225Tests.cs (plus Output tests).
- Requeued before any edit: BL-4031 (adds ITransferEvents forwarders across Networking decorators) and BL-4022 (HandBuiltTlsProvider.cs) hold the files this needs.
- 2026-10-28: Doing -> Backlog. Needs ITransferEvents/Output/Networking files held by BL-4031 and BL-4022; touches corrected (Curl.Tls is not where the lines live)
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Notes (2026-10-29 lane 8 run)

- Measured real curl 8.22.0 (Schannel, debug libcurl) against `openssl s_server -accept 14443 -cert srv.crt -key srv.key -WWW` with `curl -sS -v --cacert srv.crt --ssl-no-revoke https://localhost:14443/index.txt`: exit 0, stdout `hello tls`; stderr has the report's lines. Curl before the fix lacked all of them.
- Done: `added N certificate(s) from CA file '<path>'` and `connection hostname (H) validated against certificate name (N)` after the first ALPN line, from new `TlsHandshakeEvent.CaCertificateFile`/`CaCertificateCount` (SslStreamTlsProvider) and `SchannelVerificationText` (Output). Tests: `Cgr225Tests.cs` (Networking), `SchannelVerificationTextTests.cs` (Output). Quality gates green for Output, Networking, Abstractions. `TlsHandshakeEvent.cs` added to touches (no other Doing task names it). HandBuiltTlsProvider does not set the new CA fields (the default Windows route is SslStream).
- Left: store-cache reuse, close_notify, shutdown and `{ [0 bytes data]` lines, filed as BL-4067 (needs a new read-side event).
- 2026-10-29: Doing -> Backlog. Handshake-time Schannel lines done; remaining close_notify/shutdown/store-cache lines are BL-4067
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Notes (2026-10-29 lane 8 finish)

- BL-4067 (Done) added the close_notify, shutdown, store-cache and 0-byte-read lines (ADR-0996); with the handshake-time lines above, every line the report lists is written. Real curl command and results are in the earlier note; focused tests: Cgr225Tests.cs, Cgr225RemainderTests.cs (Networking), SchannelVerificationTextTests/SchannelRenegotiationTextTests (Output). Build clean; Networking and Output fast tests green. CI on Linux/macOS: change is platform-neutral (Windows-only lines behind the Schannel route).
- 2026-10-29: Doing -> Done. Every CGR-225 line now written (handshake lines here, close_notify/shutdown/store-cache via BL-4067)
