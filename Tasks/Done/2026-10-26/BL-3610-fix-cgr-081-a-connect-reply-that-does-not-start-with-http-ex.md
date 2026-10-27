---
id: BL-3610
title: Fix CGR-081: A CONNECT reply that does not start with "HTTP/" exits 43 "Invalid response header"; curl exits 7
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HttpProxyTunnel.cs, Curl.Networking.UnitTests/Cgr081Tests.cs, Curl.Networking.UnitTests/HttpProxyTunnelTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3610 — Fix CGR-081: A CONNECT reply that does not start with "HTTP/" exits 43 "Invalid response header"; curl exits 7

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-081 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-081: A CONNECT reply that does not start with "HTTP/" exits 43 "Invalid response header"; curl exits 7

- **Severity:** Medium
- **Area:** proxy-connect
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `proxy-connect PC-03`

**Reproduction**

- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- run `srv 20600 log 'HN;W:garbage\r\n\r\n'` (or `ICY 200 OK`), then `curl -sS -p -x http://127.0.0.1:20600 http://example.test/`. Verifier cases: v08, v08b and VL06.

Commands (run each with REF and with PORT):

```sh
srv 20600 log 'HN;W:garbage\r\n\r\n'
curl -sS -p -x http://127.0.0.1:20600 http://example.test/
```

**Expected (real curl)**

- *REF (8.22.0 Windows and 8.18.0 Linux):* exit 7, `curl: (7) CONNECT tunnel failed, response 0`.

**Actual (Curl)**

- exit 43, `curl: (43) Invalid response header`.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\HttpProxyTunnel.cs:220-223, 234` (`FailureExitCode = BadFunctionArgument`, BL-1857; the source says this was measured against 8.21.0).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr081Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 5): Measured with `Record-CurlExchange.ps1`, `curl -sS -p -x http://127.0.0.1:P http://example.test/`, real curl 8.22.0 Schannel vs Curl after the fix; exit code, stderr, stdout (empty) and request.bin byte-identical for every reply:
  - `garbage\r\n\r\n` (the repro), `ICY 200 OK\r\n\r\n` (the repro's variant), `garbage\n\n`, `<html>\r\n\r\n`, `garbage\r\nFoo: bar\r\n\r\n`, `garbage\r\n Foo\r\n\r\n`: exit 7 `curl: (7) CONNECT tunnel failed, response 0` (Curl before: exit 43 `Invalid response header`).
  - `garbage\r\nFoo\r\n\r\n`, `HTTP/1.1 200 OK\r\nFoo\r\n\r\n`, upstream test750's `<html>\n<h1>400 Bad request</h1>\n</html>\n` (already matched, still matches): exit 43 `curl: (43) Invalid response header`.
  - `garbage\r\n` then close: exit 56 `curl: (56) Proxy CONNECT aborted`.
  Rule: the first line is never refused (not HTTP/1.x is status 0, CGR-080); a later line that ends holding no colon and not starting with a space or tab is exit 43. Recorded in ADR-0826. Linux: the change is platform-neutral pure parsing (the report found 8.18.0 OpenSSL answering the same); tests use only in-memory bytes.
- The fix replaced `HttpProxyTunnelTests.ReadReplyAsync_WhenOnlyTheFirstLineIsNotHttp_FailsAtOnceWithInvalidResponseHeader` (it pinned BL-1857's unmeasured first-line rule; its rows moved to `Cgr081Tests` with the measured answers), renamed the test750 test to say it fails on the second line, and dropped a colon-less `no colon` field from a body-length data row; so `HttpProxyTunnelTests.cs` was added to touches (no other task in Doing names it).
- Failing before the fix: `Cgr081Tests.ReadReplyAsync_FirstLineNotHttp_IsStatusZeroNotAFailure` rows are exactly the replies the old first-line branch failed with 43 (and the removed old test pinned that 43 for the same inputs, which is why it failed after the fix). Reverting the file to rerun was refused by the permission hook, so this is shown by the old test's opposite assertion.
- Gates: `dotnet build` clean; fast tests all green (Curl.Networking.UnitTests 3567 passed); `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-081 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A CONNECT reply whose first line is not HTTP/ is exit 7 'CONNECT tunnel failed, response 0' and only a later colon-less line is exit 43, as curl 8.22.0
