# ADR-0074 — One curl date parser lives in `Curl.Protocol.Abstractions`

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

libcurl reads a `-z` date and a cookie's `Expires` with the same `parsedate`. Curl had two ports
of it (BL-256): `Curl.Cli`'s `CurlDateParser`, returning a `DateTimeOffset` with no year floor, and
`Curl.Cookies`' `CookieDateParser` (BL-219), returning Unix seconds over curl's whole 64-bit range
with curl's refusal of a year before 1583. `Curl.Cookies` references only
`Curl.Protocol.Abstractions.UnitLibrary`, so it could not use the `Curl.Cli` type, and the copy in
`Curl.Cli` read `-z "1 Jan 1500"` where curl 8.21.0 prints its illegal-date warning.

Measured with curl 8.21.0 (Windows, Schannel) on 2026-09-27:
`curl -z "1 Jan 1500" -o NUL file:///Z:/repos/Curl.lanes/lane-3/global.json` prints
`Warning: Illegal date format for -z, --time-cond (and not a filename). ` and
`Warning: Disabling time condition. See curl_getdate(3) for valid date syntax.`, then transfers,
exit 0; `-z "1 Jan 1583"` prints nothing.

## Decision

- The one port is `Curl.Protocol.Abstractions.CurlDateParser`, the former `CookieDateParser`:
  `TryParse(string, out long unixSeconds)`, curl's whole range, the 1583 floor, and a real -1 read
  as 0. Its tests are `Curl.Protocol.Abstractions.UnitTests/CurlDateParserTests`.
- `Curl.Cookies`' `SetCookieParser` calls it unchanged.
- `Curl.Cli` calls it and turns the seconds into the `DateTimeOffset` `TimeCondition` carries,
  capping an instant after 9999 at 9999-12-31 23:59:59 UTC (ADR-0073). `-z` therefore refuses a
  year before 1583, as curl does.

## Consequences

- One port to keep in step with libcurl; a fix reaches `-z` and cookies together.
- `Curl.Protocol.Abstractions` gains a parser, not only contracts. It is pure, has no I/O and no
  dependency, so every library may still depend on the project without cost.
- The `DateTimeOffset` cap stays in `Curl.Cli`, the only reader that needs it.

## Alternatives considered

- **A new `Curl.Dates.UnitLibrary`.** A project pair and two references for one type; the
  solution's flat layout gains little over the one project both already reference.
- **Let `Curl.Cookies` reference `Curl.Cli`.** Inverts the layering: a protocol-side library would
  depend on the command-line parser.
- **Keep both ports, add the floor to `Curl.Cli`'s.** Two copies of the same 400 lines drift.
