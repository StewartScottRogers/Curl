---
id: BL-3621
title: Fix CGR-092: --pinnedpubkey sha256// lists are split differently: a trailing ; makes a correct pin fail, and ;; makes a later pin match
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/PinnedPublicKey.cs, Curl.Networking.UnitTests/Cgr092Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3621 — Fix CGR-092: --pinnedpubkey sha256// lists are split differently: a trailing ; makes a correct pin fail, and ;; makes a later pin match

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-092 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-092: `--pinnedpubkey` sha256// lists are split differently: a trailing `;` makes a correct pin fail, and `;;` makes a later pin match

- **Severity:** Medium
- **Area:** tls
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0079
- **Source findings:** `tls TLS-06`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- `openssl s_server ... -WWW`. Set `PIN` to the contents of `E\pki\srv.pin` (`HrAhpvV9uU1IaXMMT1BRRhexAFJ1UQcOb0V8QEp4LkQ=`). Then run:
  - `curl -sS -k --pinnedpubkey "sha256//$PIN;" https://localhost:<p>/index.txt`
  - `curl -sS -k --pinnedpubkey "sha256//$PIN;x" ...`
  - `curl -sS -k --pinnedpubkey "sha256//AAAA=;;sha256//$PIN" ...`

  Cases: `V_pin_semi`, `V_pin_semi_x`, `V_pin_dsemi`, `V_L_pin_semi`, `V_L_pin_dsemi`.

Commands (run each with REF and with PORT):

```sh
openssl s_server ... -WWW
curl -sS -k --pinnedpubkey "sha256//$PIN;" https://localhost:<p>/index.txt
curl -sS -k --pinnedpubkey "sha256//$PIN;x" ...
curl -sS -k --pinnedpubkey "sha256//AAAA=;;sha256//$PIN" ...
```

**Expected (real curl)**

- `PIN;` and `PIN;x` give exit 0. `AAAA=;;sha256//PIN` gives exit 90 `SSL: public key does not match pinned public key`. Windows 8.22 and Linux 8.18 agree.

**Actual (Curl)**

- The reverse on both platforms. `PIN;` and `PIN;x` give exit 90, and `;;` gives exit 0. The normal `sha256//A;sha256//B` form matches REF.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\PinnedPublicKey.cs:123-128`, `.Split(";" + HashPrefix)`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr092Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Pipeline: a one-method fix, so the feature stages were done inline (measure, failing test, fix, verify) rather than through separate agents. No ADR: the behaviour is "match curl" under ADR-0193, measured; the new rule is in `PinnedPublicKey.MatchesAnyHash`'s comment and `Cgr092Tests`' summary.
- Measured 2026-10-26 with `Record-CurlExchange.ps1 -Tls -Response 'HTTP/1.1 200 OK\r\nContent-Length: 9\r\n\r\nhello tls' -CurlArgs '-sS','-k','--pinnedpubkey',<pin>,https://localhost:<port>/index.txt`, REF curl 8.22.0 Schannel, PORT `Curl.Console\bin\Debug\net10.0\curl.exe` with `-Curl`. `{H}` is the served key's hash. REF and PORT agree on every case (exit code, stdout, stderr, request.bin; only the Host port differs, as each run used its own port); no files are written:
  - `sha256//{H};` 0 `hello tls`; `sha256//{H};x` 0; `sha256//{H};;` 0; `sha256//AAAA=;sha256//{H}` 0 (already matched before the fix); `sha256//AAAA=;sha256//{H};` 0; `sha256//AAAA=;sha256//{H};x` 0; `sha256//;sha256//{H}` 0.
  - `sha256//AAAA=;;sha256//{H}` 90; `sha256//AAAA=;x;sha256//{H}` 90; `sha256//AAAA=; sha256//{H}` 90; `sha256//AAAA=;SHA256//{H}` 90; `sha256//AAAA=;xsha256//{H}` 90; `sha256//AAAA=sha256//{H}` 90; each with stderr `curl: (90) SSL: public key does not match pinned public key`.
  - Before the fix PORT gave 90 for `{H};`, `{H};x` and `{H};;`, and 0 for `;;`.
- The rule curl follows: read a hash to the next `;`, compare it whole; go on only while that `;` is followed directly by `sha256//`. Linux 8.18 agrees per the report; the code is platform-neutral and the tests are pure (no files, paths or OS text).
- `Cgr092Tests` (13 rows): 7 failed with the old `Split` restored, all 13 pass with the fix.
- Solution fast run: every project passed except 4 Mtls tests in `Curl.Conformance.UnitTests` failing with a file lock on `MtlsCertificates\source\test-ca.prm` (another process), unrelated to this change; they pass alone. Filed as BL-3872.
- `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-092 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. --pinnedpubkey sha256// lists split as curl 8.22.0 does: trailing ; keeps a match, ;; ends the list
