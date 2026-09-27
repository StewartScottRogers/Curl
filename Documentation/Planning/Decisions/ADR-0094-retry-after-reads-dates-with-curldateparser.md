# ADR-0094 — `Retry-After` reads dates with `CurlDateParser`

- **Status:** Accepted
- **Date:** 2026-09-27
- **Supersedes:** ADR-0080

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"; task BL-393,
2026-09-27).

## Context

ADR-0080 had `RetryAfterHeader` read only the three RFC 9110 HTTP-date forms with
`DateTimeOffset.TryParseExact`, because porting libcurl's lenient `parsedate.c` into
`Curl.Core` was not worth the code. ADR-0074 has since put that port,
`Curl.Protocol.Abstractions.CurlDateParser`, in a project `Curl.Core` already references,
so the cost is gone.

libcurl 8.21.0's `http_header_r` reads a `Retry-After` value with `Curl_getdate_capped`
first and, only when that fails, as delay-seconds. Measured on 2026-09-27 with curl 8.21.0
(mingw, Schannel; the Windows reference, ADR-0018) through `Record-CurlExchange.ps1`, a 503
carrying `Retry-After: <date four seconds ahead>` and `--retry 1` (commands in BL-393's
notes): `27 Sep 2026 20:37:58`, `Sep 27 2026 20:38:02`, `20260927 20:38:06`,
`Sun, 27 Sep 2026 20:38:10 +0000`, `2026 Sep 27 20:38:14 UTC` and
`27 Sep 2026 21:39:00 +0100` all announced `Retrying in 4 seconds` (one 3, a second
boundary), so each was read as a date. `5 Sep` (no year) announced 5 seconds, read as
delay-seconds; `1 Jan 2000` fell back to the 1-second backoff.

## Decision

`RetryAfterHeader.ParseSeconds` reads a date with `CurlDateParser.TryParse` and waits the
whole seconds from `now` (its Unix seconds, as curl's `time(NULL)`) until it, or zero when it
has passed. Text `CurlDateParser` refuses is read as delay-seconds as before. The
`HttpDateFormats` table is gone.

## Consequences

- Every date curl reads, conforming or not, waits as curl waits.
- One date reader serves `-z`, cookie `Expires` and `Retry-After`, so a fix to it reaches
  all three.
- An eight-digit value such as `20260927` is a date to curl and so to Curl; a delay that
  long would be capped at six hours anyway.

## Alternatives considered

- **Keep ADR-0080's three forms.** Leaves non-conforming dates reading as delay-seconds,
  usually zero, where curl waits until the date; the reason for it no longer holds.
