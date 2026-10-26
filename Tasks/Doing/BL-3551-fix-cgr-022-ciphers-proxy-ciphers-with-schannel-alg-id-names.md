---
id: BL-3551
title: Fix CGR-022: --ciphers / --proxy-ciphers with Schannel ALG_ID names (CALG_*, USE_STRONG_CRYPTO) is rejected with exit 59
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/Cgr022Tests.cs, Curl.Networking.UnitLibrary/SchannelAlgorithmList.cs, Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-26
completed:
---
# BL-3551 — Fix CGR-022: --ciphers / --proxy-ciphers with Schannel ALG_ID names (CALG_*, USE_STRONG_CRYPTO) is rejected with exit 59

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-022 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-022: `--ciphers` / `--proxy-ciphers` with Schannel ALG_ID names (CALG_*, USE_STRONG_CRYPTO) is rejected with exit 59

- **Severity:** High
- **Area:** tls
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls TLS-03`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- `openssl s_server ... -WWW -tls1_2`, then run `curl -sS -k --ciphers CALG_AES_128 https://localhost:<p>/index.txt`. The same happens with `USE_STRONG_CRYPTO` and `CALG_AES_256` (also against a TLS 1.3 server), and with `CALG_3DES`. Cases: `V_calg_aes128`, `V_calg_strong`, `V_calg_tls13`, `V_calg_3des`. The original run also covered `--proxy-ciphers` (case `pxs_ciphers`).

Commands (run each with REF and with PORT):

```sh
openssl s_server ... -WWW -tls1_2
curl -sS -k --ciphers CALG_AES_128 https://localhost:<p>/index.txt
```

**Expected (real curl)**

- Exit 0 with `hello tls`. For `CALG_3DES` it gives exit 35 `schannel: AcquireCredentialsHandle failed: SEC_E_ALGORITHM_MISMATCH (0x80090331) ...`.

**Actual (Curl)**

- Exit 59 `curl: (59) schannel: Failed setting algorithm cipher list` for every list. OpenSSL-style names give 59 in both, so `--ciphers` cannot be used at all with PORT on Windows.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\SslStreamTlsProvider.cs:695` and `HandBuiltTlsProvider.cs:722` return `TlsFailureMessages.SchannelCipherListRefused`. There is no `CALG_` table anywhere in SCR\Curl, unlike curl's `get_alg_id_by_name`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr022Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Touches widened to the new `SchannelAlgorithmList.cs` (shared by both providers), `TlsFailureMessages.cs` (the exit 35 text) and the project `CLAUDE.md` (its --ciphers sentence); no other task in Doing names them.
- Decision (ADR-0816): Schannel `--ciphers` is ALG_IDs as curl's `set_ssl_ciphers`; accepted algorithms do not narrow the offered suites (not needed by any measured case); a list caps the range at TLS 1.2. `SslStreamTlsProvider` keeps its given options object (EngineCompositionTests pins identity) and caps only the offered protocols.
- Measured (curl 8.22.0 Schannel and Curl, `-sS -k --ciphers X https://localhost:<p>/index.txt`, `openssl s_server -WWW -tls1_2`): CALG_AES_128, USE_STRONG_CRYPTO, CALG_AES_256, CALG_AES_128:CALG_SHA_256, 26128 -> both exit 0 `hello tls`; CALG_3DES -> both exit 35 `schannel: AcquireCredentialsHandle failed: SEC_E_ALGORITHM_MISMATCH (0x80090331) - The client and server cannot communicate, because they do not possess a common algorithm.`; ECDHE-RSA-AES128-GCM-SHA256 and BOGUS -> both exit 59 `schannel: Failed setting algorithm cipher list` (already matched, still matches). Against `-tls1_3`: CALG_AES_256 -> both exit 35 `schannel: next InitializeSecurityContext failed: SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported`; CALG_3DES -> both the exit 35 mismatch. `--proxy-ciphers CALG_3DES -x https://localhost:<p>` -> both the exit 35 mismatch. No files written; request bytes are the same TLS handshake path as without --ciphers.
- Cgr022Tests fails before the fix (SchannelAlgorithmList did not exist; the providers returned 59 for every list) and passes after. Measure-CodeQuality for Curl.Networking.UnitLibrary: 100/100, 0 failing members. Full fast suite green (46 projects passed). Platform-neutral: the tests call the parser directly, no OS text or paths.

## Log

- 2026-10-26: Created from CGR-022 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
