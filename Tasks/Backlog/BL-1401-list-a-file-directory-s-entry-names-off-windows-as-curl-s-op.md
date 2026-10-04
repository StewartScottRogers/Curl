---
id: BL-1401
title: List a file:// directory's entry names off Windows as curl's opendir build does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1400, BL-1388]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: FR-004
created: 2026-10-03
completed:
---
# BL-1401 — List a file:// directory's entry names off Windows as curl's opendir build does

## Goal

On Linux and macOS a `file://` URL naming a directory writes the names of its entries that do not start with `.`, each followed by `\n`, and exits 0, as curl 8.21.0's Linux and macOS builds do; on Windows it keeps today's `Could not open file <path>` and exit 37.

## Context

- Today `Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs` treats an open that comes back `FileAccessStatus.IsDirectory` as a read failure on every platform (exit 37), which is the Windows build's answer only.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/file.c`: on Linux and macOS `open()` of a directory succeeds, `file_do` `fstat`s it (lines 414-420: a directory leaves `expected_size` at -1 but records its modification time), writes the `-i`/`-I` header block with no `Content-Length` and no `Accept-ranges` (lines 432-444 write those only for a known size) but with `Last-Modified` and the empty line (lines 446-468), stops there for `-I` (lines 470-471), and then lists the directory (lines 568-586): every `readdir` entry whose name does not start with `.` is written as the name and then `\n` (two body writes), in `readdir` order. A directory that cannot be listed is `CURLE_READ_ERROR` (exit 26). The Windows build cannot open a directory: measured 2026-10-03 with curl 8.21.0 (mingw, Schannel), `curl -sv file:///C:/.../dl/` writes `* Could not open file C:/.../dl/` and exits 37.
- BL-1400 adds `IDirectoryLister` (`Curl.Protocol.Abstractions.UnitLibrary`), implemented by `PhysicalFileSystem`; the handler tests its `IFileSystem` for it (`fileSystem as IDirectoryLister`), so no constructor or composition change is needed. When the file system is not a lister, keep exit 37.
- `-z` on a directory compares the directory's modification time as for a file (BL-1388 adds the `-z` lines; build on it); `--max-filesize` cuts the listing as any body write (curl's `cw_download_write`).

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.File.UnitTests` marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` (or injecting the non-Windows choice) list a fake directory with entries `b.txt`, `.hidden`, `a.txt`, `sub` (in that order) and assert the output `b.txt\na.txt\nsub\n`, exit 0, and the reported byte count 18.
- [ ] Off Windows, `-i` writes `Last-Modified: <date>\r\n\r\n` (no `Content-Length`, no `Accept-ranges`) before the listing, `-I` writes only that header block, and a lister returning `null` gives exit 26 (`CurlExitCode.ReadError`).
- [ ] A test marked `[OSCondition(OperatingSystems.Windows)]` (or injecting the Windows choice) pins today's `Could not open file <path>` and exit 37 for the same directory.
- [ ] `MaxFileSize = 5` on that listing writes `b.txt` and fails with exit 63 `Exceeded the maximum allowed file size (5) with 5 bytes`.
- [ ] Test paths are drive-less; `dotnet build Curl.Protocol.File.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary` reports no failing member in the code this task changed.

## Notes

- The Linux and macOS behaviour is cited from the source: the measuring machine is Windows. The standing rule is to match the OpenSSL build on Linux and macOS.

## Log

- 2026-10-03: Created.
