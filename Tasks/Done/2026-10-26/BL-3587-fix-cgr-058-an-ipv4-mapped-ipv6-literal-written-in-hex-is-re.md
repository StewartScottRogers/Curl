---
id: BL-3587
title: Fix CGR-058: An IPv4-mapped IPv6 literal written in hex is rewritten to dotted form in Host and url_effective
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/Cgr058Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3587 — Fix CGR-058: An IPv4-mapped IPv6 literal written in hex is rewritten to dotted form in Host and url_effective

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-058 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-058: An IPv4-mapped IPv6 literal written in hex is rewritten to dotted form in Host and url_effective

- **Severity:** Medium
- **Area:** url-parsing
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `url-parsing UP-13`

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- `curl -sS "http://[::ffff:7f00:1]:20200/a" -w "[%{url_effective}]"` (v13_v4mapped; Linux lv13_v4mapped)
- `curl -sS "http://[::FFFF:7F00:1]:20200/a"` (v13b_v4mapped_upper)

Commands (run each with REF and with PORT):

```sh
curl -sS "http://[::ffff:7f00:1]:20200/a" -w "[%{url_effective}]"
curl -sS "http://[::FFFF:7F00:1]:20200/a"
```

**Expected (real curl)**

- `Host: [::ffff:7f00:1]:20200` and `Host: [::FFFF:7F00:1]:20200`. url_effective keeps the address as written.

**Actual (Curl)**

- `Host: [::ffff:127.0.0.1]:20200` in both cases. url_effective changes the same way.

**Suspected location in source**

- `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs:209-211` (`address.ToString()`).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr058Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Cause: `CurlUrlHost.NormalizeIPv6` always printed the address in .NET's shortest form. curl's `ipv6_parse` uses its normalised (`inet_ntop`) form only when it is no longer than the address as written, zone id aside. Measured with curl 8.22.0 (ucrt64) and 8.21.0 (System32) via `-w %{url_effective}`: `[::ffff:7f00:1]`, `[::FFFF:7F00:1]`, `[::FFFF:1]`, `[::0:ffff:1:2]`, `[::ffff:0:1:2]`, `[::ffff:7f00:1%25eth0]` stay as written; `[::ABCD]`->`[::abcd]` and `[::FFFF:102:304]`->`[::ffff:1.2.3.4]` (equal length: normalised), `[::102:304]`->`[::1.2.3.4]`, `[0:0:0:0:0:0:0:1]`->`[::1]`, `[fe80::00ab]`->`[fe80::ab]`. Fix: keep the written address when .NET's form is longer. Matches upstream test 1560's `[::FFFF:7f00:0001]`->`[::ffff:127.0.0.1]` (16 = 16). No design choice was left to make (pure match of measured curl), so no ADR.
- Repro via Record-CurlExchange.ps1 -Port 20200, REF = curl 8.22.0 Schannel, PORT = this build (User-Agent version normalised):
  - `curl -sS "http://[::ffff:7f00:1]:20200/a" -w "[%{url_effective}]"`: both exit 0, stdout `[http://[::ffff:7f00:1]:20200/a]`, stderr empty, request `GET /a HTTP/1.1` / `Host: [::ffff:7f00:1]:20200` / UA / `Accept: */*` - identical.
  - `curl -sS "http://[::FFFF:7F00:1]:20200/a"`: both exit 0, stdout empty, stderr empty, `Host: [::FFFF:7F00:1]:20200` - identical.
- `Cgr058Tests` (11 rows): 6 failed with the fix reverted, all pass with it. Linux/macOS: pure string normalisation, platform-neutral. Measure-CodeQuality for Curl.Protocol.Abstractions.UnitLibrary: 0 failing members. Build clean, fast tests green.
- Pipeline `feature` run as a direct one-line fix: the plan was the report's suspected location, confirmed by measurement.

## Log

- 2026-10-26: Created from CGR-058 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A bracketed IPv6 address keeps its written form in Host and url_effective unless curl's normalised form is no longer (CGR-058)
