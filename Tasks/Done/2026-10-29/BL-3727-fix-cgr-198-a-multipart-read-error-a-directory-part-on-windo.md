---
id: BL-3727
title: Fix CGR-198: A multipart read error (a directory part on Windows, or encoder=7bit on 8-bit data) gives the same exit 26 and message, but Curl sends no...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests/Cgr198Tests.cs, Curl.Engine.UnitLibrary/ManagedMimePartBuilder.cs, Curl.Engine.UnitTests/Cgr198Tests.cs, Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Console/CurlCommandRunner.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3727 — Fix CGR-198: A multipart read error (a directory part on Windows, or encoder=7bit on 8-bit data) gives the same exit 26 and message, but Curl sends no...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-198 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-198: A multipart read error (a directory part on Windows, or encoder=7bit on 8-bit data) gives the same exit 26 and message, but Curl sends nothing where curl has already sent the head and the first part

- **Severity:** Low
- **Area:** http-requests
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed: reproduced 2/2 on Windows and 2/2 on Linux (7bit case).
- **Related existing finding (GF/AF):** GF-0018
- **Source findings:** `http-requests HR-10`

**Reproduction**

- *Setup (http-requests):* E = `SCR\evidence\http-requests`. Recording loopback server + differential runner: `dotnet run E\diff.cs -- E\verify.json E\vout1 20335` (Windows, port 20335) and `E/vlinux.sh` (WSL, port 20355; runs `python3 -I E/diff.py E/verify.json ~/hr_verify/v1 20355`). The server answers `200 OK` with body `ok\n` after reading the whole request; in `early` mode it answers right after the request head and keeps reading.
- Run `curl -sS -F "a=<q.txt;encoder=7bit" http://127.0.0.1:20335/a` where q.txt holds the single byte 0xFF (case v10_F_7bit).
- On Windows, also run `mkdir sub` and then `curl -sS -F f=@sub http://127.0.0.1:20335/a` (case v9_F_dir).

Commands (run each with REF and with PORT):

```sh
curl -sS -F "a=<q.txt;encoder=7bit" http://127.0.0.1:20335/a
mkdir sub
curl -sS -F f=@sub http://127.0.0.1:20335/a
```

**Expected (real curl)**

- Writes the POST head (`Content-Length: 182` for the 7bit case; chunked with `Expect: 100-continue` for the directory case) and the first part's headers, then aborts with exit 26 and `curl: (26) read error getting mime data`.

**Actual (Curl)**

- Exits 26 with the same message, but the server receives 0 bytes.

**Suspected location in source**

- the multipart body is fully built before the request is written (multipart building in `Curl.Cli.UnitLibrary` / `Curl.Engine.UnitLibrary`). Exact line not located.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr198Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Measured (Windows, curl 8.22.0 vs Curl, Record-CurlExchange.ps1, absolute paths): `-sS -F "a=<q.txt;encoder=7bit"` real: exit 26, 321 request bytes; Curl before: 0 bytes, after: 321 bytes, identical. `-sS -F f=@sub` real: exit 26, 379 bytes (head, chunked, Expect, first part headers); Curl before: 0 bytes after: 379 bytes with the recorder read window raised to 3000 ms (Curl needs about 1.9 s against real curl 1.1 s because of startup, so at the default 1000 ms window the recorder gives up first; timing only). stderr `curl: (26) read error getting mime data` in both. Linux not run (lane is Windows only); the 7bit change is platform-neutral and the directory change only applies where SendsDirectoryEmpty is off.
- Fix: `CurlCommandRunner` sets `SevenBitRefusalWaitsForSend` and the new `DirectoryReadFailsAtSend` on the -F builder. Added Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs and Curl.Console/CurlCommandRunner.cs to touches (no other Doing task names them). Test in Curl.Cli.UnitTests/Cgr198Tests.cs. CI criterion: change is platform-neutral, fast tests green here.

## Log

- 2026-10-26: Created from CGR-198 of the third-party gap report.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Done. -F with 7bit refusal or a directory part on Windows now sends the head and first part headers before failing exit 26
