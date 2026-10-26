---
id: BL-3599
title: Fix CGR-070: An unsolicited 101 Switching Protocols is treated as an interim 1xx response, so the body is not written and the transfer fails with exit 8
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs, Curl.Protocol.Http.UnitTests/Cgr070Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3599 — Fix CGR-070: An unsolicited 101 Switching Protocols is treated as an interim 1xx response, so the body is not written and the transfer fails with exit 8

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-070 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-070: An unsolicited `101 Switching Protocols` is treated as an interim 1xx response, so the body is not written and the transfer fails with exit 8

- **Severity:** Medium
- **Area:** http-responses, network-robustness
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0083
- **Source findings:** `http-responses HR-3`, `network-robustness NR-4`

**Reproduction**

- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- **[http-responses HR-3]** Unsolicited 101 Switching Protocols is treated as an interim 1xx, so the body fails with exit 8
  - `x101b.bin` = `HTTP/1.1 101 Switching\r\n\r\nhello`, then the server closes. Command: `curl -s -S http://127.0.0.1:20400/x101b`
  - Variants: `x101` (`Upgrade: foo`, run with `-i`), `x101c` (`Content-Length: 2`), `x101_10` (`HTTP/1.0 101`).
- **[network-robustness NR-4]** Unsolicited `101 Switching Protocols` fails with exit 8 `Invalid status line`
  - `curl -sSi "$H/raw?b=$(b64 'HTTP/1.1 101 Switching\r\n\r\nabc')"`; add `-w '%{http_code} %{exitcode}'`. Also with `Upgrade: h2c`/`Connection: Upgrade` headers (Linux l_101b).

Commands (run each with REF and with PORT):

```sh
# [http-responses HR-3]
curl -s -S http://127.0.0.1:20400/x101b
# [network-robustness NR-4]
curl -sSi "$H/raw?b=$(b64 'HTTP/1.1 101 Switching\r\n\r\nabc')"
```

**Expected (real curl)**

- **[http-responses HR-3]** exit 0. stdout is `hello`. With `-i`, the 101 head is followed by `hello`.
- **[network-robustness NR-4]** exit 0, stdout `HTTP/1.1 101 Switching\r\n\r\nabc`; -w `101 0`.

**Actual (Curl)**

- **[http-responses HR-3]** exit 8 `curl: (8) Invalid status line`, and no body is written. With `-i` only the 101 head is written. Same on Linux.
- **[network-robustness NR-4]** exit 8 `curl: (8) Invalid status line`; the body is not written; -w `101 8`.

**Suspected location in source**

- **[http-responses HR-3]** `Curl.Protocol.Http.UnitLibrary\HttpResponseHeadReader.cs` around line 470-517: 101 is treated as an interim response (`SwitchedProtocols |= StatusCode == 101` at line 517).
- **[network-robustness NR-4]** `HttpResponseHeadReader.cs` treats 101 as informational and expects another status line

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr070Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-070 of the third-party gap report.
