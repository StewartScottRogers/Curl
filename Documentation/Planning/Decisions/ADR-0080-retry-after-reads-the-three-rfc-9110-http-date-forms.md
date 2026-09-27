# ADR-0080 — `Retry-After` reads the three RFC 9110 HTTP-date forms

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"). The
decision was taken in BL-208 on 2026-09-26; this record was written in BL-318 because
this folder was held by another lane (BL-303) at the time.

## Context

`--retry` honours a `Retry-After` header on a transient HTTP failure. Its value is
either delay-seconds or an HTTP-date. libcurl 8.21.0's `http_header_r` reads the date
with `Curl_getdate_capped`, its lenient `parsedate.c`, which accepts far more than the
forms HTTP defines. `Curl.Core`'s `RetryAfterHeader` had to decide how much of that to
read.

Measured on 2026-09-26 with curl 8.21.0 (mingw, Schannel; the Windows reference,
ADR-0018) against a loopback server (commands in BL-208's notes): an RFC 1123 date five
seconds ahead waited 5 seconds; a date in the past, `abc`, `garbage 3`, `-3` and
`99999999999999999999` fell back to the backoff; `3abc` and ` 3` waited 3, `2.5`
waited 2; anything over 21600 waited 21600.

## Decision

`RetryAfterHeader.ParseSeconds` reads the three HTTP-date forms RFC 9110 names, with
`DateTimeOffset.TryParseExact` in the invariant culture:

- RFC 1123: `Sun, 27 Sep 2026 05:26:19 GMT`
- RFC 850: `Sunday, 27-Sep-26 05:26:19 GMT`
- asctime: `Sun Sep 27 05:26:19 2026`

Any other text is read as delay-seconds: the digits it starts with, or zero. It does not
port libcurl's `parsedate.c`.

Why: base class library only, every conforming server sends one of the three forms,
and the RFC 1123 form was measured to match curl 8.21.0. Hand-porting `parsedate.c` for
non-conforming dates was not worth the code.

## Consequences

- Every date a conforming server sends waits as curl waits.
- A non-conforming date that curl's `parsedate` would still read (for example
  `27 Sep 2026 05:26:19`, with no weekday) is read by Curl as delay-seconds - usually
  zero, so the retry falls back to the backoff where curl would wait until the date.
- Since this decision, ADR-0074 put one port of `parsedate`, `CurlDateParser`, in
  `Curl.Protocol.Abstractions`, which `Curl.Core` already references. The cost that
  justified the narrower reader is gone; BL-393 moves `RetryAfterHeader` onto
  `CurlDateParser`, and when it lands a new ADR supersedes this one.

## Alternatives considered

- **Port libcurl's `parsedate.c` into `Curl.Core`.** Matches curl on non-conforming
  dates, at the cost of several hundred lines for input no conforming server sends.
- **`DateTimeOffset.TryParse` with no format.** Culture-sensitive and lenient in ways
  that are not curl's; it would read dates curl refuses and refuse some curl reads.
