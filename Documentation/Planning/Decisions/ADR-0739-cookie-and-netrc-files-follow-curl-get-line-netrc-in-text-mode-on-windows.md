# ADR-0739 — Cookie and netrc files follow Curl_get_line's line rules; netrc reads in text mode on Windows

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2916
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0 reads cookie and netrc files line by line with `Curl_get_line` (`lib/curl_get_line.c`):
a last line without a newline gets one, a line that with its newline reaches the caller's `dynbuf`
limit fails the read with `CURLE_TOO_LARGE`, and every byte is kept. Measured on curl 8.22.0
(mingw, Schannel) on 2026-10-21 with `Record-CurlExchange.ps1`:

- Cookie files (`MAX_COOKIE_LINE`, 5000): a line of 4998 characters before its line feed, a CR
  counted, is read; 4999 makes curl send no request and exit 100 (`A value or data field grew
  larger than allowed`). A last line without a line feed has the same limit. A `^Z` is kept: a
  line that is only `^Z` is skipped and a cookie holding one is refused as a control character.
  curl opens the file in binary mode.
- Netrc files (16384): a line of 16383 bytes or more, line feed included, makes the file a syntax
  error (exit 26) wherever it is, even after the matching entry has ended, so curl judges every
  line before it parses one. Bytes, not characters, count. On Windows curl opens the file with
  `FOPEN_READTEXT` (`"rt"`), so the C runtime ends it at the first `^Z` (a password cut by one is
  sent up to it) and reads CR LF as one line feed (16382 bytes then CR LF are read).

## Decision

- `NetscapeCookieFile.Read` gains an overload that reports a line past `LongestLine`, and
  `CookieStore.LoadCookieFile` and `LoadCookieFileAsync` return `false` for one (`true` otherwise,
  an unopenable file included). Failing the transfer with exit 100 is the engine's part, filed as
  BL-2971, so the library change lands without touching `Curl.Engine` or `Curl.Console`.
- `NetrcFile.Find` checks every line against `LineBufferLimit` (16384) before parsing, replacing
  `NetrcTokenScanner`'s lazy per-line check, which judged only the lines a lookup reached.
- `NetrcFile.Find` reads in text mode on Windows and binary mode elsewhere, the platform's curl's
  `FOPEN_READTEXT`. The internal `FindWithReadMode` takes the mode, so both are tested on every
  platform. Off Windows a `^Z` stays in the token, as `Curl_get_line` keeps it; that build was
  read from source, not measured, since no Linux or macOS curl runs on the lane machine.

## Consequences

- A netrc file with a long line after the matching entry is now a syntax error, as in curl.
- Curl still sends a request for a cookie file with an over-long line until BL-2971 is done.
- A NUL byte inside a netrc line still counts toward the line limit, though curl's `strlen` on each
  `fgets` chunk would drop the rest of that chunk; no measured case needs it yet.
