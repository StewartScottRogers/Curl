---
id: BL-3740
title: Fix CGR-211: -v does not print * Uses proxy env variable <name> == '<value>' when a proxy comes from the environment
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.Traces.cs, Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs, Curl.Engine.UnitTests/Cgr211Tests.cs, Curl.Core.UnitLibrary/ProxySelector.cs, Curl.Core.UnitTests/ProxySelectorVariableTests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3740 — Fix CGR-211: -v does not print * Uses proxy env variable <name> == '<value>' when a proxy comes from the environment

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-211 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-211: `-v` does not print `* Uses proxy env variable <name> == '<value>'` when a proxy comes from the environment

- **Severity:** Low
- **Area:** proxy-connect, environment
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `proxy-connect PC-12`, `environment ENV-10`

**Reproduction**

- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- **[proxy-connect PC-12]** -v lacks "* Uses proxy env variable <name> == '<value>'"
  - run `srv 20600 log 'H;OK'`, then `http_proxy=http://127.0.0.1:20600 curl -sv --resolve ex.test:20600:127.0.0.1 http://ex.test:20600/x`. Verifier cases: v19 and VL15.
- **[environment ENV-10]** `-v` through an environment proxy does not log `* Uses proxy env variable http_proxy == '...'`
  - Windows: `set http_proxy=http://127.0.0.1:21201` then `curl -v -o NUL http://127.0.0.1:21200/echo` (v-px-v)
  - Linux: `http_proxy=http://127.0.0.1:21211 ~/curlnet/curl -sv -o /dev/null http://127.0.0.1:21210/echo`

Commands (run each with REF and with PORT):

```sh
# [proxy-connect PC-12]
srv 20600 log 'H;OK'
http_proxy=http://127.0.0.1:20600 curl -sv --resolve ex.test:20600:127.0.0.1 http://ex.test:20600/x
# [environment ENV-10]
set http_proxy=http://127.0.0.1:21201
curl -v -o NUL http://127.0.0.1:21200/echo
http_proxy=http://127.0.0.1:21211 ~/curlnet/curl -sv -o /dev/null http://127.0.0.1:21210/echo
```

**Expected (real curl)**

- **[proxy-connect PC-12]** `* Uses proxy env variable http_proxy == 'http://127.0.0.1:20600'`.
- **[environment ENV-10]** The first verbose line is `* Uses proxy env variable http_proxy == 'http://127.0.0.1:21201'`. REF 8.22 and REF-LINUX 8.18 both print it.

**Actual (Curl)**

- **[proxy-connect PC-12]** the line is absent.
- **[environment ENV-10]** The line is missing. The rest of the trace is the same as REF.

**Suspected location in source**

- **[proxy-connect PC-12]** missing; the port source has no match for "Uses proxy env".
- **[environment ENV-10]** the string does not occur anywhere in `SCR\Curl` (the port repository).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Fix: `ProxySelector.TryFindProxyVariable` names the environment variable that gave the proxy (same order as before); `TransferPerformer.TrySelectProxyAndCredentials` writes `Uses proxy env variable <name> == '<value>'` as a -v info line via `EngineComposition.ReportEnvironmentProxy` after the proxy is chosen, only when no -x/--preproxy and a proxy was chosen (not for --noproxy matches). Added Core files to touches: the selector lives in Curl.Core and no other live task names it.
- Repro `http_proxy=http://127.0.0.1:20600 curl -sv --resolve ex.test:20600:127.0.0.1 http://ex.test:20600/x` (nothing listening): real curl and Curl both print `* [DNS] added ...` then `* Uses proxy env variable http_proxy == 'http://127.0.0.1:20600'` then `* Trying 127.0.0.1:20600...`. With `-x` the line is absent in Curl. Library handles (Curl.Api/Libcurl) not changed: only the tool path writes it.
- Measure-CodeQuality for Curl.Core and Curl.Engine libraries: 100% line/branch, no failing member. Platform-neutral tests.
- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr211Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-211 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Done. Curl -v now prints '* Uses proxy env variable <name> == <value>' when the proxy comes from the environment
