---
id: BL-3644
title: Fix CGR-115: Linux: PORT writes ESC[?1h ESC= (application cursor/keypad mode) to the terminal on every run when a standard stream is a TTY, and never...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3892]
touches: [Curl.Console.UnitTests/Cgr115Tests.cs, Curl.Console/TerminalColumns.cs, Curl.Console/NativeTerminalWindowSize.cs, Curl.Console/StandardOutputOpener.cs, Curl.Console/Program.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3644 — Fix CGR-115: Linux: PORT writes ESC[?1h ESC= (application cursor/keypad mode) to the terminal on every run when a standard stream is a TTY, and never...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-115 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-115: Linux: PORT writes `ESC[?1h ESC=` (application cursor/keypad mode) to the terminal on every run when a standard stream is a TTY, and never resets it

- **Severity:** Medium
- **Area:** environment, http-responses
- **Platform(s):** Linux
- **Confidence:** Confirmed. Detail: [environment ENV-09] Confirmed. The cause is a .NET runtime artefact, so the curl version does not matter.
- **Related existing finding (GF/AF):** none
- **Source findings:** `environment ENV-09`, `http-responses HR-9`

**Reproduction**

- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- **[environment ENV-09]** On Linux, the port writes `ESC[?1h ESC=` (application cursor/keypad mode) to the terminal whenever a standard stream is a TTY
  - set `TERM=xterm` and run under a pty:
    - `script -qec "~/curlnet/curl -s http://127.0.0.1:21210/echo -o /dev/null -w ok" /dev/null | cat -v`
    - With stdout redirected to a file and an error on stderr: `script -qec "~/curlnet/curl -sS http://127.0.0.1:21299/ > o.txt" /dev/null`
    - `linux/verify.sh` section F9 covers both.
- **[http-responses HR-9]** PORT-LINUX writes ESC[?1h ESC= (application cursor/keypad mode) to the terminal on every run and never resets it
  - *(WSL):* 
    - `script -qec "~/curlnet/curl -s http://127.0.0.1:20450/basic_D" /dev/null | od -c`
    - `script -qec "~/curlnet/curl -s http://127.0.0.1:20450/basic_D > /tmp/o.txt" /dev/null | od -c` (only stderr on the TTY)
    - `script -qec "~/curlnet/curl --version > /dev/null" /dev/null | od -c`
    - Scripts: `EV\linux\tty.sh`, `tty2.sh`, `tty3.sh`, `EV\verify\linux\vrun.sh`.

Commands (run each with REF and with PORT):

```sh
# [environment ENV-09]
export TERM=xterm
script -qec "~/curlnet/curl -s http://127.0.0.1:21210/echo -o /dev/null -w ok" /dev/null | cat -v
script -qec "~/curlnet/curl -sS http://127.0.0.1:21299/ > o.txt" /dev/null
# [http-responses HR-9]
script -qec "~/curlnet/curl -s http://127.0.0.1:20450/basic_D" /dev/null | od -c
script -qec "~/curlnet/curl -s http://127.0.0.1:20450/basic_D > /tmp/o.txt" /dev/null | od -c
script -qec "~/curlnet/curl --version > /dev/null" /dev/null | od -c
EV\linux\tty.sh
EV\verify\linux\vrun.sh
```

**Expected (real curl)**

- **[environment ENV-09]** `ok`, and the error message, with no control sequences.
- **[http-responses HR-9]** `/usr/bin/curl` writes only `hello`, or nothing when stdout is redirected. No control sequences.

**Actual (Curl)**

- **[environment ENV-09]** 
  - The first command prints `^[[?1h^[=ok`.
  - The second prints `^[[?1h^[=curl: (7) ...`.
  - Nothing resets the mode at exit.
  - `TERM=dumb` gives the same output as REF.
- **[http-responses HR-9]** writes `\033[?1h\033=` before any output (`\033[?1h\033=hello`). This happens even with `-s`, with stdout redirected, and with `--version`. No reset sequence follows. Not version-dependent: no curl version emits it.

**Suspected location in source**

- **[environment ENV-09]** `Curl.Console\TerminalColumns.cs:102` (`System.Console.WindowWidth` initialises the .NET terminfo console, which emits `keypad_xmit`).
- **[http-responses HR-9]** `Curl.Console\TerminalColumns.cs:102` (`System.Console.WindowWidth`). On Unix this initialises .NET's terminfo console, which writes keypad_xmit.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr115Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 (lane 3, first claim, no code edited): the cause is wider than `TerminalColumns.cs`. On Unix .NET's `ConsolePal.EnsureConsoleInitialized` (which writes terminfo `keypad_xmit`, `ESC[?1h ESC=`, to stdout's terminal, or stdin's when stdout is redirected) runs from `Console.WindowWidth` AND from every write through a `UnixConsoleStream`: `ConsolePal.WriteFromConsoleStream` calls it. That was checked by decoding the IL of the linux-x64 10.0.14 `System.Console.dll` from the NuGet cache with a System.Reflection.Metadata file app. Curl writes through such streams in two places: `Program.cs`'s `System.Console.OpenStandardError()` and `StandardOutputOpener`'s console branch (stdout on a terminal). So the fix needs three parts:
  1. `TerminalColumns`: off Windows, read the width as curl's `get_terminal_columns` does, `ioctl(STDIN_FILENO, TIOCGWINSZ)`, through the runtime's own shim. The linux-x64 System.Console metadata gives its signature as `int SystemNative_GetWindowSize(IntPtr fd, WinSize*)`, where WinSize is 4 x ushort (Row, Col, XPixel, YPixel); it returns 0 on success. Use `[LibraryImport("libSystem.Native", EntryPoint = "SystemNative_GetWindowSize")]` in a new `NativeTerminalWindowSize.cs`, `[ExcludeFromCodeCoverage]` as `NativeRegularFileTest` is (ADR-0083). Don't P/Invoke libc `ioctl`: it is variadic, which breaks on macOS arm64. Keep `Console.WindowWidth` (stderr, minus one) on Windows only, behind a testable `runsOnWindows` choice so both branches are covered on Windows.
  2. `StandardOutputOpener`: off Windows, open stdout on a terminal as the same unbuffered `FileStream` over fd 1 as the redirected branch, not `Console.OpenStandardOutput()`.
  3. `Program.cs`: off Windows, open stderr as an unbuffered `FileStream` over fd 2 (still inside `WriteFailureIgnoringStream`), not `Console.OpenStandardError()`. `OpenStandardInput` is fine: reads do not initialise the console.
  Requeued behind BL-3892, which holds `Curl.Console/Program.cs` (in Doing on origin/work/dark-factory). Touches widened to the three files above for that reason. Write an ADR for the decision. The unit test goes in `Cgr115Tests.cs`: the width read picks the stdin-terminal reader off Windows and never calls the window-width reader, and the opener picks the handle stream for a terminal stdout off Windows.
## Log

- 2026-10-26: Created from CGR-115 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Needs Curl.Console/Program.cs, held by BL-3892 (Doing); fix plan recorded in Notes
