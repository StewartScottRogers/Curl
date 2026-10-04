# ADR-0411: A file:// source time past what curl holds reads as -1 on Windows, and whole elsewhere

- Status: Accepted
- Date: 2026-10-03
- Decided by Claude under Stewart's delegation (BL-1423)

## Context

ADR-0410 decision 3 asked the `file` handler to read a source's last-write time past
9999-12-31T23:59:59Z, which `FileOpenResult.LastWriteTimeUtc` (a `DateTimeOffset`) cannot
carry, and hand it to `-R` and `-z` in Unix seconds. Measured on 2026-10-03 with curl 8.21.0
(Windows, Schannel), US Mountain time, a source stamped 30000-01-01T00:00:00Z
(FILETIME read back as Unix seconds 884541340800):

- `curl -I file:///...` prints `Last-Modified: Thu, 31 Dec 1969 23:59:59 GMT` - curl holds `time_t` -1.
- `curl -R -o out file:///...` exits 0 and leaves `out` at the current time.
- `-z "1 Jan 2020"` writes nothing; `-z "-1 Jan 2020"` writes the body: -1 is compared.
- Bisected: 3001-01-01T00:00:00Z still prints its date; from 32566777200
  (3002-01-01T00:00:00 local) on, curl holds -1. The C runtime's `_fstat64` fails there.

## Decision

1. The handler reads a source's raw last-write time through an internal
   `ISourceLastWriteReader`: `GetFileTime` on the opened `FileStream`'s handle on Windows
   (`LibraryImport`, AOT-safe), none yet elsewhere. The raw time is used only when it lies
   past what `DateTimeOffset` or the platform's curl holds; otherwise the open's time is
   used exactly as before.
2. On Windows, a raw time past local 3001-12-31T23:59:59 is held as -1: `-z` compares
   -1, the header block reports 1969-12-31T23:59:59Z, and `-R` gets no time
   (`SourceLastWriteUnixSeconds` null), as curl leaves the output alone.
3. Elsewhere (no limit), a raw time past 9999 reaches `SourceLastWriteUnixSeconds`
   whole and `-z` compares it in Unix seconds; the header block leaves out
   `Last-Modified`. A raw `stat` reader off Windows is follow-up work.
4. `-z` now compares in Unix seconds (`TimeCondition.ValueUnixSeconds`) for every time.

## Consequences

The Windows boundary depends on the local time zone, as curl's does. curl writes the
weekday of -1 as `Thu`; our header formatter writes `Wed`, the true weekday - a known
difference left for a follow-up.

## Alternatives considered

- Hand -R the real 884541340800 on Windows, as BL-1423 first asked: lost, because curl
  8.21.0 on Windows does not; the platform's curl wins.
- Change `IFileSystem`/`FileOpenResult` to carry Unix seconds: lost for now, because
  `Curl.Protocol.Abstractions.UnitLibrary` was held by BL-1418 and a handle-level read
  inside the `file` library needs no contract change.
