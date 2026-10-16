# ADR-0595 — `Libcurl`'s `CurlStrings` is escape, unescape, getenv, strequal and strnequal, over UTF-8 bytes

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2447.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` lists `curl_easy_escape`,
`curl_easy_unescape`, the deprecated `curl_escape` and `curl_unescape`, `curl_getenv`, `curl_strequal`
and `curl_strnequal`. None needs the engine: each is a pure function in upstream's `lib/escape.c`,
`lib/getenv.c` and `lib/strequal.c`. libcurl works on `char` bytes with a length in bytes, where 0
means `strlen`; C# text is UTF-16.

## Decision

1. `Libcurl/Misc/Strings` holds the static `CurlStrings`. Text is read as its UTF-8 bytes and every
   length counts those bytes; `ReadOnlySpan<byte>` overloads take raw bytes.
2. `Escape(CurlEasy? handle, string? text, int length)` is `curl_easy_escape`: every byte but
   `A-Z a-z 0-9 - . _ ~` becomes `%XX` in uppercase hex. `Unescape(CurlEasy? handle, string? text,
   int length, out int outputLength)` is `curl_easy_unescape`: `%` and two hex digits of either case
   become that byte, `%00` included, and anything else, `+` and a lone or cut `%` among them, stays.
   It answers `byte[]`, since decoded bytes need be neither text nor `NUL`-free. Both ignore the
   handle, as libcurl does but for out-of-memory reports. Empty input answers empty, not `null`.
3. `null` text, a negative length, or a length past the end of the bytes answers `null` (and
   `outputLength` 0). The last reads past the buffer in C; refusing it is the safe drop-in answer.
4. The handle-less `Escape(string?, int)` and `Unescape(string?, int)` are `curl_escape` and
   `curl_unescape`, marked `[Obsolete]` as upstream deprecates them.
5. `GetEnv(string name)` is `curl_getenv`: the variable's value, or `null` when it is unset or empty,
   as libcurl answers on every platform. An internal overload takes the lookup so tests do not change
   the process environment.
6. `StrEqual` and `StrNEqual(first, second, nuint maximum)` are `curl_strequal` and `curl_strnequal`,
   answering `bool` for libcurl's 1 or 0: ASCII letters compare without case and nothing else does,
   a `NUL` character ends a string as in C, two `null` strings are equal (for `StrNEqual` only when
   `maximum` is not 0) and one `null` equals nothing.

## Consequences

- `curl_easy_escape` and `_unescape` callers port with the same lengths, since lengths are bytes.
- No new seam member: the functions live wholly in `Libcurl`.
