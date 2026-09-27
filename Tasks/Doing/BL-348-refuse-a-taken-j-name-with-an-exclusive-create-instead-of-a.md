---
id: BL-348
title: Refuse a taken -J name with an exclusive create instead of a check before the open
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-348 — Refuse a taken -J name with an exclusive create instead of a check before the open

## Goal

A `-J` (`--remote-header-name`) download refuses a name that is already taken through a single exclusive-create open, so a file created by another process between the check and the open can never be overwritten.

## Context

- curl 8.21.0 opens a `-J` (Content-Disposition) output file with `O_EXCL`. A taken name
  gives `Warning: Failed to open the file x.txt: File exists` on standard error, then
  `curl: (23) client returned ERROR on write of 51 bytes`, exit 23
  (`CurlExitCode.WriteError`). Measured while finishing BL-239; the commands and bytes
  are in BL-239's Notes (`Tasks/Done/` or its archive).
- BL-239 approximates this in `Curl.Console/RemoteHeaderNameStream.cs`, `TryOpenAsync`:
  it calls `IOutputPaths.FileExists(path)` and, when false, does an ordinary truncating
  open through `DeferredOutputFileStream`. That is two steps with a race window between
  them, because `IFileSystem` (`Curl.Protocol.Abstractions.UnitLibrary/IFileSystem.cs`,
  `OpenForWriteAsync`) has no exclusive create.
- Contract today: `Curl.Protocol.Abstractions.UnitLibrary/FileWriteMode.cs` has `Truncate`
  and `Append`; `FileAccessStatus.cs` has `Ok`, `NotFound`, `IsDirectory`,
  `AccessDenied`, `IoError`. `Curl.Core.UnitLibrary/FileSystem/PhysicalFileSystem.cs`
  maps the mode to a `FileMode` (around line 115) and
  `Curl.Core.UnitLibrary/FileSystem/FileOpenFailure.cs` maps exceptions to a status.
- Plan: add a create-new write mode (e.g. `FileWriteMode.CreateNew`, mapped to
  `FileMode.CreateNew`) and a distinct status for "the file already exists" (e.g.
  `FileAccessStatus.AlreadyExists`, mapped from the `IOException` `FileMode.CreateNew`
  raises for an existing file - on Windows HResult `0x80070050` ERROR_FILE_EXISTS; on
  POSIX EEXIST). Use it for `-J`: `RemoteHeaderNameStream` opens with the new mode and
  turns the "exists" status into `OutputFileOpenWarning.ForExistingFile(path)` and exit
  23, dropping the separate `FileExists` check. `Curl.Console/OutputFileOpenWarning.cs`
  switches on `FileAccessStatus` and must handle the new value.
- Test doubles that implement `IFileSystem` and may need the new mode:
  `Curl.Console.UnitTests/InMemoryFileSystem.cs` (in touches). Others
  (`Curl.Protocol.File.UnitTests/Fakes/FakeFileSystem.cs`,
  `Curl.Core.UnitTests/Multipart/FormFileSystem.cs`,
  `Curl.Cookies.UnitTests/CookieStoreTests.CookieFiles.cs`) only need to keep compiling;
  if one of them would have to change, stop and widen `touches` via a new task rather
  than editing outside it.
- If `IOutputPaths.FileExists` has no remaining caller afterwards, remove it rather than
  leave it unused.
- Base class library only; no package.

## Acceptance criteria

- [ ] `IFileSystem.OpenForWriteAsync` accepts a create-new mode, documented in XML doc comments, that fails with a distinct "already exists" `FileAccessStatus` when the path exists and never truncates it.
- [ ] `PhysicalFileSystemTests` in `Curl.Core.UnitTests` has a named test showing a create-new open of an existing file returns the "already exists" status and leaves the file's bytes unchanged, and one showing it creates a missing file.
- [ ] A named unit test in `Curl.Console.UnitTests` (e.g. in `RemoteHeaderNameStreamTests`) shows that a file created after the name is chosen but before the open is not overwritten: the run prints `Warning: Failed to open the file <name>: File exists` and `curl: (23) client returned ERROR on write of <n> bytes` and returns `CurlExitCode.WriteError`.
- [ ] `RemoteHeaderNameStream` no longer checks for existence before opening; the refusal comes from the open's status.
- [ ] The existing BL-239 runner tests in `Curl.Console.UnitTests/CurlCommandRunnerRemoteNameTests.cs` still pass unchanged.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage and no failing member for `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Core.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
