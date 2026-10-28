---
id: BL-3704
title: Fix CGR-175: -F name=@- shared by several URLs: PORT sends an empty part and exits 0 in serial mode, where REF fails the later transfers with (26); un...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3968, BL-3969]
touches: [Curl.Console.UnitTests/Cgr175Tests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/EasyOptionsFromCommandLine.cs, Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Core.UnitTests/Cgr175Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3704 — Fix CGR-175: -F name=@- shared by several URLs: PORT sends an empty part and exits 0 in serial mode, where REF fails the later transfers with (26); un...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-175 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-175: -F name=@- shared by several URLs: PORT sends an empty part and exits 0 in serial mode, where REF fails the later transfers with (26); under -Z the bodies differ

- **Severity:** Medium
- **Area:** parallel-multi-transfer
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows, Linux serial and parallel)
- **Related existing finding (GF/AF):** GF-0018
- **Source findings:** `parallel-multi-transfer PMT-7`

**Reproduction**

- *Setup (parallel-multi-transfer):* EV = `SCR\evidence\parallel-multi-transfer`. Server: `dotnet run EV\srv\server.cs -- 22000,22001 EV\srv\log` (listens on 127.0.0.1:22000 and :22001; query `d=` delay in ms, `s=` status, `n=` body size, `p=`/`pd=` send the body in parts with a delay, `close=1` adds Connection: close; other paths return `body /<path>`); port 22099 is closed. Linux: `EV/linux/srv.py 22000,22001 /tmp/pmt_srv` (started by `EV/linux/lt.sh`). Harness `EV\dt.ps1`; verifier `EV\v1.ps1` and `EV/linux/vl.sh`.
- pipe `STDIN-DATA-123` into each command.
  - Serial: `curl -sS -w "%{urlnum} %{exitcode} %{http_code} %{size_upload}\n" -F "f=@-" http://127.0.0.1:22000/a http://127.0.0.1:22000/b`
  - Parallel: `curl -Z --parallel-immediate -sS -w "%{urlnum} %{exitcode} %{http_code} %{size_upload}\n" -F "f=@-" "http://127.0.0.1:22000/a?d=200" "http://127.0.0.1:22000/b?d=100"`

  Scripts: `v1.ps1` (v_F_ser, v_F_par), `linux/vl.sh` (vl_F_ser, vl_F_par).

Commands (run each with REF and with PORT):

```sh
curl -sS -w "%{urlnum} %{exitcode} %{http_code} %{size_upload}\n" -F "f=@-" http://127.0.0.1:22000/a http://127.0.0.1:22000/b
curl -Z --parallel-immediate -sS -w "%{urlnum} %{exitcode} %{http_code} %{size_upload}\n" -F "f=@-" "http://127.0.0.1:22000/a?d=200" "http://127.0.0.1:22000/b?d=100"
```

**Expected (real curl)**

- Serial: stdout `body /a\n0 0 200 176\n1 26 000 162\n`, stderr `curl: (26) client mime read EOF fail, only 162/176 of needed bytes read`, exit 26.
- Parallel (8.22): stdout `body /b\n1 0 200 176\nbody /a\n0 0 200 0\n`. /b carries the stdin data, and /a is sent with an empty body.

**Actual (Curl)**

- Serial: b is sent with an empty `f` part (162 bytes). stdout `body /a\n0 0 200 176\nbody /b\n1 0 200 162\n`, no stderr, exit 0.
- Parallel: stdout `body /b\n1 0 200 162\nbody /a\n0 0 200 176\n`. /a carries the data, and /b gets an empty part.

**Suspected location in source**

- `Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs:383` and `:502` (`AddStandardInputPartAsync`), and `Curl.Console/EasyOptionsFromCommandLine.cs:573`. When stdin is already drained, PORT builds an empty part instead of failing with a short read.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr175Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-28 (lane 3, no code edited): measured real curl 8.22.0 Schannel (`C:\Program Files\Git\ucrt64\bin\curl.exe`) with `Record-CurlExchange.ps1` (stdin `STDIN-DATA-123`, 14 bytes, no newline; responses `HTTP/1.1 200 OK` with a short body; call the script from the PowerShell tool, since `pwsh -File` from Bash cannot pass the `-CurlArgs` array):
  - Serial `-sS -w "%{urlnum} %{exitcode} %{http_code} %{size_upload}\n" -F f=@- URL/a URL/b`: stdout `body /a\n0 0 200 176\n1 26 000 162\n`, stderr `curl: (26) client mime read EOF fail, only 162/176 of needed bytes read`, exit 26. On the wire only `/a`'s request (Content-Length 176, the stdin part); for `/b` nothing at all is sent on the kept-alive connection (the read fails before the first send).
  - Parallel `-Z --parallel-immediate` same command (150 ms response delay): `/a` is sent as `POST /a` with `Content-Length: 0` and NO `Content-Type` (`0 0 200 0`), `/b` gets the whole form with the stdin data (`1 0 200 176`), exit 0.
  - The parallel half is not about stdin: `-Z -F f=value` with two URLs gives the same (`/a` an empty POST, `/b` 153 bytes), `-Z --parallel-max 2 -F f=value a b c` sends the form only to `/c` (a and b empty POSTs), and `-Z --parallel-max 1 -F f=@- a b` still sends `/a` empty and `/b` the data. So under `-Z` only the option group's LAST transfer gets its `-F` form; every earlier one is an empty POST, as curl's tool sets one `curl_mime` on every handle it sets up ahead (its 2 x parallel-max look-ahead) and `CURLOPT_MIMEPOST` moves the mime to the newest handle. Model: last transfer of the group (command-line order) gets the form; note the look-ahead window as unmodelled in the ADR.
- Design for the next claim (needs `Curl.Console/CurlCommandRunner.cs`, held by BL-3699 in Doing, hence the requeue): (1) serial - the runner keeps, per option group (`CommandLineOptions` identity), whether its `@-`/`<-` parts were already read; the first form build of the group reads standard input whole (as now), later builds of the same group declare the same length but the part reads nothing, failing the transfer before anything is sent with exit 26 and `client mime read EOF fail, only <sent>/<total> of needed bytes read` (sent = total minus the stdin bytes). That needs a `MultipartFormBodyBuilder` hook to build with given/spent stdin bytes. A second `@-` part in the same form, or another group's, still reads what is left (empty, no failure). (2) parallel - in `TransferUploadingAsync`, under `-Z`, a transfer that is not its group's last sends an empty body (a zero-length `BytesBody` with an empty content type: `HttpRequestHeadFormatter.AppendUnlessOverridden` drops an empty `Content-Type`, so the request carries only `Content-Length: 0`). No other prerequisite found: the HTTP formatter already omits an empty content type.

## Log

- 2026-10-26: Created from CGR-175 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Needs Curl.Console/CurlCommandRunner.cs, held by BL-3699 in Doing; real curl measured and design recorded in Notes
