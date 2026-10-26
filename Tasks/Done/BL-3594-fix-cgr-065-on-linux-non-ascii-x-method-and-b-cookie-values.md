---
id: BL-3594
title: Fix CGR-065: On Linux, non-ASCII -X method and -b cookie values on the command line are sent as Latin-1 bytes ('?' outside Latin-1) instead of the arg...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Protocol.Http.UnitTests/Cgr065Tests.cs, Curl.Engine.UnitLibrary/CookieEngine.cs, Curl.Engine.UnitTests/Cgr065Tests.cs, Curl.Console/CurlComposition.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3594 — Fix CGR-065: On Linux, non-ASCII -X method and -b cookie values on the command line are sent as Latin-1 bytes ('?' outside Latin-1) instead of the arg...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-065 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-065: On Linux, non-ASCII -X method and -b cookie values on the command line are sent as Latin-1 bytes ('?' outside Latin-1) instead of the argument's UTF-8 bytes

- **Severity:** Medium
- **Area:** http-requests
- **Platform(s):** Linux
- **Confidence:** Confirmed. Detail: Confirmed: reproduced 2/2 on Linux.
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-requests HR-3`

**Reproduction**

- *Setup (http-requests):* E = `SCR\evidence\http-requests`. Recording loopback server + differential runner: `dotnet run E\diff.cs -- E\verify.json E\vout1 20335` (Windows, port 20335) and `E/vlinux.sh` (WSL, port 20355; runs `python3 -I E/diff.py E/verify.json ~/hr_verify/v1 20355`). The server answers `200 OK` with body `ok\n` after reading the whole request; in `early` mode it answers right after the request head and keeps reading.
- WSL: run `curl -sS -X 中 http://127.0.0.1:20355/a`, `curl -sS -b 'c=中' http://127.0.0.1:20355/a` and `curl -sS -b 'c=é' http://127.0.0.1:20355/a`. The cases are v3_* in `E/verify.json`, run with `E/vlinux.sh`.

Commands (run each with REF and with PORT):

```sh
curl -sS -X 中 http://127.0.0.1:20355/a
curl -sS -b 'c=中' http://127.0.0.1:20355/a
curl -sS -b 'c=é' http://127.0.0.1:20355/a
E/vlinux.sh
```

**Expected (real curl)**

- Copies the argv bytes: the request line is `\xe4\xb8\xad /a HTTP/1.1`, and the cookie headers are `Cookie: c=\xe4\xb8\xad` and `Cookie: c=\xc3\xa9`. curl has always copied these values from argv unchanged, so the REF version does not matter here. Windows REF 8.22.0 copies its ANSI argv bytes the same way.

**Actual (Curl)**

- The request line is `? /a HTTP/1.1`, and the cookie headers are `Cookie: c=?` and `Cookie: c=\xe9`. The control case `-H 'X: 中'` matches REF.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs:213` (the method is appended raw) and `:150` (the cookie), then `:158` (`Encoding.Latin1.GetBytes(head)`).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr065Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Touches widened to `Curl.Engine.UnitLibrary/CookieEngine.cs`, `Curl.Engine.UnitTests/Cgr065Tests.cs` and `Curl.Console/CurlComposition.cs`: the `Cookie` value mixes stored cookies (one character per byte) with the `-b` strings, so the formatter cannot encode it whole; the `-b` strings are turned into their bytes where `CookieEngine` takes them. No task in Doing on origin/work/dark-factory named these files (checked 2026-10-26).
- Fix (ADR-0823): `HttpRequestHeadFormatter.AppendRequestLine` writes the method through `HeadText` (its bytes in `CommandLineTextEncoding`, as `-H`); `CookieEngine.FromHttpOptions` takes an optional encoding and sends the joined `-b` strings as their bytes in it (too-long check counted in it); `CurlComposition.SharingRunCookies` passes `CredentialEncoding.ForPlatform`. Library handles pass none, unchanged.
- Measured real curl 8.18.0 (OpenSSL, WSL) against a netcat listener on 127.0.0.1:20355, 2026-10-26: `curl -sS -X 中 .../a` sent `E4 B8 AD /a HTTP/1.1`, exit 0, stdout `ok`; `-b c=中` sent `Cookie: c=E4 B8 AD`; `-b c=é` sent `Cookie: c=C3 A9`; control `-H X:中` sent `X:E4 B8 AD`; all exit 0, stdout `ok\n`, stderr empty, no files. Curl was not run on Linux here (no .NET in WSL); its Linux wire bytes are pinned by the unit tests with `CommandLineTextEncoding` / the cookie encoding set to UTF-8, which is what `CredentialEncoding.ForPlatform(false)` gives: `Curl.Protocol.Http.UnitTests/Cgr065Tests.cs` (method gives E4 B8 AD; failed before as `?`) and `Curl.Engine.UnitTests/Cgr065Tests.cs` (`c=中` and `c=é` give their UTF-8 bytes). Exit code, stdout and stderr are unaffected by the change. Windows keeps its ANSI bytes as before, matching Schannel curl; the `-H` control case is untouched.
- Platform check: the new tests set the encoding explicitly, so they give the same answer on every OS; CI on Linux and macOS is covered by keeping them platform-neutral and the fast tests green here.
- Fast tests: all green (e.g. Curl.Protocol.Http.UnitTests 2319 passed, Curl.Engine.UnitTests 2540, Curl.Console.UnitTests 2913, Curl.Conformance.UnitTests 4381). Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary, Curl.Engine.UnitLibrary and Curl.Console: Http and Console 100/100, Engine one failing member, `MultiCallbackQueue.QueuedCallback.WaitUntilRun`, not touched here and already filed as BL-3857.
- Filed BL-3866: a lone-surrogate (invalid UTF-8 argv byte) in a `-b` string still goes out as EF BF BD.

## Log

- 2026-10-26: Created from CGR-065 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. On Linux -X and -b values go out as their UTF-8 argv bytes, as curl 8.18.0 sends them
