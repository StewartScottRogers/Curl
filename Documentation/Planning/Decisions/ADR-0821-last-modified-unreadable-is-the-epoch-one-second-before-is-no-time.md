# ADR-0821: An unreadable Last-Modified is the epoch, and one second before the epoch is no file time

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3576)
- Relates to: [ADR-0044](ADR-0044-http-honours-range-resume-time-condition-and-max-filesize.md), [ADR-0410](ADR-0410-remote-file-times-travel-as-unix-seconds-so-r-can-stamp-and-cap-times-past-year-9999.md)

## Context

Third-party gap report finding CGR-047: with `-R`, curl 8.22.0 (Schannel, Windows; 8.18.0 on Linux agrees)
gives the downloaded file the epoch (1970-01-01T00:00:00Z) when `Last-Modified` is a value it cannot read
(`garbage`, an empty value, `x`, `1445412480`, `2015-10-21T07:28:00Z`, day 32, hour 25), and leaves the file
at its download time for `Wed, 31 Dec 1969 23:59:59 GMT`. Curl did the opposite: it took an unreadable value
as no time and read the instant one second before the epoch as the epoch, as `CurlDateParser.TryParse`
does for `-z` and cookie dates.

Measured on 2026-10-26 with `Record-CurlExchange.ps1` and curl 8.22.0 from Git for Windows, `-z` follows the
same document time: with `-z "Jan 1 2000"` and `-z "-Jan 1 2000"`, an unreadable `Last-Modified` delivers the
body either way (curl's `Curl_meets_timecondition` takes a document time of 0 as met), `Wed, 31 Dec 1969
23:59:59 GMT` is compared as -1 (304 for the first, the body for the second), and `Thu, 01 Jan 1970 00:00:00
GMT` delivers the body for both. Curl answered 304 for that last one.

## Decision

- `CurlDateParser.ParseOrEpoch` reads a response header's date as curl reads `Last-Modified` into its document
  time: a date `TryParse` accepts gives its instant, including -1 for one second before the epoch, and any
  other text gives 0. `TryParse` is unchanged for its other callers.
- `HttpLastModified.Find` gives that document time for `-z`, and `HttpDownloadConditions.IsMet` takes a
  document time of 0 as met.
- `HttpLastModified.FindFileTime` gives `-R` and `%{filetime}` that time, with -1 as no time, as curl's
  `CURLINFO_FILETIME` takes -1.

## Consequences

Each repro in CGR-047 and the `-z` cases above now match curl 8.22.0 in exit code, output, file time and
request bytes (BL-3576 Notes). The change is platform-neutral: it is date arithmetic on a header value.
