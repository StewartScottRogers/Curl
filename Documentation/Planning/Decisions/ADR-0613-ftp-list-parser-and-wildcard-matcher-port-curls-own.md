# ADR-0613 — The FTP LIST parser and wildcard matcher port curl's own, byte for byte

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2476.

## Context

An FTP wildcard download (`CURLOPT_WILDCARDMATCH`, BL-2478 and BL-2420) lists the directory, parses the `LIST`
reply into `struct curl_fileinfo` entries and keeps the ones whose names match the URL's pattern. curl 8.22.0 does
this in `lib/ftplistparser.c` (Unix `ls -l` and Windows/DOS formats) and `lib/curl_fnmatch.c`. Several choices are
not fixed by the source alone:

- `curl_fnmatch.c` holds curl's own matcher only `#ifndef HAVE_FNMATCH`. The Linux and macOS builds have
  `fnmatch(3)` and call it instead, and upstream's `unit1307.c` pins where they differ (for example `[` matches `[`
  on Linux, `\` alone does not, and `[\xFF]` fails on both).
- curl's strings are bytes; .NET strings are UTF-16.
- `hardlinks` is a C `long`: 32 bits on Windows, 64 elsewhere.

## Decision

1. **curl's own matcher on every platform.** `FtpWildcardPattern.Matches` ports `Curl_fnmatch`'s custom loop,
   quirks included (at most two star groups, a malformed set never matches, only the first class named in a set in
   curl's order counts, `[:space:]` tests blanks), and its tests pin the custom column of `unit1307.c`. Matching the
   Linux and macOS builds' system `fnmatch` where it differs is filed as its own task (BL-2480), so this one stays
   one library's port.
2. **Bytes as Latin-1.** The parser reads bytes and gives every text one character per byte (Latin-1), cut at the
   first NUL as curl's C strings are; the matcher turns its strings back into Latin-1 bytes. A name in UTF-8 is
   matched byte for byte, as curl does.
3. **The platform's `long`.** `FtpFileListParser` takes `runsOnWindows`, as `FtpUploadLineEndings` does: a hard
   link count past 2³¹ − 1 is unknown on Windows and kept elsewhere.
4. **The selector is a predicate.** The parser asks a `Func<string, bool>` once per completed entry, in order, as
   `ftp_pl_insert_finfo` calls the fnmatch callback, so the engine can pass `CURLOPT_FNMATCH_FUNCTION` or the
   default matcher with its pattern. A symbolic link whose target holds another ` -> ` is dropped after the
   selector is asked, as curl drops it.
5. **Failures as exit codes.** A malformed byte ends the parse with `FtpBadFileList` (87); an entry past curl's
   10,000-byte buffer with `OutOfMemory` (27), the code curl stores when its buffer refuses a byte. Later chunks are
   ignored and the entries kept before stay.

## Consequences

The engine (BL-2420, BL-2478) maps `FtpFileListEntry` to `CurlFileInfo` and drives the download loop; neither
type references `Curl.Api`. Off Windows, patterns where system `fnmatch` answers differently match as Windows'
curl does until BL-2480 lands.
