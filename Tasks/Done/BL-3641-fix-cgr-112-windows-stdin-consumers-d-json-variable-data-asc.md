---
id: BL-3641
title: Fix CGR-112: Windows: @- stdin consumers (-d, --json, --variable, --data-ascii, -H, -w) and telnet stdin are read in binary; REF reads them in text mo...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/DiskDataFileReader.cs, Curl.Cli.UnitLibrary/VariableDefinition.cs, Curl.Cli.UnitLibrary/WindowsTextModeInput.cs, Curl.Cli.UnitTests/Cgr112Tests.cs, Curl.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs, Curl.Protocol.Telnet.UnitTests/Cgr112Tests.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitLibrary/WindowsTextModeReadStream.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs, Curl.Engine.UnitTests/Cgr112Tests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolDispatch.cs, Curl.Engine.UnitTests/TransferContextFactoryTests.cs, Curl.Console.UnitTests/CurlCommandRunnerTests.cs, Curl.Engine.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3641 — Fix CGR-112: Windows: @- stdin consumers (-d, --json, --variable, --data-ascii, -H, -w) and telnet stdin are read in binary; REF reads them in text mo...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-112 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-112: Windows: `@-` stdin consumers (`-d`, `--json`, `--variable`, `--data-ascii`, `-H`, `-w`) and telnet stdin are read in binary; REF reads them in text mode (CRLF becomes LF, Ctrl-Z ends input)

- **Severity:** Medium
- **Area:** environment, cli-options, other-protocols, upstream-cases
- **Platform(s):** Windows
- **Confidence:** Confirmed. Detail: [environment ENV-05] Confirmed. `C:\Windows\System32\curl.exe` 8.21 (another upstream build, used only as a cross-check) also stops `-d @-` at 0x1A and sends 9 bytes for `--json`, so this is upstream Windows behaviour.; [other-protocols OP-9] Confirmed (verifier re-ran this on Windows).
- **Related existing finding (GF/AF):** none
- **Source findings:** `environment ENV-05`, `cli-options CLIO-10`, `other-protocols OP-9`, `upstream-cases UC-10`

**Reproduction**

- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- **[environment ENV-05]** On Windows, REF reads stdin in text mode for `--json @-`, `--variable @-`, `-d @-`, `--data-ascii @-` and `-H @-`; the port reads it as binary
  - *(each line gives the stdin bytes, then the command):* 
    - `{\r\n"a":1\r\n}` to `curl -s --json @- http://127.0.0.1:21200/echo`
    - `ab\x1Acd\r\nef` to `curl -s -d @- ...`
    - `ab\x1Acd\r\nef` to `curl -s --variable v@- --expand-data "{{v}}" ...`
    - `X-A: 1\r\nX-B: 2\x1A\r\nX-C: 3` to `curl -s -H @- ...`
    - Verifier cases: v-json, v-d-ctrlz, v-var-ctrlz, v-H-ctrlz, and the control case v-dbin-ctrlz.
- **[cli-options CLIO-10]** Windows: -d @-, --json @- and -w @- read stdin in binary; REF uses text mode (CRLF becomes LF, Ctrl-Z ends input)
  - `ctrlz.bin` is `ab\x1acd\r\nef\n`. Run `curl -s --json @- @U < ctrlz.bin`; `curl -s -d @- @U < ctrlz.bin`; `curl -s -o NUL -w @- @U < ctrlz.bin`. Scripts: `repro/r11_textmode_win.sh`, `verify/v1_win.sh`.
- **[other-protocols OP-9]** TELNET reading stdin on Windows: curl reads stdin in text mode (CRLF to LF, Ctrl-Z ends input); PORT sends stdin raw
  - `EV\confirm.cases` C19 (also `EV\tel2.cases` t40).
    - Stdin is `EV\data\crlf_stdin.txt`, which holds the bytes `a\r\nb\r\n\x1a\r\nc\n`.
    - The server is in mode `telnet` with no negotiation.
    - Command: `curl -s -S telnet://127.0.0.1:21001 < crlf_stdin.txt`
- **[upstream-cases UC-10]** Windows telnet: REF strips CR from CRLF in the stdin data it sends; PORT sends the bytes unchanged
  - `verify\vwin.ps1` (case `v_telnet`). Record-CurlExchange on 127.0.0.1:21373 with StandardInput `GET /x HTTP/1.0\r\n\r\n`. Run `curl -s -S telnet://127.0.0.1:21373`.

Commands (run each with REF and with PORT):

```sh
# [environment ENV-05]
curl -s --json @- http://127.0.0.1:21200/echo
curl -s -d @- ...
curl -s --variable v@- --expand-data "{{v}}" ...
curl -s -H @- ...
# [cli-options CLIO-10]
curl -s --json @- @U < ctrlz.bin
curl -s -d @- @U < ctrlz.bin
curl -s -o NUL -w @- @U < ctrlz.bin
# [other-protocols OP-9]
curl -s -S telnet://127.0.0.1:21001 < crlf_stdin.txt
# [upstream-cases UC-10]
curl -s -S telnet://127.0.0.1:21373
```

**Expected (real curl)**

- **[environment ENV-05]** 
  - `--json` sends `{\n"a":1\n}` (Content-Length 9).
  - `-d` and `--variable` send `ab` (Content-Length 2).
  - `-H` sends X-A and X-B and drops X-C.
- **[cli-options CLIO-10]** input stops at 0x1A. `--json` sends `ab` (Content-Length 2), `-d` sends `ab` and `-w` prints `ab`. `--data-binary @-` is binary in both and matches.
- **[other-protocols OP-9]** wire `61 0A 62 0A`: CR is removed and input stops at Ctrl-Z. With `-T -` or `-T file`, REF sends the raw bytes.
- **[upstream-cases UC-10]** Wire bytes `GET /x HTTP/1.0\n\n`.

**Actual (Curl)**

- **[environment ENV-05]** 
  - `--json` sends `{\r\n"a":1\r\n}` (11).
  - `-d` sends `ab\x1Acdef` (7).
  - `--variable` sends `ab\x1Acd\r\nef` (9).
  - `-H` also sends X-C.
- **[cli-options CLIO-10]** `--json` sends 10 bytes `ab\x1acd\r\nef\n`, `-d` sends 7 bytes `ab\x1acdef`, and `-w` prints `ab\x1acdef`.
- **[other-protocols OP-9]** wire `61 0D 0A 62 0D 0A 1A 0D 0A 63 0A`.
- **[upstream-cases UC-10]** Wire bytes `GET /x HTTP/1.0\r\n\r\n`. Both exit 0.

**Notes**

- **[environment ENV-05]** *What text mode means:* CRLF becomes LF, and a Ctrl-Z byte (0x1A) ends the input.
- **[environment ENV-05]** *Where they agree:* `--data-binary @-` sends the same bytes in REF and PORT.
- **[upstream-cases UC-10]** *Verifier note:* 
  - The test1714 sub-claim (REF-WIN sends nothing and exits 0, PORT exits 63) was not re-run.
  - REF's CR stripping is probably a text-mode stdin artefact of the mingw/ucrt build.

**Suspected location in source**

- **[environment ENV-05]** `Curl.Cli.UnitLibrary\CommandLineOptionTable.cs` `ReadAtFile` (~1051) reads stdin raw for every `@-` consumer.
- **[cli-options CLIO-10]** `Curl.Cli.UnitLibrary/DiskDataFileReader.cs:100` (stdin always read as a binary stream).
- **[other-protocols OP-9]** `Curl.Protocol.Telnet.UnitLibrary\TelnetProtocolHandler.cs`. The default stdin input is used with no text-mode translation.
- **[upstream-cases UC-10]** `Curl.Protocol.Telnet.UnitLibrary` (stdin read loop).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr112Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 7): Measured real curl 8.22.0 (Git for Windows ucrt64, the reference `Record-CurlExchange.ps1` picks) and Curl (`Curl.Console\bin\Debug\net10.0\curl.exe`) with one PowerShell driver over `Record-CurlExchange.ps1` (`-StandardInput`, echo-less HTTP 200 server; telnet against the same listener). Before the fix Curl sent the raw bytes (as the report says). After it, each case gives the same exit code (0), stdout, stderr and request bytes (body and the headers before it; only the port differs) as curl:
  - `-s --json @-` with `{\r\n"a":1\r\n}` -> `{\n"a":1\n}`, Content-Length 9.
  - `-s -d @-` and `-s --data-ascii @-` with `ab\x1Acd\r\nef` -> `ab`.
  - `-s --variable v@- --expand-data {{v}}` with `ab\x1Acd\r\nef` -> `ab`; with `a\r\nb\rc` -> `a\nb\rc` (a lone CR stays).
  - `-s -H @-` with `X-A: 1\r\nX-B: 2\x1A\r\nX-C: 3` -> X-A and X-B only.
  - `-s -o NUL -w @-` with `ab\x1acd\r\nef\n` -> stdout `ab`; `-s --json @-` with the same -> `ab` (Content-Length 2).
  - Already matching, still matching: `--data-binary @-` (every byte) and `--data-urlencode @-` (`ab%1Acd%0D%0Aef`, binary in curl too).
  - `-s -S telnet://` with `a\r\nb\r\n\x1a\r\nc\n` -> wire `a\nb\n`; with `GET /x HTTP/1.0\r\n\r\n` -> `GET /x HTTP/1.0\n\n`; `-T - telnet://` sends every byte in both.
- Decision (ADR-0844): text mode is applied to standard input only, in `Curl.Cli` (`WindowsTextModeInput`, through `ReadAtFile`'s `readsStandardInputAsText` and `VariableDefinition`) and, for telnet, in `Curl.Engine`'s `TransferContextFactory` (a `WindowsTextModeReadStream` over standard input when there is no `-T`, on by default on Windows). The handler cannot tell `-T -` from default standard input, so the fix landed in the factory, not `TelnetProtocolHandler.cs`; `Curl.Telnet` files were left unchanged. `CurlCommandRunner.cs` (held by BL-3900) was avoided by taking the platform default inside the factory.
- Touches widened: `VariableDefinition.cs` (`--variable @-` reads stdin there), the new `WindowsTextModeInput.cs`, the Engine factory, stream and `ManagedEasyTransfer.Context.cs` (library handle has `Stream.Null`, setting off), and three existing tests that pinned the raw stream by reference (`ManagedEasyTransferTests.ToolDispatch.cs`, `TransferContextFactoryTests.cs`, `CurlCommandRunnerTests.cs`). No other task in Doing on `origin/work/dark-factory` named any of them.
- Unit tests: `Curl.Cli.UnitTests/Cgr112Tests.cs` (11) and `Curl.Engine.UnitTests/Cgr112Tests.cs` (10); the Windows ones fail before the fix (stdin raw). Platform-neutral: the Cli tests pass `isWindows` explicitly and the Engine tests set `ReadsTelnetStandardInputInTextMode`; the Console test expects text mode only when `OperatingSystem.IsWindows()`. CI on Linux and macOS is covered by that and the fast tests green here.
- `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary,Curl.Engine.UnitLibrary`: Engine 100/100, no failing member; Cli's `WindowsTextModeInput.Contents` was complexity 12, split, and the rerun on Cli is 100/100 with no failing member. `dotnet build` clean; `dotnet test --filter "TestCategory!=Integration"` passed (46 test projects).

## Log

- 2026-10-26: Created from CGR-112 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. On Windows, -d/--data-ascii/--json/--variable/-H/-w @- and telnet's stdin without -T are read in text mode (Ctrl-Z ends, CR LF to LF), matching curl 8.22.0
