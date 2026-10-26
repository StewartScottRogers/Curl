---
id: BL-3548
title: Fix CGR-019: IDN hosts get no UTS#46 mapping, so a different punycode host is contacted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMapping.cs, Curl.Protocol.Abstractions.UnitTests/Cgr019Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3548 — Fix CGR-019: IDN hosts get no UTS#46 mapping, so a different punycode host is contacted

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-019 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-019: IDN hosts get no UTS#46 mapping, so a different punycode host is contacted

- **Severity:** High
- **Area:** url-parsing
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed. The Windows cases ran against REF 8.22, and the Linux results agree with them.
- **Related existing finding (GF/AF):** GF-0028
- **Source findings:** `url-parsing UP-02`

Upper-case non-ASCII letters are not lower-cased. Full-width letters, ligatures, soft hyphens, ZWJ and combining marks are not mapped either.

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- *, Windows:* 
  - `curl -sS "http://BÜCHER.example:20200/a" --connect-to ::127.0.0.1:20200` (v02_idn_upper_raw)
  - `curl -sS "http://École.example:20200/a" --connect-to ::127.0.0.1:20200` (v02b_idn_E_raw)
- *, Linux:* the same command with each of these hosts: ΣΣ, İstanbul, Ａb (full-width A), ﬁle, a+U+00AD+b, a+U+200D+b, e+U+0301+cole. These are cases lv02_idn_* in `E\wsl\vcases.py`.

Commands (run each with REF and with PORT):

```sh
curl -sS "http://BÜCHER.example:20200/a" --connect-to ::127.0.0.1:20200
curl -sS "http://École.example:20200/a" --connect-to ::127.0.0.1:20200
```

**Expected (real curl)**

- *REF behaviour (Host header):* 

  | Input host | REF sends |
  |---|---|
  | BÜCHER | `xn--bcher-kva` |
  | École | `xn--cole-9oa` |
  | ΣΣ | `xn--4xaa` |
  | İstanbul | `xn--istanbul-o0e` |
  | Ａb | `ab` |
  | ﬁle | `file` |
  | a+soft hyphen+b, a+ZWJ+b | `ab` |
  | e+combining acute+cole | `xn--cole-9oa` |

**Actual (Curl)**

- sends `xn--bcher-2pa`, `xn--cole-pka`, `xn--7waa`, `xn--stanbul-ifb`, `xn--b-4o0i`, `xn--le-1b1n`, `xn--ab-5da`, `xn--ab-m1t` and `xn--ecole-6ed`, all with exit 0. The request goes to a different host name from the one curl uses.

**Suspected location in source**

- `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs:108` and `:179` (`ToPunycode` calls `Idn.GetAscii` on the unmapped name).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Cause: `Curl.Console` runs with `InvariantGlobalization`, where `IdnMapping.GetAscii` maps nothing (probed: `BÜCHER` gave `xn--bcher-2pa` invariant, `xn--bcher-kva` with globalization on). Fixed by mapping by hand in the new `CurlUrlHostNameMapping.cs` (added to touches: a new file, named by no task in Doing on origin/work/dark-factory) before punycode; ADR-0816.
- Measured, Windows, `Record-CurlExchange.ps1 -Port 20391 -CurlArgs -sS "http://<host>.example:20391/a" --connect-to ::127.0.0.1:20391`, real curl 8.22.0 (libidn2) and Curl (`-Curl Curl.Console\bin\Debug\net10.0\curl.exe`) after the fix: `BÜCHER` both exit 0, `Host: xn--bcher-kva.example:20391`, identical request.bin (SHA-256 B4C762A3C146...), empty stdout and stderr; `École` both exit 0, `xn--cole-9oa`, identical request.bin (6C6498F91C26...); `straße` (variant that already matched) both exit 0, `xn--strae-oqa`, identical (A1D19F3B442E...).
- The Linux-only hosts (ΣΣ, İstanbul, Ａb, ﬁle, soft hyphen, ZWJ, e+U+0301) cannot reach Windows curl's ANSI argv (PowerShell best-fits them: ΣΣ became `SS`), so they are pinned against the report's Linux REF results in `Cgr019Tests.TryNormalize_HostThatIsNotAscii_ConvertsTheMappedName`; `Map_Character_MapsAsUts46` pins the mapping itself and fails before the fix (the type did not exist; the unit tests run with globalization on, where ICU already mapped, so only the mapping test sees the invariant-mode defect). Tests are platform-neutral (no paths, OS text or certificates); CI on Linux and macOS is left to the shift's CI watch.
- Choice: non-transitional mapping (`ß`, `ς` kept), as measured for `straße`. The rest of the UTS #46 table (`ſ`, circled digits, other scripts) is filed as BL-3827.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. Build clean; fast tests green (45 assemblies passed, 0 failed).
- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr019Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-019 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Host names are mapped as UTS #46 before punycode; CGR-019's Windows repros send curl's bytes, the Linux hosts are pinned in Cgr019Tests; rest of the table is BL-3827
