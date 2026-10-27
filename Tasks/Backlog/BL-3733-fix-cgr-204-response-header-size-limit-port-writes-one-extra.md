---
id: BL-3733
title: Fix CGR-204: Response-header size limit: PORT writes one extra header line to -i/-D and counts it in %{size_header}; %{num_headers} is 0 after the exi...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpResponseHeadBuilder.cs, Curl.Protocol.Http.UnitTests/Cgr204Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3733 — Fix CGR-204: Response-header size limit: PORT writes one extra header line to -i/-D and counts it in %{size_header}; %{num_headers} is 0 after the exi...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-204 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-204: Response-header size limit: PORT writes one extra header line to `-i`/`-D` and counts it in `%{size_header}`; `%{num_headers}` is 0 after the exit-56 failure

- **Severity:** Low
- **Area:** http-responses, network-robustness, upstream-cases
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [upstream-cases UC-12] Confirmed (on all four binaries).
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-responses HR-16`, `network-robustness NR-10`, `upstream-cases UC-12`

**Reproduction**

- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- **[http-responses HR-16]** Response-header size limit: PORT writes one extra header line to -D/-i, and %{num_headers} is 0
  - `h_many.bin` = a status line plus 4000 headers `X-H<n>: ` + 70 x `v`, then `Content-Length: 2` (about 320 KB).
  - Commands: `curl -s -S -D - -o NUL http://127.0.0.1:20400/h_many` and `curl -s -S -o NUL -w "%{http_code} %{num_headers} %{size_header}" http://127.0.0.1:20400/h_many`
- **[network-robustness NR-10]** %{size_header} after a `Too large response headers` (56) failure counts the last header twice
  - `curl -sS -o NUL -w '%{size_header}\n' "$H/bighdrs?n=4&size=100000"`; also `n=10&size=40000`.
- **[upstream-cases UC-12]** Response-header size limit: PORT writes one more header line to the -i output than curl before failing with exit 56
  - `verify\v3.sh`, server `verify\conf\hdr497.py` (test497 data: 9 headers, then 1750 repeated 178-byte headers, LF line ends). Run `curl -s -S -i -o o.txt http://127.0.0.1:21360/497`.

Commands (run each with REF and with PORT):

```sh
# [http-responses HR-16]
curl -s -S -D - -o NUL http://127.0.0.1:20400/h_many
curl -s -S -o NUL -w "%{http_code} %{num_headers} %{size_header}" http://127.0.0.1:20400/h_many
# [network-robustness NR-10]
curl -sS -o NUL -w '%{size_header}\n' "$H/bighdrs?n=4&size=100000"
# [upstream-cases UC-12]
curl -s -S -i -o o.txt http://127.0.0.1:21360/497
```

**Expected (real curl)**

- **[http-responses HR-16]** exit 56 `Too large response headers: 307277 > 307200`. -D writes 307277 bytes. -w prints `200 3807 307277`.
- **[network-robustness NR-10]** exit 56 `Too large response headers: 400057 > 307200`; -w `400057` (n=10: `320097`).
- **[upstream-cases UC-12]** Exit 56, `curl: (56) Too large response headers: 307325 > 307200`. o.txt has 1744 lines (307325 bytes).

**Actual (Curl)**

- **[http-responses HR-16]** same exit and message. -D writes 307358 bytes (one extra header line). -w prints `200 0 307358`. Same on Linux.
- **[network-robustness NR-10]** the same exit and message, but -w `500067` (n=10: `360107`), exactly one header line too many.
- **[upstream-cases UC-12]** Exit 56 with the same message. o.txt has 1745 lines (307502 bytes): it writes the header that crossed the limit.

**Notes**

- **[upstream-cases UC-12]** *Verifier note:* The original line counts (1741/1742) came from different data; the off-by-one holds.

**Suspected location in source**

- **[http-responses HR-16]** `HttpResponseHeadBuilder.cs:137` (`HeadBytesWithPendingHeader`) also writes the pending header after `Append` throws.
- **[network-robustness NR-10]** `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadBuilder.cs` (size accounting after the overflow).
- **[upstream-cases UC-12]** `Curl.Protocol.Http.UnitLibrary\HttpResponseHeadBuilder.cs` (MaximumHeadSize check after the write).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr204Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-204 of the third-party gap report.
