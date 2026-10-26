---
id: BL-3625
title: Fix CGR-096: Windows: a PKCS#12 client certificate succeeds where the Schannel reference fails the handshake
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/Cgr096Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3625 — Fix CGR-096: Windows: a PKCS#12 client certificate succeeds where the Schannel reference fails the handshake

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-096 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-096: Windows: a PKCS#12 client certificate succeeds where the Schannel reference fails the handshake

- **Severity:** Medium
- **Area:** tls
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** AF-0401
- **Source findings:** `tls TLS-10`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- `openssl s_server ... -WWW -Verify 1 -CAfile E\pki\ca.crt -verify_return_error`, then run `curl -sS -k --cert E\pki\cli.p12:secret --cert-type P12 https://localhost:<p>/index.txt`, and the same with `--tls-max 1.2`. Cases: `V_p12`, `V_p12_tls12`.

Commands (run each with REF and with PORT):

```sh
openssl s_server ... -WWW -Verify 1 -CAfile E\pki\ca.crt -verify_return_error
curl -sS -k --cert E\pki\cli.p12:secret --cert-type P12 https://localhost:<p>/index.txt
```

**Expected (real curl)**

- Exit 35 `schannel: next InitializeSecurityContext failed: SEC_E_INTERNAL_ERROR (0x80090304) - The Local Security Authority cannot be contacted`. With TLS 1.2 the error is `SEC_E_ALGORITHM_MISMATCH (0x80090331) ...`.

**Actual (Curl)**

- Exit 0, `hello tls`. Against a server that distrusts the client CA, PORT gives exit 56 where REF gives 35 (original run).

**Notes**

- *Note:* REF's failure looks like a Schannel limitation with an ephemeral imported key. PORT is arguably more useful here but it is not drop-in. Deciding whether to keep this difference needs judgement.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\SslStreamTlsProvider.cs:389` (client certificate selection).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr096Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-096 of the third-party gap report.
