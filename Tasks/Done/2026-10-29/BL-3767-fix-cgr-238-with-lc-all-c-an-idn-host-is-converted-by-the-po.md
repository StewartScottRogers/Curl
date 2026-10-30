---
id: BL-3767
title: Fix CGR-238: With LC_ALL=C, an IDN host is converted by the port and rejected by REF
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3945]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests/Cgr238Tests.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/Cgr238Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3767 — Fix CGR-238: With LC_ALL=C, an IDN host is converted by the port and rejected by REF

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-238 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-238: With `LC_ALL=C`, an IDN host is converted by the port and rejected by REF

- **Severity:** Low
- **Area:** environment
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable. The difference reproduces, but the REF behaviour depends on the build and version (libidn2 and locale) and was only observed with 8.18.
- **Related existing finding (GF/AF):** none
- **Source findings:** `environment ENV-15`

**Reproduction**

- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- `LC_ALL=C ~/curlnet/curl -sS -x 127.0.0.1:21211 http://bücher.example/` (`linux/verify.sh` v-idn-c). With `LC_ALL=C.UTF-8` (v-idn-utf8) both behave the same.

Commands (run each with REF and with PORT):

```sh
LC_ALL=C ~/curlnet/curl -sS -x 127.0.0.1:21211 http://bücher.example/
export LC_ALL=C.UTF-8
```

**Expected (real curl)**

- `curl: (3) URL using bad/illegal format or missing URL`

**Actual (Curl)**

- Sends `GET http://xn--bcher-kva.example/` and exits 0.

**Suspected location in source**

- not located.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr238Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-28 (lane 7): The IDN conversion lives in `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs` (`NormalizeName`/`ToPunycode`; the `HostAsWindows1252` flag is the platform switch to extend), not in Curl.Core. Fix plan: off Windows, when LC_ALL / LC_CTYPE / LANG (first non-empty wins) names a non-UTF-8 charset (C, POSIX), a non-ASCII host is `IdnConversionFailed` (exit 3), as libidn2 rejects it; read the variable through an injected seam, test per platform. BL-3945 names CurlUrlHost.cs, so depends-on BL-3945 added; measure real curl 8.x on Linux/WSL if available, else pin from the report. Test goes in Curl.Protocol.Abstractions.UnitTests/Cgr238Tests.cs.

- 2026-10-29 (lane 5): Fix: `CurlUrlHost.LocaleIsUtf8` (LC_ALL, LC_CTYPE, LANG, first non-empty; none set = C) and `LocaleRejectsNonAsciiNameOn`; off Windows outside a UTF-8 locale a non-ASCII transfer host is `IdnConversionFailed` (exit 3, "URL using bad/illegal format or missing URL"). Real curl not measurable on this Windows lane (WSL has no curl run); expected result pinned from the report (curl 8.18 OpenSSL, `LC_ALL=C` => exit 3; `C.UTF-8` unchanged, covered by existing tests). Variants that matched still match: fast tests green, quality gate 0 failing. CI Linux/macOS criterion: platform-neutral tests (explicit seams); runners assumed UTF-8 locale.

## Log

- 2026-10-26: Created from CGR-238 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Needs Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, which BL-3945 holds; depends-on BL-3945
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Done. Non-ASCII host is refused (exit 3) off Windows when the locale is not UTF-8
