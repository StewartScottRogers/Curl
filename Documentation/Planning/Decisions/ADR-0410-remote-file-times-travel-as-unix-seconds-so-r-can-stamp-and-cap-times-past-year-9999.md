# ADR-0410 — Remote file times travel as Unix seconds, so -R can stamp and cap times past year 9999

- **Status:** Accepted
- **Date:** 2026-10-03
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

curl keeps a remote file time as a 64-bit `time_t`, Unix seconds, from the moment a protocol
reads it to the moment `-R`/`--remote-time` stamps the `-o` file. Curl keeps it as a
`DateTimeOffset?` (`TransferResult.SourceLastWriteTimeUtc` in
`Curl.Protocol.Abstractions.UnitLibrary`), whose range ends at 9999-12-31T23:59:59Z, so a
later time is read as an unknown time and the file is not stamped at all.

Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel), `--no-progress-meter -R -o <file>`
against a reply carrying `Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT` (BL-1409):

| What | curl 8.21.0 (Windows) |
|---|---|
| stderr | `Warning: Capping set filetime to max to avoid overflow` |
| file's last-write time | 30827-12-31T23:59:59Z |
| exit code | 0 |

curl 8.21.0's `src/tool_filetime.c` (tag `curl-8_21_0`):

- lines 94-98, Windows only: a time above `910670515199` (30827-12-31T23:59:59Z, the largest
  time a Windows `FILETIME` converted by `SystemTimeToFileTime` holds) is set to that value
  with the warning above, unless `-s`; lines 88-93 are the minimum cap BL-1392 matched.
- line 128 on, POSIX: the time is handed to `utimes` unchanged; no cap and no warning. A
  failure prints `Failed to set filetime %ld on '%s': %s`. Linux's `utimensat` clamps a time
  past the file system's range to that range itself and succeeds, so on Linux the file gets
  the file system's latest time and curl prints nothing.

Which handlers can produce a time past 9999:

| Source | Can it exceed 9999? | Why |
|---|---|---|
| HTTP `Last-Modified` | yes | `curl_getdate` reads any year that fits `time_t`, as the measurement shows |
| FTP `MDTM` | no | the reply is `YYYYMMDDHHMMSS`; curl's `ftp_state_mdtm_resp` reads a four-digit year |
| SFTP / SCP stat | no | SFTP version 3 and SCP carry `mtime` as an unsigned 32-bit count of seconds, ending in 2106 |
| `file://` stat | yes | an NTFS `FILETIME` reaches 30828, and a POSIX `stat` `st_mtime` is 64-bit |

`-z`/`--time-cond` compares the document time with the condition's time as two `time_t`
values in libcurl (`Curl_meets_timecondition`), and `curl_getdate` reads a `-z` date past 9999
just as it reads such a `Last-Modified`.

## Decision

1. **The type.** A file time travels as Unix seconds in a `long`. `TransferResult` stores
   `SourceLastWriteUnixSeconds` (`long?`) as the one value; `SourceLastWriteTimeUtc` stays as
   a view of it - setting it stores the time's Unix seconds (`ToUnixTimeSeconds`, the
   whole-second truncation curl's `time_t` has), and reading it gives the time when it falls
   in `DateTimeOffset`'s range and `null` otherwise. `TimeCondition` stores its time the same
   way (`ValueUnixSeconds`, with `Value` the in-range view). So the
   `Curl.Protocol.Abstractions.UnitLibrary` change is one small, additive task: no handler
   and no caller breaks, and FTP and SFTP, which never exceed 9999, keep setting the
   `DateTimeOffset` view unchanged.
2. **HTTP.** `Curl.Protocol.Http.UnitLibrary`'s `HttpLastModified` reads the three HTTP-date
   forms with any year of four or more digits into Unix seconds and sets
   `SourceLastWriteUnixSeconds`; `HttpDownloadConditions` compares `-z` in Unix seconds, so
   a document from year 40000 is newer than any `-z` date. Sending an `If-Modified-Since`
   for a `-z` date past 9999 formats the year as curl does, measured first.
3. **`file://`.** `Curl.Protocol.File.UnitLibrary` reads a source file's time as Unix seconds
   where .NET's `DateTime`-based `FileSystemInfo.LastWriteTimeUtc` cannot hold it (the Win32
   `FILETIME` on Windows, `stat` off Windows, through `LibraryImport`, which is AOT-safe and
   in the base class library), and compares `-z` the same way.
4. **`-R`.** `Curl.Core.UnitLibrary`'s `IFileTimeSetter` takes Unix seconds, and stamps a time
   past 9999 itself (`SetFileTime` on Windows, `utimensat` off Windows, through
   `LibraryImport`), since `File.SetLastWriteTimeUtc` takes a `DateTime`. `Curl.Console`'s
   `StampOutputFileTimeAsync` works in Unix seconds and, when the runner's `runsOnWindows`
   is true, caps a time above `910670515199` to it with
   `Warning: Capping set filetime to max to avoid overflow` (muted by `-s`) before the set,
   the mirror of BL-1392's minimum cap. Off Windows it passes the time on uncapped, as
   curl's `utimes` branch does; what the kernel then does is what curl gets.
   `RemoteTimeFailureWarning` prints the Unix seconds it is given.
5. **`-z` dates.** `Curl.Cli.UnitLibrary` reads a `-z` date past 9999 into
   `TimeCondition.ValueUnixSeconds`, matching curl's measured reading of it.
6. FTP and SFTP/SCP do not change: they cannot name a time past 9999.

## Consequences

- Projects that change, all already in `Curl.slnx`: `Curl.Protocol.Abstractions.UnitLibrary`,
  `Curl.Protocol.Http.UnitLibrary`, `Curl.Protocol.File.UnitLibrary`, `Curl.Core.UnitLibrary`,
  `Curl.Console`, `Curl.Cli.UnitLibrary`, each with its `.UnitTests` twin.
- The Abstractions task lands first; the HTTP, file and Cli tasks then fan out in parallel;
  the `-R` task waits for the HTTP one so its end-to-end test can pin the measured output.
- `-w '%{filetime}'`, when it is built, reads `SourceLastWriteUnixSeconds` and so prints
  times past 9999 as curl does.

## Alternatives considered

- **Replace `SourceLastWriteTimeUtc` with a `long?` outright.** One property, but every
  handler, the console and their tests break at once, so the Abstractions task would touch
  half the solution and run alone. The view keeps one stored value and one small task.
- **A `UnixFileTime` struct with an implicit conversion from `DateTimeOffset`.** Handlers would
  compile unchanged, but tests comparing the property with a `DateTimeOffset` would no longer
  compare equal and the console would still break; a plain `long` is simpler.
- **Clamp to 9999 in the parser.** Stamps the wrong time on Windows, and prints no warning
  where curl prints one: not a drop-in replacement.
