---
id: BL-1400
title: Add an IDirectoryLister seam that lists a local directory's entry names, implemented by PhysicalFileSystem
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: FR-004
created: 2026-10-03
completed:
---
# BL-1400 — Add an IDirectoryLister seam that lists a local directory's entry names, implemented by PhysicalFileSystem

## Goal

`Curl.Protocol.Abstractions.UnitLibrary` gains a small interface, `IDirectoryLister`, that gives the entry names of a local directory in the order the operating system returns them, and `Curl.Core.UnitLibrary/FileSystem/PhysicalFileSystem.cs` implements it, so the `file` handler can list a directory as curl's Linux and macOS builds do without touching the disk in tests.

## Context

- curl 8.21.0 (tag `curl-8_21_0`), `lib/file.c` lines 568-589 (`file_do`, `HAVE_OPENDIR`): a `file://` path that is a directory is listed with `opendir`/`readdir`, writing each entry name that does not start with `.` followed by `\n`, in `readdir` order; without `opendir` (the Windows build) it is `Directory listing not yet implemented on this platform.` - and in practice the Windows build never gets there, because opening the directory already fails (measured 2026-10-03 with curl 8.21.0, mingw, Schannel: `curl -sv file:///C:/.../dl/` writes `* Could not open file C:/.../dl/` and exits 37).
- Today `IFileSystem` (`Curl.Protocol.Abstractions.UnitLibrary/IFileSystem.cs`) can only open files, and its remarks say a directory always gives exit 37, which is only the Windows build's answer. Adding a member to `IFileSystem` would break its many test fakes (`Curl.Console.UnitTests/InMemoryFileSystem.cs`, `Curl.Protocol.File.UnitTests/Fakes/FakeFileSystem.cs`, `Curl.Cookies.UnitTests`, `Curl.Core.UnitTests/Multipart/FormFileSystem.cs`, `Curl.Protocol.Ssh.UnitTests/Fakes/InMemoryKeyFileSystem.cs`), so a separate interface is the smaller change: a handler that holds an `IFileSystem` can test it for `IDirectoryLister` (`fileSystem as IDirectoryLister`) and needs no new constructor parameter or composition change.
- Shape: `ValueTask<IReadOnlyList<string>?> ListEntryNamesAsync(string path, CancellationToken cancellationToken)` - the names only (no path), every entry including those starting with `.` (the handler filters, as curl does), `null` when the directory cannot be listed. `PhysicalFileSystem` uses `Directory.EnumerateFileSystemEntries` (BCL only) and `Path.GetFileName` on each, without sorting.

## Acceptance criteria

- [ ] `IDirectoryLister` exists in `Curl.Protocol.Abstractions.UnitLibrary` with the member above and XML doc comments saying what each return means; `IFileSystem`'s remark about directories says the Windows build cannot open a directory (exit 37) while the Linux and macOS builds list it, citing `lib/file.c` lines 568-589.
- [ ] `PhysicalFileSystem` implements it; tests in `Curl.Core.UnitTests` create a temporary directory with `a.txt`, `.hidden` and a subdirectory `sub` and assert the three names come back (in any order, compared as a set), and that a missing directory and a regular file give `null`.
- [ ] Nothing else in the solution changes behaviour: `dotnet build -warnaserror` of `Curl.Protocol.Abstractions.UnitTests` and `Curl.Core.UnitTests` is clean and both pass with `--filter "TestCategory!=Integration"`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` and `-Library Curl.Core.UnitLibrary` report no failing member in the code this task changed.

## Notes

- Kept small on purpose: a change to `Curl.Protocol.Abstractions.UnitLibrary` makes the quality measurement run nearly every test project.
- The `file` handler's use of it is the next task (it depends on this one).

## Log

- 2026-10-03: Created.
