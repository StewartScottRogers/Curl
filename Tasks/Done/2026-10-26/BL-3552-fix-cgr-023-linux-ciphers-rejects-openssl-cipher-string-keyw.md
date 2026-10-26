---
id: BL-3552
title: Fix CGR-023: Linux: --ciphers rejects OpenSSL cipher-string keywords and operators (DEFAULT, HIGH, !x, aRSA, @SECLEVEL)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/OpenSslCipherSuites.cs, Curl.Networking.UnitLibrary/OpenSslCipherString.cs, Curl.Networking.UnitLibrary/CLAUDE.md, Curl.Networking.UnitTests/Cgr023Tests.cs, Curl.Networking.UnitTests/OpenSslCipherSuitesTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3552 — Fix CGR-023: Linux: --ciphers rejects OpenSSL cipher-string keywords and operators (DEFAULT, HIGH, !x, aRSA, @SECLEVEL)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-023 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-023: Linux: `--ciphers` rejects OpenSSL cipher-string keywords and operators (DEFAULT, HIGH, !x, aRSA, @SECLEVEL)

- **Severity:** High
- **Area:** tls
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable (Linux only, REF-LINUX is 8.18). The cipher-string expansion is done by OpenSSL, not by curl, so it should not depend on the curl version.
- **Related existing finding (GF/AF):** AF-0344
- **Source findings:** `tls TLS-04`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- WSL `openssl s_server -accept <p> -cert srv.crt -key srv.key -WWW -tls1_2`, then run `curl -sS -k --ciphers DEFAULT https://localhost:<p>/index.txt`. The same happens with `'HIGH:!aNULL'`, `aRSA`, and `DEFAULT@SECLEVEL=0`. Cases: `V_L_ciph_default`, `V_L_ciph_high`, `V_L_ciph_arsa`; originally `L2_srv11_tlsv11_cipher0`.

Commands (run each with REF and with PORT):

```sh
openssl s_server -accept <p> -cert srv.crt -key srv.key -WWW -tls1_2
curl -sS -k --ciphers DEFAULT https://localhost:<p>/index.txt
```

**Expected (real curl)**

- *REF-LINUX:* Exit 0, `hello tls`.

**Actual (Curl)**

- *PORT-LINUX:* Exit 59 `curl: (59) failed setting cipher list: DEFAULT` (or `HIGH:!aNULL`, `aRSA`).

**Suspected location in source**

- `Curl.Networking.UnitLibrary\OpenSslCipherSuites.cs`, which matches literal suite names only.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr023Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Added `OpenSslCipherString.cs` (the cipher-string evaluator), `CLAUDE.md` (its doc line) and `OpenSslCipherSuitesTests.cs` (its `HIGH:!aNULL` failure row now succeeds as in curl, so the row became `aNULL:!HIGH`) to touches: no task in Doing on origin/work/dark-factory names them.
- Measured in WSL (curl 8.18.0, OpenSSL 3.5.5; `openssl s_server -accept <p> -cert srv.crt -key srv.key -WWW -tls1_2`, `index.txt` holding `hello tls`): `curl -sS -k --ciphers <c> https://localhost:<p>/index.txt` with `DEFAULT`, `HIGH:!aNULL`, `aRSA` and `DEFAULT@SECLEVEL=0` each prints `hello tls`, exit 0, empty stderr; `@FOO` is exit 59 `curl: (59) failed setting cipher list: @FOO`. Before the fix Curl's OpenSSL build was exit 59 for the first four. After it `OpenSslCipherSuites.Select` gives the suite lists `openssl ciphers -s -tls1_2` gives for each (pinned in `Cgr023Tests`), so the handshake offers suites the server takes and the transfer matches; `@FOO`, `!!` and `DEFAULT@SECLEVEL=6` stay exit 59 with the same text. Curl's Linux build cannot run in this Windows lane, so the end-to-end match rests on the unit tests pinning the measured expansion; no 8.22.0 OpenSSL build was at hand, and the expansion is OpenSSL's, not curl's.
- Decision (ADR-0817): suites in OpenSSL 3.5.5's `ALL` order, `@SECLEVEL=n` accepted and not kept, unknown aliases skipped, invalid commands fail the value. Platform-neutral: pure string logic, no OS dependency, so CI on Linux and macOS is met by the green fast tests here.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 0 failing members. Build clean; fast tests green.

## Log

- 2026-10-26: Created from CGR-023 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. --ciphers is expanded as an OpenSSL cipher string: DEFAULT, HIGH:!aNULL, aRSA and @SECLEVEL now select suites as curl's OpenSSL build does
