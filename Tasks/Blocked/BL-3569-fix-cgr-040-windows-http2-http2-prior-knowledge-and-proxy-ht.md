---
id: BL-3569
title: Fix CGR-040: Windows: --http2, --http2-prior-knowledge and --proxy-http2 are accepted (h2c upgrade sent) although -V lists no HTTP2; the Windows REF r...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.HttpVersionRows.cs, Curl.Cli.UnitTests/Cgr040Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3569 — Fix CGR-040: Windows: --http2, --http2-prior-knowledge and --proxy-http2 are accepted (h2c upgrade sent) although -V lists no HTTP2; the Windows REF r...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-040 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-040: Windows: `--http2`, `--http2-prior-knowledge` and `--proxy-http2` are accepted (h2c upgrade sent) although `-V` lists no HTTP2; the Windows REF refuses them with exit 2

- **Severity:** Medium
- **Area:** cli-options, upstream-cases
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0099, GF-0031, GF-0098
- **Source findings:** `cli-options CLIO-05`, `upstream-cases UC-16`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- **[cli-options CLIO-05]** --http2, --http2-prior-knowledge and --proxy-http2 are accepted although -V lists no HTTP2; REF refuses them
  - `curl --http2 -s @U`; `curl --http2-prior-knowledge -s @U`; `curl --proxy-http2 -s @U`. Scripts: `repro/r05_unsupported_win.sh`, `verify/v1_win.sh`.
- **[upstream-cases UC-16]** Windows: PORT supports HTTP/2 (h2c), which the Windows reference build lacks, so --http2 runs exit differently
  - `verify\vwin.ps1` (case `v_h2win`). The server replies `HTTP/1.1 101 Switching!\r\n\r\nHTTP/1.1 200 OK...`. Run `curl -s -S http://127.0.0.1:21374/1801 --http2`.

Commands (run each with REF and with PORT):

```sh
# [cli-options CLIO-05]
curl --http2 -s @U
curl --http2-prior-knowledge -s @U
curl --proxy-http2 -s @U
# [upstream-cases UC-16]
curl -s -S http://127.0.0.1:21374/1801 --http2
```

**Expected (real curl)**

- **[cli-options CLIO-05]** exit 2, `curl: option --http2: the installed libcurl version does not support this`, then the try-help line.
- **[upstream-cases UC-16]** Exit 2, `curl: option --http2: the installed libcurl version does not support this`.

**Actual (Curl)**

- **[cli-options CLIO-05]** `--http2` gives exit 0 and sends `Upgrade: h2c`, `HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA` and `Connection: Upgrade, HTTP2-Settings`. `--http2-prior-knowledge` gives exit 55. `--proxy-http2` is accepted silently (exit 0).
- **[upstream-cases UC-16]** Sends an h2c upgrade request and exits 55 (see UC-8).

**Notes**

- **[upstream-cases UC-16]** *Verifier note:* PORT supports more than REF-WIN here, which is arguably not a defect. It is recorded because the exit codes differ.

**Suspected location in source**

- **[cli-options CLIO-05]** `Curl.Cli.UnitLibrary/CommandLineOptionTable.HttpVersionRows.cs`.
- **[upstream-cases UC-16]** Curl.Cli feature set.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr040Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 (lane 5): Measured with `Record-CurlExchange.ps1` (loopback server answering `HTTP/1.1 200 OK`, `hello\n`; UC-16 with `HTTP/1.1 101 Switching!\r\n\r\n` first). REF = Git's curl 8.22.0 Schannel; NG = curl.se 8.18.0 nghttp2 (WinGet); PORT = Curl at 25106b327.
  - `--http2 -s URL`: REF exit 2, `curl: option --http2: the installed libcurl version does not support this` + `curl: try 'curl --help' or 'curl --manual' for more information`; NG exit 0, `hello`; PORT exit 0, `hello`, request bytes equal to NG's (Upgrade: h2c, HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA, Connection: Upgrade, HTTP2-Settings) apart from the User-Agent version.
  - `--http2-prior-knowledge -s URL`: REF exit 2, same refusal naming the option; NG exit 55, no output; PORT exit 55, no output.
  - `--proxy-http2 -s URL`: REF exit 2, refusal; NG exit 0, `hello`; PORT exit 0, `hello`, the same plain HTTP/1.1 request.
  - `-s -S --http2 URL` (UC-16): REF exit 2, refusal; NG exit 16, `curl: (16) Remote peer returned unexpected data while we expected SETTINGS frame.  Perhaps, peer does not support HTTP/2 properly.`; PORT exit 56, `curl: (56) Recv failure: Connection was aborted`. That NG difference is BL-3650's (CGR-121).
- Decision (ADR-0819): Curl keeps Stewart's ADR-0786 (the three options run on Windows as the nghttp2 build runs them); refusing them would reverse a decision Stewart made himself, which the delegation does not cover, so the criteria (match REF's exit 2) cannot be met as written. Blocked for Stewart. If he reverses ADR-0786, the fix is `.RefusedBySchannelBuild()` on the three rows (`--proxy-http2` lives outside HttpVersionRows) plus `Cgr040Tests.cs` and `--ai-help`.

## Log

- 2026-10-26: Created from CGR-040 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Blocked. Stewart: CGR-040 asks Windows to refuse --http2/--http2-prior-knowledge/--proxy-http2 (exit 2) as Schannel curl does, reversing your ADR-0786 - keep ADR-0786 (ADR-0819) and close CGR-040 as accepted, or refuse them?
