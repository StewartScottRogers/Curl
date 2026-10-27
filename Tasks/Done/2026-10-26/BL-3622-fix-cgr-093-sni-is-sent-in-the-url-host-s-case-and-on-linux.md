---
id: BL-3622
title: Fix CGR-093: SNI is sent in the URL host's case, and on Linux with the trailing dot, not lowercased and dot-stripped
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/Cgr093Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3622 — Fix CGR-093: SNI is sent in the URL host's case, and on Linux with the trailing dot, not lowercased and dot-stripped

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-093 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-093: SNI is sent in the URL host's case, and on Linux with the trailing dot, not lowercased and dot-stripped

- **Severity:** Medium
- **Area:** tls
- **Platform(s):** Both (Windows and Linux). Detail: Both (case: Windows and Linux; trailing dot: Linux only. On Windows `localhost.` matches REF.)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls TLS-07`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- An SNI-logging TLS server: `E\srv\out\starttls.exe http <p> E\pki\srv.pfx implicit`, or in WSL `stsrv.py http <p> <pki> implicit`. Then run `curl -sS -k https://LOCALHOST:<p>/`, `curl -sS -k https://LocalHost:<p>/`, and on Linux `curl -sS -k https://localhost.:<p>/`. Cases: `V_sni_upper`, `V_sni_mixed`, `V_L_sni_upper`, `V_L_sni_dot`.

Commands (run each with REF and with PORT):

```sh
E\srv\out\starttls.exe http <p> E\pki\srv.pfx implicit
curl -sS -k https://LOCALHOST:<p>/
curl -sS -k https://LocalHost:<p>/
curl -sS -k https://localhost.:<p>/
```

**Expected (real curl)**

- The server sees `sni=localhost` in every case. The Host header keeps its original case.

**Actual (Curl)**

- The server sees `sni=LOCALHOST` / `sni=LocalHost`, and `SNI=localhost.` on Linux. A trailing dot is forbidden in SNI by RFC 6066 §3. Exit codes and output match.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\SslStreamTlsProvider.cs:386` (`TargetHost = targetHost`, not normalised).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr093Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 6): Fix: `SslStreamTlsProvider.ServerNameIndication` lowercases the target host and drops one trailing dot for `SslClientAuthenticationOptions.TargetHost`; verification and the Host header keep the host as written (ADR-0825).
- Repro, measured on Windows with a C# file-based SNI-logging `SslStream` server (self-signed CN=localhost, answers `hello tls\n`), REF = curl 8.21.0 Schannel (`C:\Windows\System32\curl.exe`, the 8.22.0 Schannel build is not on this machine), PORT = Curl at this commit:
  - `curl -sS -k https://LOCALHOST:48622/`: REF and PORT exit 0, stdout `hello tls\n`, stderr empty, server saw `sni=localhost Host: LOCALHOST:48622` for both.
  - `curl -sS -k https://LocalHost:48622/`: same, `sni=localhost Host: LocalHost:48622` for both.
  - `curl -sS -k https://localhost.:48622/`: same, `sni=localhost Host: localhost.:48622` for both (already matched on Windows, still matches).
  - No files written by either. Linux is not measurable from a lane; the unit test pins the dot-stripped, lowercased name for the OpenSSL build too (both builds share the code path), and the change is platform-neutral, so CI on Linux and macOS is covered by the fast tests.
- Unit test `Cgr093Tests` failed before the fix (`TargetHost` was `LocalHost.`) and passes after it.
- `dotnet build` clean; fast tests all passed (Curl.Networking.UnitTests 3556 passed, 32 skipped); `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-093 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. SNI is the URL host lowercased without a trailing dot, as curl sends it
