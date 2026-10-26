---
id: BL-3757
title: Fix CGR-228: Windows: on a certificate verification failure, PORT completes the TLS handshake and then resets, where REF aborts before finishing it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/Cgr228Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3757 — Fix CGR-228: Windows: on a certificate verification failure, PORT completes the TLS handshake and then resets, where REF aborts before finishing it

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-228 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-228: Windows: on a certificate verification failure, PORT completes the TLS handshake and then resets, where REF aborts before finishing it

- **Severity:** Low
- **Area:** tls
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls TLS-16`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- `E\srv\out\starttls.exe smtp <p> E\pki\srv.pfx implicit`, then run `curl -sS --mail-from a@b --mail-rcpt c@d -T E\mail.txt smtps://localhost:<p>` (no CA). The same happens against `starttls.exe http <p> srv.pfx implicit` with `curl -sS https://localhost:<p>/`. Cases: `V_smtps_noca`, `V_https_noca_st`; originally also an HTTPS proxy without `--proxy-insecure` (`pxs_noverify`).

Commands (run each with REF and with PORT):

```sh
E\srv\out\starttls.exe smtp <p> E\pki\srv.pfx implicit
curl -sS --mail-from a@b --mail-rcpt c@d -T E\mail.txt smtps://localhost:<p>
curl -sS https://localhost:<p>/
```

**Expected (real curl)**

- Exit 60 (SEC_E_UNTRUSTED_ROOT text). The server-side handshake never completes: `Received an unexpected EOF or 0 bytes from the transport stream`.

**Actual (Curl)**

- Exit 60 with identical stderr. The server logs `TLS Tls13 sni=localhost` (handshake completed), then `An existing connection was forcibly closed by the remote host`. No application data is sent.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\SslStreamTlsProvider.cs`. Verification is judged after `AuthenticateAsClientAsync` completes.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr228Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-228 of the third-party gap report.
