---
id: BL-3588
title: Fix CGR-059: IDN host validity differs between REF and PORT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-11-02
---
# BL-3588 — Fix CGR-059: IDN host validity differs between REF and PORT

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-059 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-059: IDN host validity differs between REF and PORT

- **Severity:** Medium
- **Area:** url-parsing
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed for the Windows `a%FFb` case against 8.22. The Linux sub-cases reproduce, but they were compared only against REF-LINUX 8.18, whose libidn2 behaviour may differ from the 8.22 target, so those parts are Probable.
- **Related existing finding (GF/AF):** GF-0028
- **Source findings:** `url-parsing UP-14`

On Windows, PORT rejects `a%FFb`, which curl transfers. On Linux, PORT accepts a leading-hyphen IDN label and rejects an empty label or an RTL label.

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- *, Windows:* `curl -sS "http://a%FFb:20200/a" --connect-to ::127.0.0.1:20200` (v14_host_pctff)
- *, Linux:* the same form with these hosts: `-ü.example`, `ü..example`, `aא.example` (lv02_idn_leading_hyphen_idn, trailing_dot_idn, bidi).

Commands (run each with REF and with PORT):

```sh
curl -sS "http://a%FFb:20200/a" --connect-to ::127.0.0.1:20200
```

**Expected (real curl)**

- Windows `a%FFb`: exit 0, `Host: xn--ab-6ka`.
- Linux `-ü`: exit 3, `URL using bad/illegal format or missing URL`.
- Linux `ü..`: exit 0, `Host: xn--tda..example`.
- Linux `aא`: exit 0, `Host: xn--a-0hc.example`.

**Actual (Curl)**

- `a%FFb`: exit 3, `URL rejected: Bad hostname`.
- `-ü`: exit 0, `Host: xn----eha.example`.
- `ü..` and `aא`: exit 3, `Bad hostname`.

**Suspected location in source**

- `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs:100-118` and `:177-186`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr059Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-11-02 (lane 6): the work landed through its split tasks BL-3884, BL-3885 and BL-3886 (all Done 2026-10-28), so this claim changed no code. The stash 5328375b is the work BL-3884 carried over and was not reapplied.
- Windows repro re-measured at f4aaed5c with Record-CurlExchange.ps1 -Port 20200 -Response 'HTTP/1.1 200 OK
Content-Length: 3

ok
' -CurlArgs -sS,http://a%FFb:20200/a,--connect-to,::127.0.0.1:20200: real curl 8.22.0 and Curl both exit 0, stdout `ok
`, empty stderr, request `GET /a HTTP/1.1` / `Host: xn--ab-6ka:20200` / `User-Agent: curl/8.22.0` / `Accept: */*`, byte for byte identical.
- Linux cases (-ü.example exit 3; ü..example as xn--tda..example; aא.example as xn--a-0hc.example) were measured against Linux curl and pinned by Cgr059LabelRulesTests and Cgr059BidiTests (see BL-3885 and BL-3886). Their platform-neutral pure-code tests pass here.
- Criterion 2: the focused tests are Cgr059PercentEncodedHostTests, Cgr059LabelRulesTests and Cgr059BidiTests, from the split tasks. Cgr059Tests.cs was not created because those tests already cover the issue.
- Curl.Protocol.Abstractions.UnitTests fast tests: 1630 passed, 2 skipped. No production code changed, so no quality run was needed.

## Log

- 2026-10-26: Created from CGR-059 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Dark factory stopped at its 80-turn cap, so the task is too big for one run: split it in the first turns of the next claim; see Z:\repos\Curl.logs\BL-3588-20261026-063535-L2.jsonl
- 2026-10-26: Backlog -> Deferred. Split into BL-3884, BL-3885, BL-3886 after exceeding its cost cap
- 2026-11-02: Deferred -> Backlog. Stewart 2026-11-02: complete all existing tasks before the third-party handoff.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-11-02: Doing -> Done. Every CGR-059 repro matches curl through BL-3884, BL-3885 and BL-3886; a%FFb re-measured byte-identical on Windows
