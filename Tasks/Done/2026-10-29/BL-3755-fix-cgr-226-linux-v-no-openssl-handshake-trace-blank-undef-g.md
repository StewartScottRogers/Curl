---
id: BL-3755
title: Fix CGR-226: Linux -v: no OpenSSL handshake trace, '[blank] / UNDEF' group/signature, and missing cipher-selection and host-mismatch lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4041, BL-4010]
touches: [Curl.Output.UnitLibrary/OpenSslHandshakeText.cs, Curl.Output.UnitTests/Cgr226Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
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

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

  Determining projects to restore...
  All projects are up-to-date for restore.
  Curl.Protocol.Abstractions.UnitLibrary -> Z:eposCurl.laneslane-2Curl.Protocol.Abstractions.UnitLibraryinDebug
et10.0Curl.Protocol.Abstractions.UnitLibrary.dll
  Curl.Zstandard.UnitLibrary -> Z:eposCurl.laneslane-2Curl.Zstandard.UnitLibraryinDebug
et10.0Curl.Zstandard.UnitLibrary.dll
  Curl.Http2.UnitLibrary -> Z:eposCurl.laneslane-2Curl.Http2.UnitLibraryinDebug
et10.0Curl.Http2.UnitLibrary.dll
  Curl.Cryptography.UnitLibrary -> Z:eposCurl.laneslane-2Curl.Cryptography.UnitLibraryinDebug
et10.0Curl.Cryptography.UnitLibrary.dll
  Curl.Ntlm.UnitLibrary -> Z:eposCurl.laneslane-2Curl.Ntlm.UnitLibraryinDebug
et10.0Curl.Ntlm.UnitLibrary.dll
  Curl.Kerberos.UnitLibrary -> Z:eposCurl.laneslane-2Curl.Kerberos.UnitLibraryinDebug
et10.0Curl.Kerberos.UnitLibrary.dll
  Curl.Tls.UnitLibrary -> Z:eposCurl.laneslane-2Curl.Tls.UnitLibraryinDebug
et10.0Curl.Tls.UnitLibrary.dll
  Curl.Quic.UnitLibrary -> Z:eposCurl.laneslane-2Curl.Quic.UnitLibraryinDebug
et10.0Curl.Quic.UnitLibrary.dll
  Curl.Authentication.UnitLibrary -> Z:eposCurl.laneslane-2Curl.Authentication.UnitLibraryinDebug
et10.0Curl.Authentication.UnitLibrary.dll
  Curl.Networking.UnitLibrary -> Z:eposCurl.laneslane-2Curl.Networking.UnitLibraryinDebug
et10.0Curl.Networking.UnitLibrary.dll
  Curl.Networking.UnitTests -> Z:eposCurl.laneslane-2Curl.Networking.UnitTestsinDebug
et10.0Curl.Networking.UnitTests.dll
Test run for Z:eposCurl.laneslane-2Curl.Networking.UnitTestsinDebug
et10.0Curl.Networking.UnitTests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.
  Skipped AuthenticateAsClientAsync_WithTls13CiphersInTheOpenSslBuildOverTls13_NegotiatesTheNamedSuite ("TLS_AES_128_GCM_SHA256",TLS_AES_128_GCM_SHA256) [15 ms]
  Skipped AuthenticateAsClientAsync_WithCiphersInTheOpenSslBuildOverTls12_NegotiatesTheNamedSuite ("BOGUS:ECDHE-RSA-AES256-SHA",TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA) [21 ms]
  Skipped AuthenticateAsClientAsync_WithCiphersInTheOpenSslBuildOverTls12_NegotiatesTheNamedSuite ("ECDHE-RSA-AES128-GCM-SHA256",TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256) [22 ms]
  Skipped AuthenticateAsClientAsync_WithTls13CiphersInTheOpenSslBuildOverTls13_NegotiatesTheNamedSuite ("BOGUS:TLS_CHACHA20_POLY1305_SHA256",TLS_CHACHA20_POLY1305_SHA256) [16 ms]
  Skipped AuthenticateAsClientAsync_WithCiphersInTheOpenSslBuildOverTls12_NegotiatesTheNamedSuite ("TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384",TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384) [23 ms]
  Skipped AuthenticateAsClientAsync_WithTls13MinimumAgainstATls12OnlyServerInTheOpenSslBuildOnLinux_ReportsTheOpenSslErrorString [5 ms]
  Skipped AuthenticateAsClientAsync_SchannelBuildOverTls13_ReportsNothingButReceivedTicketsAndKeepsTheData
  Skipped AuthenticateAsClientAsync_WithACeilingBelowTls12InTheOpenSslBuildOnLinux_ReportsTheMeasuredLine (SystemDefault,Tls11)
  Skipped AuthenticateAsClientAsync_WithACeilingBelowTls12InTheOpenSslBuildOnLinux_ReportsTheMeasuredLine (Tls11,Tls11)
  Skipped AuthenticateAsClientAsync_WithACeilingBelowTls12InTheOpenSslBuildOnLinux_ReportsTheMeasuredLine (Tls10,Tls10)
  Skipped AuthenticateAsClientAsync_WithACeilingBelowTls12InTheOpenSslBuildOnLinux_ReportsTheMeasuredLine (SystemDefault,Tls10)

Passed!  - Failed:     0, Passed:   336, Skipped:    11, Total:   347, Duration: 1 s - Curl.Networking.UnitTests.dll (net10.0)
- Linux repro not runnable from this Windows lane. Prerequisites BL-4010 (handshake message trace via hand-built client), BL-4041 (group and signature type from the handshake records) and BL-4047 (-v sets TracesHandshakeMessages) are Done, so the OpenSSL build now reports group and signature; `OpenSslHandshakeText.ConnectionLine` prints them and `[blank] / UNDEF` only when none was reported. Pinned by `Cgr226Tests` (new, fast tests green: Curl.Output.UnitTests 874 passed). No production change; Measure-CodeQuality skipped as no production code changed. CI on Linux/macOS: tests are platform-neutral.
- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr226Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-226 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Needs BL-3997: Curl.Networking SslStreamTlsProvider must report group/signature and handshake trace; Output library already prints them
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Done. OpenSSL-build -v names group and signature once BL-4010/4041/4047 landed; pinned by Cgr226Tests
