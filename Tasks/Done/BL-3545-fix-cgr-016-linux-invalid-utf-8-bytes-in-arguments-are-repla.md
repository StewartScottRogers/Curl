---
id: BL-3545
title: Fix CGR-016: Linux: invalid UTF-8 bytes in arguments are replaced with U+FFFD before use
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3849, BL-3850, BL-3851, BL-3852, BL-3853]
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitTests/Cgr016Tests.cs, Curl.Console, Curl.Console.UnitTests/Cgr016Tests.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/Cgr016HeaderCookieTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3545 — Fix CGR-016: Linux: invalid UTF-8 bytes in arguments are replaced with U+FFFD before use

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-016 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-016: Linux: invalid UTF-8 bytes in arguments are replaced with U+FFFD before use

- **Severity:** High
- **Area:** cli-options
- **Platform(s):** Linux
- **Confidence:** Confirmed. Detail: Confirmed. curl never re-encodes argv, so this does not depend on the REF version.
- **Related existing finding (GF/AF):** none
- **Source findings:** `cli-options CLIO-02`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *(WSL, echo.py on 20001):* `curl -s -d $'\xff' URL`; `-H $'X-A: \xff'`; `$'http://127.0.0.1:20001/\xff'`; `-o $'\xff.txt'`; `-o /dev/null -w $'\xfe'`; `--data-urlencode $'\xff'`; `-b $'a=\xff'`. Scripts: `linux/t3.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.

Commands (run each with REF and with PORT):

```sh
curl -s -d $'\xff' URL
```

**Expected (real curl)**

- the raw bytes are sent. Body 0xFF, header `X-A: \xff`, `GET /%FF`, file `\xff.txt`, `-w` writes 0xFE, `%FF`, `Cookie: a=\xff`.

**Actual (Curl)**

- every invalid byte becomes EF BF BD. Body EF BF BD (Content-Length 3), the header value EF BF BD, `GET /%EF%BF%BD`, file `EF BF BD.txt`, `-w` writes EF BF BD, `%EF%BF%BD`. The cookie value becomes `?`.

**Suspected location in source**

- argv reaches .NET already decoded as UTF-8 (Curl.Console Program entry) and is re-encoded per option, e.g. `CommandLineOptions.cs:2377`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr016Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Split for size (2026-10-26, lane 8, before any code): the fix needs Linux and macOS to read argv as raw bytes and then five separate byte paths changed (request body, headers and cookies, URL, `-w` output, `-o` file names, the last needing a libc `open` P/Invoke since .NET cannot name a file with an invalid UTF-8 byte). Filed BL-3849 (raw argv with a lossless surrogate-escape decoding, plus `-d` and `--data-urlencode`), BL-3850 (`-H`, `-b`), BL-3851 (URL), BL-3852 (`-w`) and BL-3853 (`-o`); BL-3850 to BL-3853 depend on BL-3849. Once they are Done this task only re-measures every repro against curl 8.18.0 in WSL, records both results here, and adds the `Cgr016Tests.cs` checks its touches name.

- Re-measured 2026-10-26 (lane 6) in WSL Ubuntu: REF is curl 8.18.0 (OpenSSL), PORT a linux-x64 build of this commit (`dotnet publish Curl.Console -c Release -r linux-x64 --self-contained -p:PublishAot=false`). Each command ran from an empty directory against `nc -N -l 127.0.0.1 <port>` replying `200`, `Content-Length: 5`, `hello`; the script compared exit code, stdout, stderr, the files written and the request bytes (Host and User-Agent lines normalised, as section 1 of the report does). Ubuntu's Rust `timeout` refuses invalid UTF-8 arguments, so the binaries ran unwrapped. Results, REF and PORT identical on every one, exit 0, stderr empty:
  - `curl -s -d $'\xff' URL`: `Content-Length: 1`, body FF; stdout `hello`.
  - `-H $'X-A: \xff'`: `X-A: ` FF.
  - `$'http://127.0.0.1:<port>/\xff'`: `GET /%FF HTTP/1.1`.
  - `-o $'\xff.txt'`: file name `ff 2e 74 78 74`, contents `hello`, stdout empty.
  - `-o /dev/null -w $'\xfe'`: stdout the one byte FE.
  - `--data-urlencode $'\xff'`: `Content-Length: 3`, body `%FF`.
  - `-b $'a=\xff'`: `Cookie: a=` FF. Before this run PORT sent `a=` C3 BF: BL-3850 turned the escape U+DCFF into U+00FF in the parse, and the console's cookie engine then encoded the string as UTF-8 (CGR-065's platform encoding), so the byte was encoded twice.
  - Variants that already matched still match: `-d $'\xc3\xa9' -H $'X-B: \xc3\xa9'` (C3 A9 in both), and `-b $'a=\xff' -b $'c=\xc3\xa9'` gives `Cookie: a=` FF `; c=` C3 A9 in both.
- Fix: a command-line `-b` string now keeps the escape (`CommandLineOptionTable`, only a config-file value still goes through `AsRawWireText`), and `CurlComposition.CookieStringEncodingFor` gives the cookie engine `InvalidUtf8ByteEscape.Encoding` off Windows (identical to UTF-8 for valid text, the escape back to its byte) and the ANSI code page on Windows as before. Decision (sensible default, no new ADR): this applies ADR-0821's escape to the cookie string encoding and decides nothing new. Windows argv is never escaped, so Windows is unchanged.
- Tests: `Curl.Console.UnitTests/Cgr016Tests.cs` pins the encoding (FF back as FF, valid text as UTF-8, Windows the ANSI code page; it does not compile before the change), and `Cgr016HeaderCookieTests`' two invalid-byte cookie tests now expect the escape kept (they fail against the old parse). `Curl.Cli.UnitTests/Cgr016Tests.cs` covers the `-X` escape branches the cookie tests used to reach. Tests are platform-neutral (strings and byte arrays only).
- Added to touches: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` and `Curl.Cli.UnitTests/Cgr016HeaderCookieTests.cs` (the `-b` parse lives there); no task in Doing on origin/work/dark-factory names them.
- Found outside this issue's repros, filed: BL-3894 (`-X $'G\xffT'` sends `G` C3 BF `T`, curl sends `G` FF `T`) and BL-3895 (a config file's `cookie = "c=é"` sends C3 83 C2 A9 on Linux, curl C3 A9; it was like that before this run).
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary,Curl.Console` flagged one branch of `IsEscapedByte` (only the cookie tests reached it); after `Cgr016Tests` covered it, `-Library Curl.Cli.UnitLibrary` reports 100% line, 100% branch, 0 failing members; Curl.Console 100/100, 0 failing. `dotnet build` clean; fast tests 46 test projects passed, 0 failed (Curl.Cli.UnitTests 4376, Curl.Console.UnitTests 2934 before the two `-X` tests were added). CI on Linux and macOS: the change and tests are platform-neutral.

## Log

- 2026-10-26: Created from CGR-016 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Split for size: waits on BL-3849, BL-3850, BL-3851, BL-3852 and BL-3853 (raw argv bytes on Linux and macOS, then body, headers and cookies, URL, -w and -o paths)
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Every CGR-016 repro (-d, -H, URL, -o, -w, --data-urlencode, -b) sends and writes the raw bytes curl 8.18.0 does on Linux
