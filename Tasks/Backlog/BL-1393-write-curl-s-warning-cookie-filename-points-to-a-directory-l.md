---
id: BL-1393
title: Write curl's 'WARNING: cookie filename points to a directory' line for a -b directory off Windows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: FR-099
created: 2026-10-03
completed:
---
# BL-1393 — Write curl's 'WARNING: cookie filename points to a directory' line for a -b directory off Windows

## Goal

Off Windows, a `-b <path>` that names a directory writes curl 8.21.0's `* WARNING: cookie filename points to a directory: "<path>"` info line, as the Linux and macOS builds do; on Windows it keeps today's `* WARNING: failed to open cookie file "<path>"`.

## Context

- Today `Curl.Cookies.UnitLibrary/CookieStore.cs` `LoadCookieFileAsync` (around line 262) writes `WARNING: failed to open cookie file "<path>"` for every open that is not `FileAccessStatus.Ok`, "a directory included" as its doc comment says (BL-487). That is the Windows build's answer only.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/cookie.c` lines 1146-1156: `fp = curlx_fopen(file, "rb"); if(!fp) infof(data, "WARNING: failed to open cookie file \"%s\"", file); else { ... if((curlx_fstat(fileno(fp), &stat) != -1) && S_ISDIR(stat.st_mode)) { curlx_fclose(fp); fp = NULL; infof(data, "WARNING: cookie filename points to a directory: \"%s\"", file); } ...`. On Linux and macOS `fopen(dir, "rb")` succeeds, so a directory reaches the second line; on Windows the open itself fails, so it gets the first.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel): `-sv -b <existing directory> http://127.0.0.1:PORT/a` writes `* WARNING: failed to open cookie file "<path>"` and carries on with the transfer, exit 0. The non-Windows line is cited from the source above (the standing rule: the OpenSSL build on Linux and macOS).
- The fake and physical file systems already report a directory as `FileAccessStatus.IsDirectory` (`Curl.Protocol.Abstractions.UnitLibrary/FileAccessStatus.cs`, `Curl.Core.UnitLibrary/FileSystem/FileOpenFailure.cs`). Decide the platform with `OperatingSystem.IsWindows()` or an injected flag, whichever keeps the tests able to pin both answers on any machine.

## Acceptance criteria

- [ ] A test in `Curl.Cookies.UnitTests` marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` (or one that injects the non-Windows choice) loads a path the fake file system reports as `IsDirectory` and asserts the single info line `WARNING: cookie filename points to a directory: "<path>"` with the path as given, and that nothing is loaded.
- [ ] A test marked `[OSCondition(OperatingSystems.Windows)]` (or injecting the Windows choice) pins `WARNING: failed to open cookie file "<path>"` for the same directory; `NotFound` and `AccessDenied` keep that line on every platform.
- [ ] `LoadCookieFileAsync`'s doc comment says which line each platform writes.
- [ ] `dotnet build Curl.Cookies.UnitTests -warnaserror` is clean; `dotnet test Curl.Cookies.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cookies.UnitLibrary` reports no failing member in the code this task changed.

## Notes

- Test paths are drive-less (`/dir/cookies`) so they pass on all three platforms.

## Log

- 2026-10-03: Created.
