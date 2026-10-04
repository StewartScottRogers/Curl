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
completed: 2026-10-03
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

- [x] Tests in `Curl.Protocol.File.UnitTests` marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` (or injecting the non-Windows choice) list a fake directory with entries `b.txt`, `.hidden`, `a.txt`, `sub` (in that order) and assert the output `b.txt\na.txt\nsub\n`, exit 0, and the reported byte count 18.
- [x] Off Windows, `-i` writes `Last-Modified: <date>\r\n\r\n` (no `Content-Length`, no `Accept-ranges`) before the listing, `-I` writes only that header block, and a lister returning `null` gives exit 26 (`CurlExitCode.ReadError`).
- [x] A test marked `[OSCondition(OperatingSystems.Windows)]` (or injecting the Windows choice) pins today's `Could not open file <path>` and exit 37 for the same directory.
- [x] `MaxFileSize = 5` on that listing writes `b.txt` and fails with exit 63 `Exceeded the maximum allowed file size (5) with 5 bytes`.
- [x] Test paths are drive-less; `dotnet build Curl.Protocol.File.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary` reports no failing member in the code this task changed.

## Notes

- Decision (sensible default, no ADR needed - the platform split follows the standing "match the platform's curl" rule): the choice is `FileProtocolHandler.ListsDirectories`, an internal init property defaulting to `!OperatingSystem.IsWindows()`, so tests inject either answer and the composition root is untouched.
- The directory's timestamp comes from the failed open's `FileOpenResult.LastWriteTimeUtc` (`Failed(IsDirectory) with { LastWriteTimeUtc = ... }`); `IDirectoryLister` gives names only. `PhysicalFileSystem` does not fill it in yet, so on a real disk the `-i` block is only the blank line and `-z` always transfers: filed BL-1422 (Curl.Core, outside this task's touches).
- The listing of `b.txt`, `.hidden`, `a.txt`, `sub` is `b.txt\na.txt\nsub\n`, which is 16 bytes, not the 18 the first criterion said; the test asserts 16.
- `--max-filesize` writes what fits of each write, then exit 63, as `cw_download_write` does; a failed `opendir` is exit 26 with curl's easy error text and no `-v` line (no `failf` upstream), and closes the connection as other exit 26 failures do.
- Range options do nothing to a listing except skip `-z`, as for a file (`HasRange`).
- Tests: `FileProtocolHandlerDirectoryListingTests` (15). Measure-CodeQuality on Curl.Protocol.File.UnitLibrary: one failing member, `PseudoHeaderLines`, left unused by the refactor; it was removed (its doc moved to `HeaderLines` and `PseudoHeaders`). Every other member passed.

- The Linux and macOS behaviour is cited from the source: the measuring machine is Windows. The standing rule is to match the OpenSSL build on Linux and macOS.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. file:// lists a directory's entry names off Windows (Last-Modified-only -i block, -I, -z, --max-filesize, exit 26 on a failed listing); Windows keeps exit 37
