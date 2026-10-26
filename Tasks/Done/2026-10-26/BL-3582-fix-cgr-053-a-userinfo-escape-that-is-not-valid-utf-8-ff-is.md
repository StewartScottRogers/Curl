---
id: BL-3582
title: Fix CGR-053: A userinfo escape that is not valid UTF-8 (%FF) is sent as the literal text %FF
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitTests/Cgr053Tests.cs, Curl.Authentication.UnitLibrary/CredentialEncoding.cs, Curl.Authentication.UnitLibrary/InvalidUtf8ByteEscapingEncoding.cs, Curl.Authentication.UnitTests/InvalidUtf8ByteEscapingEncodingTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3582 — Fix CGR-053: A userinfo escape that is not valid UTF-8 (%FF) is sent as the literal text %FF

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-053 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-053: A userinfo escape that is not valid UTF-8 (`%FF`) is sent as the literal text `%FF`

- **Severity:** Medium
- **Area:** url-parsing
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `url-parsing UP-04`

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- `curl -sS "http://user:pa%FFss@127.0.0.1:20200/a"` (v04_user_pct_ff; Linux lv04_user_ff)

Commands (run each with REF and with PORT):

```sh
curl -sS "http://user:pa%FFss@127.0.0.1:20200/a"
```

**Expected (real curl)**

- `Authorization: Basic dXNlcjpwYf9zcw==`, which is `user:pa\xFFss`.

**Actual (Curl)**

- `Authorization: Basic dXNlcjpwYSVGRnNz`, which is `user:pa%FFss`.

**Suspected location in source**

- `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:347`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr053Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 (lane 6): Measured with `Record-CurlExchange.ps1` (reply `200 OK`, `ok`): `curl -sS "http://user:pa%FFss@127.0.0.1:20582/a"` - curl 8.22.0 Schannel: exit 0, stdout `ok`, stderr empty, request `GET /a HTTP/1.1`, `Host`, `Authorization: Basic dXNlcjpwYf9zcw==`, `User-Agent: curl/8.22.0`, `Accept: */*`. Curl at 0de3dc19 on Windows: the same bytes (port normalised). BL-3581's CGR-052 fix (ADR-0821) had already fixed Windows, as Windows-1252 round-trips the byte FF. Off Windows the credential encoding was `Encoding.UTF8`, so FF read back as U+FFFD and went out as EF BF BD.
- Fix (ADR-0822): `CredentialEncoding.ForPlatform(false)` is now the new internal `InvalidUtf8ByteEscapingEncoding` (UTF-8 with surrogate escapes, code page 65001; a copy of `Curl.Cli`'s `InvalidUtf8ByteEscape` scheme, because `Curl.Cli` does not reference `Curl.Authentication` and BL-3850's files use it), so the lookup's decoded bytes reach the wire as they are. Touches widened to `Curl.Authentication.UnitLibrary/CredentialEncoding.cs`, the new encoding and its tests: the fix belongs where the credential encoding is chosen, and no live task on origin/work/dark-factory names those files. `TransferCredentialLookup.cs` itself needed no change. BL-3858 (`-J` off Windows) now reads invalid bytes as surrogate escapes from `ForPlatform` instead of U+FFFD.
- Focused test `Cgr053Tests.TryLookUp_InvalidUtf8EscapeOffWindows_SendsTheByte` failed with `ForPlatform(false)` set back to `Encoding.UTF8` and passes with the fix. Tests are platform-neutral (they pick the encoding explicitly), so CI on Linux and macOS is covered.
- Build clean; fast tests: 43738 passed across 46 assemblies, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-053 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A URL credential escape that is not valid UTF-8 (%FF) is sent as its byte on Windows and off it (CGR-053)
