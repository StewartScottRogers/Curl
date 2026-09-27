# ADR-0037 — A `-z` value that is not a date is read as a file through `IDataFileReader`

- **Status:** Accepted; its non-Windows behaviour is replaced by [ADR-0071](ADR-0071-a-failed-z-file-lookup-off-windows-stands-in-for-stat-and-prints-its-strerror-text.md)
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

curl 8.21.0's tool tries a `-z`/`--time-cond` value that `curl_getdate` rejects as a file
name, and uses that file's modification time as the date (BL-246). The parser reaches the
disk only through injected seams (`pathExists`, `IDataFileReader`), so the lookup needed one.
On Windows curl opens the file with `CreateFile` and, for any failure but
`ERROR_FILE_NOT_FOUND`, prints `Warning: Failed to get filetime: CreateFile failed:
GetLastError 0x%08x` before the two illegal-date lines.

Measured with the reference build (curl 8.21.0, mingw, Schannel) on 2026-09-26,
`curl -z <value> -o NUL file:///Z:/.../global.json`:

| Value | Error code printed |
| --- | --- |
| `notadate` (no such file) | none, only the two illegal-date lines |
| `""`, `-` | `0x00000003` |
| `nodir/x`, `global.json/x` | `0x00000003` |
| a directory, `C:/Windows/System32/config/SAM` | `0x00000005` |
| `C:/pagefile.sys` | `0x00000020` |
| `x*y` | `0x0000007b` |
| `con` | `0x00000057` |
| `nul` | `GetFileTime failed: GetLastError 0x00000057` |

`-s` silences every line. `-z CLAUDE.md` with CLAUDE.md the newer file skips the transfer as
"not new enough"; `-z -CLAUDE.md` transfers it.

## Decision

- `IDataFileReader` gains `TryReadModificationTime(path, out time, out failureReason)`,
  rather than a new parameter on every `CommandLineOptionApplier`: it is already the seam
  through which the parser reads the files an option names.
- The time is truncated to the whole second, as curl's `time_t` is.
- `DiskDataFileReader` opens the file with `File.OpenHandle` and reads
  `File.GetLastWriteTimeUtc(handle)`. The Windows error code is the low word of the
  exception's `HResult`; `ArgumentException` (an empty or malformed path, refused before
  Windows is asked) and an exception carrying no Windows code read as `0x00000003`, as
  curl reports `-z ""`. Error 2 prints nothing.
- Off Windows no filetime line is printed: curl's `stat` failure text (`strerror`) is not
  measured yet, so every failure reads as file not found.

## Consequences

Every measured case above matches except the two DOS devices (`con`, `nul`), which .NET
opens differently from `CreateFileA`; their line may differ. The non-Windows lines are
ADR-0071 (BL-288).

## Alternatives considered

- A new `Func<string, DateTimeOffset?>` parameter on the applier delegate: touches every
  applier for one option.
- `File.GetLastWriteTimeUtc(path)`: returns 1601-01-01 for a missing file and gives no
  error code, so the filetime line could not be told apart.
