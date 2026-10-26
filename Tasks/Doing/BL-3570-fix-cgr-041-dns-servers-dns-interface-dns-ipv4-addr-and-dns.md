---
id: BL-3570
title: Fix CGR-041: --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr are accepted and used; both non-c-ares reference builds refuse them w...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/Cgr041Tests.cs, Curl.Networking.UnitLibrary/DnsServerList.cs, Curl.Networking.UnitTests/Cgr041Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3570 — Fix CGR-041: --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr are accepted and used; both non-c-ares reference builds refuse them w...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-041 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-041: `--dns-servers`, `--dns-interface`, `--dns-ipv4-addr` and `--dns-ipv6-addr` are accepted and used; both non-c-ares reference builds refuse them with exit 2

- **Severity:** Medium
- **Area:** cli-options, proxy-connect
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [proxy-connect PC-17] Probable. The difference reproduces, but it depends on the build: a c-ares curl accepts these options.
- **Related existing finding (GF/AF):** none
- **Source findings:** `cli-options CLIO-06`, `proxy-connect PC-17`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- **[cli-options CLIO-06]** --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr are accepted; REF says they are not supported
  - `curl --dns-servers 127.0.0.1 -s @U`; `curl --dns-interface lo -s @U`. Scripts: `verify/v1_win.sh`, `verify/v2_linux.sh`.
- **[proxy-connect PC-17]** --dns-ipv4-addr, --dns-ipv6-addr, --dns-interface and --dns-servers are accepted; both non-c-ares reference builds reject them with exit 2
  - run `srv 20600 log 'H;OK'`, then `curl -sS --dns-ipv4-addr 127.0.0.1 http://127.0.0.1:20600/x`. Verifier cases: v24 and VL19.

Commands (run each with REF and with PORT):

```sh
# [cli-options CLIO-06]
curl --dns-servers 127.0.0.1 -s @U
curl --dns-interface lo -s @U
# [proxy-connect PC-17]
srv 20600 log 'H;OK'
curl -sS --dns-ipv4-addr 127.0.0.1 http://127.0.0.1:20600/x
```

**Expected (real curl)**

- **[cli-options CLIO-06]** *REF (8.22 on Windows and 8.18 on Linux):* exit 2, `curl: option --dns-servers: the installed libcurl version does not support this`.
- **[proxy-connect PC-17]** exit 2, `curl: option --dns-ipv4-addr: the installed libcurl version does not support this`, then the "try curl --help" line.

**Actual (Curl)**

- **[cli-options CLIO-06]** exit 0 and the transfer runs. -V does not claim c-ares.
- **[proxy-connect PC-17]** exit 0 and the transfer runs.

**Suspected location in source**

- **[cli-options CLIO-06]** `CommandLineOptionTable.cs` (dns-* rows); `Curl.Networking.UnitLibrary/DnsServerList.cs`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr041Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-041 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
