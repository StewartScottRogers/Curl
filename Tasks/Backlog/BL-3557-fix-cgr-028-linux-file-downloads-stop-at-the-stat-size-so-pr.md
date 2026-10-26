---
id: BL-3557
title: Fix CGR-028: Linux: file:// downloads stop at the stat() size, so procfs/sysfs files, FIFOs, /dev/stdin pipes and character devices return no data wit...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs, Curl.Protocol.File.UnitTests/Cgr028Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3557 — Fix CGR-028: Linux: file:// downloads stop at the stat() size, so procfs/sysfs files, FIFOs, /dev/stdin pipes and character devices return no data wit...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-028 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-028: Linux: file:// downloads stop at the stat() size, so procfs/sysfs files, FIFOs, `/dev/stdin` pipes and character devices return no data with exit 0

- **Severity:** High
- **Area:** environment, ftp-file-tftp
- **Platform(s):** Linux
- **Confidence:** Confirmed. Detail: [environment ENV-03] Confirmed (reproduced by the verifier). Upstream `file.c` has read until EOF for a long time, so this does not depend on the 8.18 version.; [ftp-file-tftp FILE-1] Confirmed. This is not version-dependent: curl has always read file:// until EOF.
- **Related existing finding (GF/AF):** GF-0074
- **Source findings:** `environment ENV-03`, `ftp-file-tftp FILE-1`

**Reproduction**

- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- *Setup (ftp-file-tftp):* EV = `SCR\evidence\ftp-file-tftp`. Scripted FTP/TFTP stub: `EV\stub\win3\stub.exe` (Linux `EV/stub/linux/stub`, source `EV\stub\Program.cs`) with fixture root `EV\fixture\root` (file.txt is 18 bytes). Differential harness `EV\dt.sh NAME 'script' curl-args`, where `@U@` = `ftp://127.0.0.1:$CPORT`, script lines are `CMD=reply` or `OPT key=value` separated by `;;`, and `STUBEXTRA=EV\tls.pfx` enables FTPS. Verifier: `EV\v1.sh`, `EV\v2.sh` (Windows), `EV\vl.sh` (Linux).
- **[environment ENV-03]** On Linux, file:// reads of files that report size 0 (procfs, FIFOs, `/dev/stdin` from a pipe, character devices) return no data and exit 0
  - `~/curlnet/curl -sS file:///proc/version`
  - `printf pipe-in | ~/curlnet/curl -sS file:///dev/stdin`
  - `~/curlnet/curl -sS -r 0-9 -o z.bin file:///dev/zero`
  - `mkfifo fifo; (printf 'fifo-data\n' > fifo &); ~/curlnet/curl -sS -m 5 file://$PWD/fifo`
  - `linux/verify.sh` covers these as v-proc, v-zero, v-fifo and the pipe lines.
- **[ftp-file-tftp FILE-1]** file:// downloads stop at the stat() size, so procfs/sysfs files give empty output and `/dev/zero` gives 0 bytes
  - WSL, no server. Run `curl -sS file:///proc/version` and `curl -sS -m 3 --max-filesize 100 file:///dev/zero`. Cases: `v_proc` and `v_zero` (`EV\vl.sh`); originals `EV\cases-linux\f_proc*`, `f_fifo`.

Commands (run each with REF and with PORT):

```sh
# [environment ENV-03]
~/curlnet/curl -sS file:///proc/version
printf pipe-in | ~/curlnet/curl -sS file:///dev/stdin
~/curlnet/curl -sS -r 0-9 -o z.bin file:///dev/zero
mkfifo fifo; (printf 'fifo-data\n' > fifo &); ~/curlnet/curl -sS -m 5 file://$PWD/fifo
# [ftp-file-tftp FILE-1]
curl -sS file:///proc/version
curl -sS -m 3 --max-filesize 100 file:///dev/zero
EV\vl.sh
```

**Expected (real curl)**

- **[environment ENV-03]** Reads until EOF. Prints the kernel version string, `pipe-in` and `fifo-data`, and `z.bin` gets 10 bytes.
- **[ftp-file-tftp FILE-1]** Prints `Linux version 6.6.87.2-microsoft-standard-WSL2 ...` with exit 0. For /dev/zero it streams 100 NUL bytes, then exits 63 with `curl: (63) Exceeded the maximum allowed file size (100) with 100 bytes`.

**Actual (Curl)**

- **[environment ENV-03]** Writes nothing (0 bytes) for all four, exit 0. Regular files work.
- **[ftp-file-tftp FILE-1]** Empty output with exit 0 in both cases.

**Suspected location in source**

- **[environment ENV-03]** `Curl.Protocol.File.UnitLibrary\FileProtocolHandler.cs:572`. `DownloadBodyAsync(context, source, opened.Length)` copies only `opened.Length` bytes.
- **[ftp-file-tftp FILE-1]** `Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs:572` (`DownloadBodyAsync(context, source, opened.Length)`) and the copy loop at `:962` (`while (consumed < count)`).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr028Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-028 of the third-party gap report.
