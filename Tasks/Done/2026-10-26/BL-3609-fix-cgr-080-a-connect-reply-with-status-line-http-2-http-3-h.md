---
id: BL-3609
title: Fix CGR-080: A CONNECT reply with status line HTTP/2, HTTP/3, HTTP/2.0 or HTTP/1.2 is accepted as a tunnel; curl fails with exit 7
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HttpProxyTunnel.cs, Curl.Networking.UnitTests/Cgr080Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3609 — Fix CGR-080: A CONNECT reply with status line HTTP/2, HTTP/3, HTTP/2.0 or HTTP/1.2 is accepted as a tunnel; curl fails with exit 7

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-080 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-080: A CONNECT reply with status line HTTP/2, HTTP/3, HTTP/2.0 or HTTP/1.2 is accepted as a tunnel; curl fails with exit 7

- **Severity:** Medium
- **Area:** proxy-connect
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `proxy-connect PC-02`

**Reproduction**

- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- run `srv 20600 log 'HN;W:HTTP/2 200\r\n\r\n;H;OK'`, then `curl -sS -p -x http://127.0.0.1:20600 http://example.test/`. Also tried `HTTP/1.2 200 OK`. Verifier cases: v06, v07 and VL05.

Commands (run each with REF and with PORT):

```sh
srv 20600 log 'HN;W:HTTP/2 200\r\n\r\n;H;OK'
curl -sS -p -x http://127.0.0.1:20600 http://example.test/
```

**Expected (real curl)**

- exit 7, `curl: (7) CONNECT tunnel failed, response 0`. Nothing is sent after the CONNECT.

**Actual (Curl)**

- exit 0. It sends `GET / HTTP/1.1` through the "tunnel" and prints `hello`.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\HttpProxyTunnel.cs:435-446`. `ParseStatusCode` accepts any `HTTP/` prefix.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr080Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 9): Measured with `Record-CurlExchange.ps1 -Response '<status line>\r\n\r\n','HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nhello\n' -CurlArgs '-sS','-p','-x','http://127.0.0.1:<port>','http://example.test/'`, first real curl 8.22.0 (Schannel), then Curl (`-Curl Curl.Console\bin\Debug\net10.0\curl.exe`). For the status lines `HTTP/2 200`, `HTTP/3 200`, `HTTP/2.0 200`, `HTTP/1.2 200 OK`, `HTTP/1. 200 OK`, `HTTP/1.10 200 OK`, `HTTP/1.x 200 OK` and `HTTP/11 200`, real curl exits 7 with `curl: (7) CONNECT tunnel failed, response 0` and empty stdout, and sends only the CONNECT (114 bytes). `HTTP/1.0 200 OK` and `HTTP/1.1 200 OK` open the tunnel: curl sends the GET, and the recorder (one reply per connection) closes the connection, so both builds exit 56 with `Recv failure: Connection was aborted`. With the fix, Curl matches real curl in all ten cases: same exit code, stderr, stdout and request bytes (compared by hash). Neither writes any files.
- Fix: `HttpProxyTunnel.ParseStatusCode` now reads a status only after exactly `HTTP/1.0` or `HTTP/1.1`. The check is in its own method, `IsHttp10OrHttp11`, so the method's complexity stays at 10 or below. Any other `HTTP/` version is status 0, which already gives curl's exit 7 `response 0`. The `HTTP/` prefix check on the first line (exit 43) is unchanged, because curl exits 7, not 43, for these lines. No ADR: the fix matches measured curl and makes no design choice.
- Focused test `Cgr080Tests.ReadReplyAsync_StatusLineNotHttp10OrHttp11_IsStatusZero`. The old `StartsWith("HTTP/")` check read status 200 for its first seven rows, so those rows fail without the fix and pass with it. The HTTP/1.0 and HTTP/1.1 rows (the variants that already matched) still give 200.
- Platform: the test runs on in-memory bytes (`ScriptedConnection`) with no path, OS text or certificate, so its result is the same on every platform, and the issue says Linux behaves the same. The fast tests are all green. Measure-CodeQuality for Curl.Networking.UnitLibrary: 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-26: Created from CGR-080 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A CONNECT reply whose status line is not HTTP/1.0 or HTTP/1.1 is response 0, exit 7, as curl
