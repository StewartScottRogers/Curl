# ADR-0175 — The alt-svc cache reads, matches and writes curl's file as libcurl 8.21.0 does

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-622.

## Context

`--alt-svc <file>` makes curl read an alt-svc cache, learn from `Alt-Svc` response headers
(RFC 7838) and write the cache back. Drop-in scripts share that file with the real curl, so
the file must be read and written byte for byte as curl does, and the header must be read as
curl reads it, quirks included. curl documents the format (https://curl.se/docs/alt-svc.html)
but not the edge cases, so they were measured: curl 8.21.0 (mingw, Schannel) with
`-k --alt-svc cache.txt https://localhost:18443/` against `Record-CurlExchange.ps1 -Tls`,
with a range of `Alt-Svc` values and seeded files. The commands and every file curl wrote are
in BL-622's notes. Wiring the cache into a transfer is BL-623.

## Decision

1. **Where it lives.** `Curl.Core.UnitLibrary\AltSvc`: `AltSvcCache` (text in, text out,
   `TimeProvider` injected), `AltSvcHeaderParser` (giving an `AltSvcHeader` of
   `AltSvcAlternative`s), `AltSvcFileLineParser`, `AltSvcEntry`, `AltSvcAlpn` and
   `AltSvcAlpnToken`. No file or network access; the caller reads and writes the file, and
   passes origin hosts as curl does, an IPv6 address without brackets.
2. **The header**, as measured: `clear` (any case, before the first `;`, CR or LF) removes
   the origin's entries. Otherwise each alternative is `alpn="[host]:port"` followed by its own
   `; name=value` parameters - `ma` applies to the alternative it follows, not to the
   whole value. ALPN tokens are exactly `h1`, `h2`, `h3` (`H2` is unknown); an unknown one is
   skipped without stopping. A host longer than 2048 characters (46 between brackets), a
   port above 65535, a missing port or a missing quote stops reading and keeps what came
   before. A parameter name runs to the next `=`, so `h2=":8443"; foo, h3=":443"` loses
   the `h3` alternative, as curl does. `ma` defaults to 86400 seconds, also when unreadable
   or too big for 64 bits; `persist` counts only as `1`; parameter names are
   case-insensitive and blanks around them are trimmed. The first known alternative of each
   header replaces every entry the origin had, so a second `Alt-Svc` header replaces the
   first's.
3. **The file.** Lines are read strictly: nine fields and exactly one space between them,
   ALPNs `h1`/`h2`/`h3`, ports up to 65535, `persist` 0 or 1, priority 0, date quoted as
   `"yyyyMMdd HH:mm:ss"` UTC, hosts up to 2048 characters. Any other line is skipped, and so
   is an entry that has already expired. A bracketed host loses its brackets, and a source
   host its trailing dot. A line longer than 4093 characters (without its line ending),
   comment or not, ends the reading: nothing after it is read.
4. **Lookup** matches source ALPN, port and host (case-insensitive, ignoring one trailing
   dot) with an allowed destination version, first match wins, and removes the expired
   entries it passes. An entry expires once its second is past: an entry with `ma=0` is
   still written back.
5. **Writing** gives curl's two comment lines, then one line per entry in the order held,
   IPv6 hosts in brackets, priority `0`. The line ending is the caller's: curl writes in
   text mode, so its Windows build writes CR LF (measured) and its Linux and macOS builds LF;
   the caller passes `Environment.NewLine`.

## Deviations kept

- curl reads the file's date with `Curl_getdate_capped`, so a short date in another form
  (up to 17 characters, such as `1 Jan 2030`) would be read; only curl's own form is read
  here. curl only ever writes that form.
- An `ma` that reaches past year 9999 expires at `9999-12-31 23:59:59`. The Windows curl
  fails to write the file at all in that case (its `gmtime` stops at year 3000), and the
  Linux curl writes a year no `DateTimeOffset` holds.
- The 4093-character line limit is counted without a trailing CR, as the Windows curl
  reads the file in text mode; the Linux and macOS curl would count the CR of a CR LF file.

## Alternatives considered

- **One shared parameter set per header value**, as older libcurl did: measured 8.21.0
  gives `h3` the default 24 hours after `h2=":8443"; ma=60`, so per alternative it is.
- **Always LF** so the file is the same everywhere: the Windows curl writes CR LF, and a
  drop-in replacement writes what the platform's curl writes.
- **Dropping expired entries when writing**: curl writes every entry it holds.

## Consequences

- `Curl.Core.UnitTests` pins the header, the file lines, lookups and the written bytes of
  each measured case.
- BL-623 wires it: read the file before the first transfer, apply each `Alt-Svc` header of
  an HTTPS response, look up before connecting, write the file at the end.
