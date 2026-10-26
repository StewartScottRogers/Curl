---
id: BL-3608
title: Fix CGR-079: Proxy credentials in the proxy URL lose to -U/--proxy-user; curl lets the URL's user info win
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs, Curl.Engine.UnitTests/Cgr079Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3608 — Fix CGR-079: Proxy credentials in the proxy URL lose to -U/--proxy-user; curl lets the URL's user info win

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-079 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-079: Proxy credentials in the proxy URL lose to -U/--proxy-user; curl lets the URL's user info win

- **Severity:** Medium
- **Area:** proxy-connect
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed. Reproduced by the tester and again by the verifier, on both platforms.
- **Related existing finding (GF/AF):** none
- **Source findings:** `proxy-connect PC-01`

**Reproduction**

- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- run `srv 20600 log 'H;OK'`, then `curl -s -x http://a:b@127.0.0.1:20600 -U c:d http://example.test/`.
  - Variants: `http_proxy=http://a:b@127.0.0.1:20600 curl -s -U c:d http://example.test/`, and `-x http://a@127.0.0.1:20600 -U c:d`.
  - SOCKS5 variant: `-x socks5://a:b@127.0.0.1:20600 -U c:d http://127.0.0.1:8080/x` with server script `S5G;S5UP;S5U;S5AOK;S5R;S5ROK;H;OK`.
  - SOCKS4 variant: `-x socks4://a@127.0.0.1:20600 -U c:d http://127.0.0.1:8080/x` with script `S4;S4OK;H;OK`.
  - Verifier cases: v01-v05 and VL01-VL04.

Commands (run each with REF and with PORT):

```sh
srv 20600 log 'H;OK'
curl -s -x http://a:b@127.0.0.1:20600 -U c:d http://example.test/
http_proxy=http://a:b@127.0.0.1:20600 curl -s -U c:d http://example.test/
```

**Expected (real curl)**

- *REF (Windows 8.22.0 and Linux 8.18.0):* sends `Proxy-Authorization: Basic YTpi` (a:b). With a user-only URL it sends `YTo=` (a:). SOCKS5 sends `\x01\x01a\x01b` and the SOCKS4 USERID is `a`.

**Actual (Curl)**

- sends `Proxy-Authorization: Basic Yzpk` (c:d) in every variant. SOCKS5 sends `\x01\x01c\x01d` and the SOCKS4 USERID is `c`.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedEasyTransfer.Context.cs:222-237` (`ProxyCredentialsOf`). Its doc comment says -U and --proxy-user "replace the proxy URL's own user information" (BL-2660, ADR-0677), which does not match curl 8.22.0 CLI behaviour.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr079Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-079 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
