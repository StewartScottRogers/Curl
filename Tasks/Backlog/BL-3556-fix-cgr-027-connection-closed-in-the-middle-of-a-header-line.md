---
id: BL-3556
title: Fix CGR-027: Connection closed in the middle of a header line after Content-Length: curl 18, port 0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs, Curl.Protocol.Http.UnitTests/Cgr027Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3556 — Fix CGR-027: Connection closed in the middle of a header line after Content-Length: curl 18, port 0

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-027 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-027: Connection closed in the middle of a header line after Content-Length: curl 18, port 0

- **Severity:** High
- **Area:** network-robustness
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows twice, Linux l_closehdr).
- **Related existing finding (GF/AF):** GF-0012
- **Source findings:** `network-robustness NR-3`

**Reproduction**

- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- `curl -sSi $H/closehdr` (server sends `HTTP/1.1 200 OK\r\nContent-Length: 10\r\nX-Foo: ba` and closes). Minimal: `curl -sSi "$H/raw?b=$(b64 'HTTP/1.1 200 OK\r\nContent-Length: 3\r\nX')"`; `\r` variant: `b64 'HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r'`; `-sSf` with `HTTP/1.1 404 NF\r\nContent-Length: 3\r\nX`.

Commands (run each with REF and with PORT):

```sh
curl -sSi $H/closehdr
curl -sSi "$H/raw?b=$(b64 'HTTP/1.1 200 OK\r\nContent-Length: 3\r\nX')"
```

**Expected (real curl)**

- exit 18 `curl: (18) transfer closed with 10 (3) bytes remaining to read`. In the `\r` variant REF also writes the partial `\r` (37 bytes vs 36). With -f on the 404: 18.

**Actual (Curl)**

- exit 0 with empty stderr; the head is treated as complete. With -f: 22 `The requested URL returned error: 404`.

**Notes**

- With no Content-Length both give 0.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs:546-579`. At EOF with a pending partial line, ReadHeaderLinesAsync calls `builder.EndHeadAtClose()`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr027Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-027 of the third-party gap report.
