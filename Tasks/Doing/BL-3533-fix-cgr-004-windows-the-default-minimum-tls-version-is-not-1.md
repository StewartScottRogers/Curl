---
id: BL-3533
title: Fix CGR-004: Windows: the default minimum TLS version is not 1.2; the ClientHello offers TLS 1.1 and 1.0 and PORT silently connects to TLS 1.0/1.1-onl...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsVersionRange.cs, Curl.Networking.UnitTests/Cgr004Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3533 — Fix CGR-004: Windows: the default minimum TLS version is not 1.2; the ClientHello offers TLS 1.1 and 1.0 and PORT silently connects to TLS 1.0/1.1-onl...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-004 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-004: Windows: the default minimum TLS version is not 1.2; the ClientHello offers TLS 1.1 and 1.0 and PORT silently connects to TLS 1.0/1.1-only servers that REF refuses

- **Severity:** Critical
- **Area:** tls, proxy-connect
- **Platform(s):** Windows
- **Confidence:** Confirmed. Detail: [proxy-connect PC-18] Confirmed for the offered versions.
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls TLS-02`, `proxy-connect PC-18`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- **[tls TLS-02]** The Windows default TLS minimum is not TLS 1.2. PORT silently negotiates TLS 1.0/1.1 when no version option is given
  - `openssl s_server -accept 20803 -cert E\pki\srv.crt -key E\pki\srv.key -WWW -tls1_1 -cipher DEFAULT@SECLEVEL=0` (or `-tls1`). Then run `curl -sS -k https://localhost:20803/index.txt`, and `curl -sS --cacert E\pki\ca.crt --ssl-no-revoke https://localhost:20803/index.txt`. Cases: `V_srv11_default`, `V_srv10_default`, `V_srv11_default_ca`.
- **[proxy-connect PC-18]** The ClientHello offers TLS 1.1 and 1.0 in supported_versions; the Schannel reference offers only 1.3 and 1.2
  - run `srv 20600 log 'E'`, then `curl -sS -k --resolve ex.test:20600:127.0.0.1 https://ex.test:20600/x`, or `curl -sS --proxy-insecure -x https://127.0.0.1:20600 http://ex.test/x`. Both exit 35 once the server closes. Verifier cases: v25, v25b, v27, v27b, v28 and v28b.

Commands (run each with REF and with PORT):

```sh
# [tls TLS-02]
openssl s_server -accept 20803 -cert E\pki\srv.crt -key E\pki\srv.key -WWW -tls1_1 -cipher DEFAULT@SECLEVEL=0
curl -sS -k https://localhost:20803/index.txt
curl -sS --cacert E\pki\ca.crt --ssl-no-revoke https://localhost:20803/index.txt
# [proxy-connect PC-18]
srv 20600 log 'E'
curl -sS -k --resolve ex.test:20600:127.0.0.1 https://ex.test:20600/x
curl -sS --proxy-insecure -x https://127.0.0.1:20600 http://ex.test/x
```

**Expected (real curl)**

- **[tls TLS-02]** Exit 35 `curl: (35) schannel: next InitializeSecurityContext failed: SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported`. Its ClientHello supported_versions lists only TLS 1.3 and 1.2.
- **[proxy-connect PC-18]** `00 2b 00 05 04 03 04 03 03` (TLS 1.3, TLS 1.2).

**Actual (Curl)**

- **[tls TLS-02]** Exit 0. It prints `hello tls` over TLS 1.1 or TLS 1.0. Its ClientHello offers TLS 1.3, 1.2, 1.1 and 1.0.
- **[proxy-connect PC-18]** `00 2b 00 09 08 03 04 03 03 03 02 03 01` (TLS 1.3, 1.2, 1.1, 1.0). The rest of the extension list is identical.

**Notes**

- **[proxy-connect PC-18]** *Correction:* the tester's claim that REF also sends status_request and PORT omits it was not reproduced. Neither side sent status_request in any run, with or without -k, including the tester's own e60, n54, t14 and f15 captures.
- **[proxy-connect PC-18]** Whether a TLS 1.0/1.1 handshake would complete also depends on the Windows SChannel policy; it was not tested.

**Suspected location in source**

- **[tls TLS-02]** `Curl.Networking.UnitLibrary\TlsVersionRange.cs:40-52`. `ToSslProtocols` returns `SslProtocols.None` (the OS default) when no minimum is set. It should return Tls12|Tls13, the clamp that `HandBuiltTlsProvider` applies.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr004Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-004 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
