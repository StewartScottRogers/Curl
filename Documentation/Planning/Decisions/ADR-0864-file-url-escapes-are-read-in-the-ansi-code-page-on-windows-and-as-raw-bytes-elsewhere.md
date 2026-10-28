# ADR-0864 — A `file://` URL's escapes are read in the ANSI code page on Windows and as raw bytes elsewhere

- **Status:** Accepted
- **Date:** 2026-10-28

Decided by Claude under Stewart's delegation (task BL-3643, CGR-114).

## Context

CGR-114 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`) found that `FileUrlPath` read the
bytes of every run of `%XX` escapes as UTF-8. Measured on 2026-10-26 and again on 2026-10-28 against
curl 8.22.0 (Git ucrt64 mingw, Schannel, the non-Unicode build ADR-0018 and ADR-0067 make the
Windows reference) on a host whose ANSI code page is 1252, with `café.txt`, `日本.txt` and
`cafÃ©.txt` in one directory:

- `curl -sS file:///<dir>/caf%E9.txt` prints `café.txt`, exit 0;
- `curl -sS file:///<dir>/%E6%97%A5%E6%9C%AC.txt` exits 37, `Could not open file <dir>/%E6%97%A5%E6%9C%AC.txt`;
- `curl -sS file:///<dir>/caf%C3%A9.txt` prints `cafÃ©.txt`, exit 0.

The decoded bytes reach the ANSI file API, so Windows reads them in the system ANSI code page.
curl 8.18.0 on Linux hands them to `open()` unchanged, so `caf%E9.txt` opens the file named by the
raw bytes `caf\xE9.txt`.

## Decision

1. `FileUrlPath` reads each run of escape bytes with a reader chosen per platform
   (`EscapedBytesReaderFor`): on Windows the system ANSI code page
   (`CodePagesEncodingProvider.Instance.GetEncoding(0)`, asked twice as BL-1200 found, Windows-1252
   when the host names none, the rule `DictCommandLineText` follows); elsewhere `RawBytePath.FromBytes`
   (ADR-0839), so a byte that is not UTF-8 reaches `PhysicalFileSystem`'s raw-byte open and valid
   UTF-8 reads as it always did.
2. Non-ASCII characters typed unescaped in the URL stay as typed: the arguments already reach the
   parser through the ANSI code page on Windows (BL-3892), and the reference build opens them.
3. `UrlPath`, the text quoted in the exit 37 message, is unchanged: it was built from the encoded
   text and already matched.
4. An internal `TryParse` overload takes the reader, so the tests pin both platforms' readings on
   every platform.

## Consequences

- The Windows half follows the non-Unicode reference; a Unicode curl build (such as
  `C:\Windows\System32\curl.exe`) would still open `%E6%97%A5%E6%9C%AC.txt` as `日本.txt`, which is
  the difference CGR-114 recorded and the reference rule settles.
- An invalid UTF-8 escape off Windows is now a raw-byte surrogate rather than U+FFFD, so it opens
  the file of that name instead of a name holding U+FFFD.
