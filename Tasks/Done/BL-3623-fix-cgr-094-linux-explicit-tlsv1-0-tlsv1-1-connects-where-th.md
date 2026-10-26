---
id: BL-3623
title: Fix CGR-094: Linux: explicit --tlsv1.0 / --tlsv1.1 connects where the OpenSSL reference refuses at the default security level
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitTests/Cgr094Tests.cs, Curl.Networking.UnitTests/HandBuiltTlsProviderTests.LegacyVersions.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3623 — Fix CGR-094: Linux: explicit --tlsv1.0 / --tlsv1.1 connects where the OpenSSL reference refuses at the default security level

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-094 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-094: Linux: explicit `--tlsv1.0` / `--tlsv1.1` connects where the OpenSSL reference refuses at the default security level

- **Severity:** Medium
- **Area:** tls
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable. Linux only. The refusal comes from the OpenSSL 3 security level, not the curl version. On Windows these cases match.
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls TLS-08`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- WSL `openssl s_server ... -WWW -tls1_1 -cipher DEFAULT@SECLEVEL=0` (or `-tls1`), then run `curl -sS -k --tlsv1.1 https://localhost:<p>/index.txt` (or `--tlsv1.0`). Cases: `V_L_srv11_tlsv11`, `V_L_srv10_tlsv10`.

Commands (run each with REF and with PORT):

```sh
openssl s_server ... -WWW -tls1_1 -cipher DEFAULT@SECLEVEL=0
curl -sS -k --tlsv1.1 https://localhost:<p>/index.txt
```

**Expected (real curl)**

- *REF-LINUX:* Exit 35 `curl: (35) TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version`.

**Actual (Curl)**

- *PORT-LINUX:* Exit 0, `hello tls`.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\HandBuiltTlsProvider.cs` (no security-level gate).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr094Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 7): Measured in WSL with curl 8.18.0 (OpenSSL 3.5.5; no 8.22.0 OpenSSL build is on this machine, and the refusal comes from OpenSSL 3's security level, not the curl version) against `openssl s_server -accept P -cert c.pem -key k.pem -WWW -tls1_1 -cipher DEFAULT@SECLEVEL=0` (and `-tls1`), serving `index.txt` = `hello tls`. REF: `curl -sS -k --tlsv1.1 https://localhost:P/index.txt` and `--tlsv1.0` exit 35 with `curl: (35) TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version`, no stdout; the server logs a ClientHello whose legacy version is 0x0303 (supported_versions TLS 1.3 and 1.2 only). Variants: `--tlsv1.1 --tls-max 1.2` exit 35 `error:0A000102:SSL routines::unsupported protocol`; `--tlsv1.0 --tls-max 1.1` exit 35 `error:0A0000BF:SSL routines::no protocols available` (already matched); `--tlsv1.1 --ciphers DEFAULT@SECLEVEL=0` exit 0 `hello tls` from the TLS 1.1 server and the alert from the TLS 1.0 one.
- PORT before the fix: exit 0, `hello tls`. Cause: the hand-built client offered the `--tlsv1.x` minimum in the OpenSSL build, which OpenSSL 3's default security level 1 never does. Fix (ADR-0827): `HandBuiltTlsProvider.WithSecurityLevelMinimum` raises a TLS 1.0/1.1 minimum under a TLS 1.2+ ceiling to TLS 1.2 in the OpenSSL build unless the last `@SECLEVEL=` of `--ciphers` is 0. PORT after the fix (a linux-x64 publish of Curl.Console run in the same WSL against the same servers): all ten command/server combinations above give the same exit code, stdout and stderr as REF. No file is written by either; the hello now offers what curl's does (pinned by `Cgr094Tests`).
- `Cgr094Tests` (9 cases) fail 6 of 9 with the fix disabled and pass with it. Two OpenSSL rows of `HandBuiltTlsProviderTests.LegacyVersions`' legacy-minimum-alone test pinned the old (wrong) behaviour and were removed, so that file was added to `touches`, with the library's `CLAUDE.md` for one sentence on ADR-0827; no task in Doing on origin/work/dark-factory named either.
- Choice: the legacy ceiling with `@SECLEVEL=0` (`--tlsv1.0 --tls-max 1.1 --ciphers DEFAULT@SECLEVEL=0`) still refuses before any hello (ADR-0364); not part of CGR-094, filed as BL-3879.
- Build clean; fast tests green in all 46 unit test projects; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members (a first version at complexity 12 was split). The change is platform-neutral (the tests name the build explicitly), so CI on Linux and macOS is covered by the fast run here.

## Log

- 2026-10-26: Created from CGR-094 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Linux OpenSSL build raises a --tlsv1.0/--tlsv1.1 minimum to TLS 1.2 as OpenSSL 3's security level does (CGR-094)
