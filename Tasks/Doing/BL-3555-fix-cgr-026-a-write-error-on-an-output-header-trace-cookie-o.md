---
id: BL-3555
title: Fix CGR-026: A write error on an output, header, trace, cookie or %output file (ENOSPC, /dev/full) gives exit 2 with an exception message instead of e...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr026Tests.cs, Curl.Console/DeferredOutputFileStream.cs, Curl.Console/DumpHeaderOutputStream.cs, Curl.Console/UnhandledExceptionGuard.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3555 — Fix CGR-026: A write error on an output, header, trace, cookie or %output file (ENOSPC, /dev/full) gives exit 2 with an exception message instead of e...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-026 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-026: A write error on an output, header, trace, cookie or `%output` file (ENOSPC, `/dev/full`) gives exit 2 with an exception message instead of exit 23, and `-w` output is lost

- **Severity:** High
- **Area:** network-robustness, config-glob-output
- **Platform(s):** Linux. Detail: [network-robustness NR-2] Linux. Windows disk-full was not testable, but the code path is shared.; [config-glob-output CGO-09] Linux (Windows has no easy full-device test)
- **Confidence:** Confirmed. Detail: [network-robustness NR-2] Confirmed. curl has mapped write errors to exit 23 for a long time, so this is not an 8.18 artefact.; [config-glob-output CGO-09] Probable. It was reproduced twice, but only against REF-LINUX 8.18. The fclose check in curl's tool_operate.c is long-standing, so 8.22 very likely behaves the same.
- **Related existing finding (GF/AF):** none
- **Source findings:** `network-robustness NR-2`, `config-glob-output CGO-09`

**Reproduction**

- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- *Setup (config-glob-output):* E = `SCR\evidence\config-glob-output`. Windows server: `dotnet run E\srv\server.cs -- 20102 E\verify\log.txt` (listens on 127.0.0.1 and [::1], logs raw requests, replies `200 OK` with body `path=<request target>`; query keys `lm=` adds Last-Modified, `cd=`/`cdhex=` add Content-Disposition, `size=n` body of n `A`, `short=n` over-declares Content-Length, `status=n`). Linux: `python3 E/linux/server.py 20103 log`. `$U` = `http://127.0.0.1:20102` (20103 on Linux). Run each case in a fresh directory with HOME, USERPROFILE and APPDATA pointing at it and CURL_HOME/XDG_CONFIG_HOME unset. Verifier scripts: `E\verify\v1_stdin.ps1`, `v2_misc.ps1`, `v3_misc.ps1`, `v4_maxpath.ps1`, `E\verify\linux\vl.sh`.
- **[network-robustness NR-2]** ENOSPC on an output file gives exit 2 instead of 23; a failed --trace/-c/%output write fails the run
  - *(WSL server 21150, H=http://127.0.0.1:21150):* `curl -sS -o /dev/full $H/ok`; `-D /dev/full -o /dev/null $H/ok`; `--trace /dev/full -o /dev/null $H/ok`; `-c /dev/full -o /dev/null $H/ok`; `-o /dev/null -w '%output{/dev/full}x' $H/ok`; `--stderr /dev/full http://127.0.0.1:21199/`; `-o /dev/full "$H/gen?size=20000"`. Script: `linux/run2.sh` + `linux/cases2.sh`.
- **[config-glob-output CGO-09]** A write error when the output file is closed (/dev/full, ENOSPC) gives exit 2 and an exception message instead of exit 23, and -w output is lost
  - in WSL, run `python3 E/linux/server.py 20103 log`, then:
    - `curl -sS -o /dev/full $U/x`
    - `curl -sS $U/x -o /dev/full -w '%{exitcode} %{errormsg}\n'`
    - `curl -sS --remove-on-error -o /dev/full $U/x`
    - Script: `vl.sh` df1/df3/df5.

Commands (run each with REF and with PORT):

```sh
# [network-robustness NR-2]
curl -sS -o /dev/full $H/ok
# [config-glob-output CGO-09]
python3 E/linux/server.py 20103 log
curl -sS -o /dev/full $U/x
curl -sS $U/x -o /dev/full -w '%{exitcode} %{errormsg}\n'
curl -sS --remove-on-error -o /dev/full $U/x
```

**Expected (real curl)**

- **[network-robustness NR-2]** `curl: (23) Failed writing body` (23); `-D`: `Failed writing headers to /dev/full` + `(23) client returned ERROR on write of 17 bytes`; trace/-c/%output: exit 0 with no message; `--stderr /dev/full`: exit 7; for a body of 16 KiB or more: `(23) Failure writing output to destination, passed 16384 returned 0`.
- **[config-glob-output CGO-09]** *REF behaviour (8.18):* 
  - stderr `curl: curl: (23) Failed writing body`, exit 23.
  - `-w` prints `23 Failed writing received data to disk/application`.

**Actual (Curl)**

- **[network-robustness NR-2]** `curl: (2) No space left on device : '/dev/full'` with exit 2 for -o, -D, --trace, -c, %output and --stderr. For a body of 16 KiB or more it gives exit 23, but the message says `passed 16342` where curl says 16384.
- **[config-glob-output CGO-09]** 
  - stderr `curl: (2) No space left on device : '/dev/full'`, exit 2.
  - `-w` prints nothing.
  - With a 200 KB body, the failure happens during a write and both exit 23 (df2).

**Suspected location in source**

- **[network-robustness NR-2]** the IOException from the buffered flush/close is not turned into CURLE_WRITE_ERROR and falls to `Curl.Console/UnhandledExceptionGuard.cs:31`. See also `Curl.Console/DeferredOutputFileStream.cs`, `DumpHeaderOutputStream.cs`.
- **[config-glob-output CGO-09]** the `IOException` from flushing or disposing the output stream (`Curl.Console/DeferredOutputFileStream.cs`) is not mapped to CURLE_WRITE_ERROR. It reaches `Curl.Console/UnhandledExceptionGuard.cs:31-37`, which returns exit 2.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr026Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-026 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
