# ADR-0179 — The HSTS cache reads, matches and writes curl's file as libcurl 8.21.0 does

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-620.

## Context

`--hsts <file>` makes curl read an HSTS cache, switch a `http://` URL to `https://` for a
known host, learn from `Strict-Transport-Security` response headers (RFC 6797) and write the
cache back. Drop-in scripts share that file with the real curl, so it must be read and
written byte for byte as curl does, and the header read as curl reads it. curl documents the
format (https://curl.se/docs/hsts.html) but not the edge cases, so they were measured: curl
8.21.0 (mingw, Schannel) with `-k --hsts cache.txt https://localhost:18443/` against
`Record-CurlExchange.ps1 -Tls`, with a range of header values and seeded files, and read
against libcurl 8.21.0's `lib/hsts.c`. The commands and every file curl wrote are in
BL-620's notes. Wiring the cache into a transfer is BL-621.

## Decision

1. **Where it lives.** `Curl.Core.UnitLibrary\Hsts`: `HstsCache` (text in, text out,
   `TimeProvider` injected), `HstsHeaderParser` (giving an `HstsHeader`),
   `HstsFileLineParser` (giving an `HstsFileLine`), `HstsEntry`, and the internal
   `HstsExpiryText` and `AsciiText`. No file or network access.
2. **The header**, as `Curl_hsts_parse`: directives split at `;`, blanks skipped;
   `max-age` (any case) is `=`, then digits, optionally quoted; `includeSubDomains` (any
   case) sets the flag; anything else is skipped to the next `;`. A value without
   `max-age`, with a second `max-age` or `includeSubDomains`, with no digits, a sign or a
   missing closing quote is ignored. An unquoted number past 64 bits, or an expiry past
   64 bits, is `unlimited`. `max-age=0` removes the host's own entry only. A header from an
   IP address teaches nothing. An existing entry for the host is updated in place, flag
   included (so a header without `includeSubDomains` clears it); a new one is added last.
3. **The file.** A line is `host "expiry"`: leading blanks skipped, `#` comments and empty
   lines skipped, exactly one space, the expiry quoted, at most 17 characters, read by
   `CurlDateParser` (curl's `Curl_getdate_capped`) or `unlimited`; nothing may follow the
   quote but the line end or a CR. An expired or unreadable date skips the line. A leading
   dot means `includeSubDomains`; a trailing dot is dropped. A host already held keeps its
   place and takes the later expiry and either flag. A dotted host under a held
   `includeSubDomains` parent is skipped, as `hsts_add_host_expire`'s lookup finds the
   parent. A line longer than 4093 characters ends the reading. At most 10000 entries are
   held; one more drops the first.
4. **Lookup** is `hsts_check`: the host's own entry (ASCII case-insensitive, one trailing
   dot ignored), else the `includeSubDomains` entry with the longest name the host is a
   subdomain of; expired entries passed are removed.
5. **Writing** gives curl's two comment lines, then `[.]host "yyyyMMdd HH:mm:ss"` (UTC) or
   `"unlimited"` per entry in order, expired entries still held included, with the caller's
   line ending (`Environment.NewLine`: curl writes in text mode, CR LF on Windows). An entry
   whose expiry the platform's `gmtime` cannot convert makes curl leave the file untouched:
   `FormatFile` then returns `null`. The Windows limit, measured, is 3001-01-01 20:59:59 UTC;
   off Windows it is the last second of year 2147485547, where glibc's `struct tm` year
   overflows. Years past 9999 are written with all their digits.

## Deviations kept

- The Windows `gmtime` limit was measured on a machine in UTC-7 (US Mountain Standard
  Time); the C runtime's limit may shift with the local zone. The measured value is used on
  every Windows machine.
- The 4093-character line limit is counted without a trailing CR, as the Windows curl
  reads the file in text mode; the Linux and macOS curl would count the CR of a CR LF line.
- The off-Windows write limit comes from glibc's source, not a measurement: no Linux curl
  was at hand.

## Alternatives considered

- **Reading only curl's own date form**, as `AltSvcCache` does: `hsts_add_host_expire`
  hands the date to `Curl_getdate_capped`, and measured curl accepts `20300230` (as
  2 March), `20300101 23:59:60` and `20300101 0:0:0`, so the shared `CurlDateParser` reads it.
- **Dropping expired entries when reading them all at once**: curl removes them only as a
  lookup passes them, which changes which ones are written back.
- **Always LF**: the Windows curl writes CR LF.

## Consequences

- `Curl.Core.UnitTests` pins the header, the file lines, lookups and the written bytes of
  each measured case.
- BL-621 wires it: read the file before the first transfer, upgrade a `http://` URL whose
  host `Find` knows, apply each `Strict-Transport-Security` header of an HTTPS response,
  write the file at the end unless `FormatFile` returns `null`.
