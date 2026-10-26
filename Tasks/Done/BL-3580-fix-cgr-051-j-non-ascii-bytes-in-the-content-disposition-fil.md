---
id: BL-3580
title: Fix CGR-051: -J: non-ASCII bytes in the Content-Disposition filename are always decoded as UTF-8 (invalid bytes become U+FFFD), giving a different sav...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr051Tests.cs, Curl.Console/ContentDispositionFileName.cs, Curl.Console/RemoteHeaderNameStream.cs, Curl.Console.UnitTests/ContentDispositionFileNameTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3580 — Fix CGR-051: -J: non-ASCII bytes in the Content-Disposition filename are always decoded as UTF-8 (invalid bytes become U+FFFD), giving a different sav...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-051 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-051: `-J`: non-ASCII bytes in the Content-Disposition filename are always decoded as UTF-8 (invalid bytes become U+FFFD), giving a different saved file name

- **Severity:** Medium
- **Area:** config-glob-output, http-responses
- **Platform(s):** Both (Windows and Linux). Detail: [http-responses HR-8] Windows (Linux matches)
- **Confidence:** Confirmed. Detail: [config-glob-output CGO-08] Confirmed on Windows and Linux.
- **Related existing finding (GF/AF):** none
- **Source findings:** `config-glob-output CGO-08`, `http-responses HR-8`

**Reproduction**

- *Setup (config-glob-output):* E = `SCR\evidence\config-glob-output`. Windows server: `dotnet run E\srv\server.cs -- 20102 E\verify\log.txt` (listens on 127.0.0.1 and [::1], logs raw requests, replies `200 OK` with body `path=<request target>`; query keys `lm=` adds Last-Modified, `cd=`/`cdhex=` add Content-Disposition, `size=n` body of n `A`, `short=n` over-declares Content-Length, `status=n`). Linux: `python3 E/linux/server.py 20103 log`. `$U` = `http://127.0.0.1:20102` (20103 on Linux). Run each case in a fresh directory with HOME, USERPROFILE and APPDATA pointing at it and CURL_HOME/XDG_CONFIG_HOME unset. Verifier scripts: `E\verify\v1_stdin.ps1`, `v2_misc.ps1`, `v3_misc.ps1`, `v4_maxpath.ps1`, `E\verify\linux\vl.sh`.
- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- **[config-glob-output CGO-08]** -J with non-ASCII bytes in the Content-Disposition filename gives a different file name (always decoded as UTF-8, invalid bytes become U+FFFD)
  - `curl -sS -OJ "$U/x?cdhex=636166C3A92E747874"` (UTF-8 `café.txt`)
  - `curl -sS -OJ "$U/x?cdhex=636166E92E747874"` (Latin-1 byte 0xE9)
  - Scripts: `E\verify\v2_misc.ps1` j1/j2 and `vl.sh` j1/j2.
- **[http-responses HR-8]** -J on Windows: non-ASCII Content-Disposition filename bytes are decoded as UTF-8 instead of being used as ANSI-code-page bytes
  - `j_high.bin` = `HTTP/1.1 200 OK\r\nContent-Disposition: attachment; filename="caf\xe9.txt"\r\nContent-Length: 5\r\n\r\nhello`. Command (in an empty directory): `curl -s -S -OJ http://127.0.0.1:20400/j_high`
  - `j_utf8` uses `filename="caf\xc3\xa9.txt"`.

Commands (run each with REF and with PORT):

```sh
# [config-glob-output CGO-08]
curl -sS -OJ "$U/x?cdhex=636166C3A92E747874"
curl -sS -OJ "$U/x?cdhex=636166E92E747874"
E\verify\v2_misc.ps1
# [http-responses HR-8]
curl -s -S -OJ http://127.0.0.1:20400/j_high
```

**Expected (real curl)**

- **[config-glob-output CGO-08]** 
  - Windows reads the bytes in the ANSI code page: the UTF-8 name gives `cafÃ©.txt`, and 0xE9 gives `café.txt`.
  - Linux writes the raw bytes: `caf\351.txt`.
- **[http-responses HR-8]** creates `café.txt` from byte 0xE9 (CP1252), and `cafÃ©.txt` from the bytes C3 A9.

**Actual (Curl)**

- **[config-glob-output CGO-08]** 
  - Windows: `café.txt` from the UTF-8 name, and `caf\uFFFD.txt` from 0xE9.
  - Linux: valid UTF-8 matches REF, but 0xE9 becomes `caf\357\277\275.txt` (U+FFFD), so the original byte is lost.
- **[http-responses HR-8]** creates `caf\uFFFD.txt` (replacement character) from 0xE9, and `café.txt` from C3 A9. Both saved names differ from REF's.

**Suspected location in source**

- **[config-glob-output CGO-08]** `Curl.Console/ContentDispositionFileName.cs:113` (`Encoding.UTF8.GetString`).
- **[http-responses HR-8]** `Curl.Console\ContentDispositionFileName.cs:113` (`Encoding.UTF8.GetString`)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr051Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 run (lane 7): measured with `Record-CurlExchange.ps1` on Windows (code page 1252). The canned response was `HTTP/1.1 200 OK\r\nContent-Disposition: attachment; filename="<name>"\r\nContent-Length: 5\r\n\r\nhello` and the command `curl -s -S -OJ http://127.0.0.1:20581/j`, run in an empty directory (Curl through `dotnet run --no-build --project Curl.Console --`):
  - `caf\xE9.txt` (HR-8 j_high, CGO-08 `cdhex=636166E92E747874`): curl 8.22.0 Schannel gives exit 0, empty stdout and stderr, and writes `café.txt` (U+00E9) holding `hello`. Curl before the fix wrote `caf�.txt`. Curl after the fix gives exit 0, empty stdout and stderr, and `café.txt` holding `hello`, with request bytes identical to curl's.
  - `caf\xC3\xA9.txt` (j_utf8, `cdhex=636166C3A92E747874`): curl writes `cafÃ©.txt` (U+00C3 U+00A9). Curl before the fix wrote `café.txt`. Curl after the fix writes `cafÃ©.txt`, with exit 0, empty stdout and stderr, and identical request bytes.
  - The `?cdhex=` URLs of CGO-08 differ only in the URL, which `-J` does not use once the header names the file, so they take the same path.
- Fix (ADR-0821): `ContentDispositionFileName.Find` now takes the encoding to read the name in. `RemoteHeaderNameStream` passes `CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())` (the system ANSI code page on Windows, UTF-8 elsewhere) unless a test gives one. I chose to compute the default in the stream rather than pass it from `CurlCommandRunner.cs`, because BL-3584 (in Doing) holds that file.
- Touches widened to `Curl.Console/RemoteHeaderNameStream.cs` (the encoding seam) and `Curl.Console.UnitTests/ContentDispositionFileNameTests.cs` (its calls take the new argument). No task in Doing on origin/work/dark-factory names either file.
- Linux: valid UTF-8 names still decode as UTF-8, so the variant that already matched still matches (`Cgr051Tests.Find_Utf8BytesInUtf8_GivesTheUtf8Name`). The raw non-UTF-8 byte case off Windows (`caf\351.txt`) cannot be created through `System.IO` string paths, so it is filed as BL-3855. The new tests use an explicit Windows-1252 or UTF-8 encoding, so their results do not depend on the platform (CI on Linux and macOS: platform-neutral, fast tests green here).
- `Cgr051Tests` (4 tests) pins the code-page names. The old `Find` read UTF-8 on every platform, which gave `caf�.txt` and `café.txt` for the 1252 cases, so these tests fail against it.
- Gates: `dotnet build` clean (0 warnings); fast tests passed in all 46 assemblies, 0 failures; `Measure-CodeQuality.ps1 -Library Curl.Console` reports 0 failing members; `dotnet format --verify-no-changes` is clean on the changed files.

## Log

- 2026-10-26: Created from CGR-051 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. -J reads the Content-Disposition name in the ANSI code page on Windows, matching curl 8.22.0 Schannel for both CGR-051 repros; Linux raw bytes filed as BL-3855
