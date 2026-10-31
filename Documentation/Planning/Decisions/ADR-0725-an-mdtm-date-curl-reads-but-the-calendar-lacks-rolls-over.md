# ADR-0725 — An MDTM date curl reads but the calendar lacks rolls over, as curl's time2epoch does

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2904
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0 reads a `213` reply to `MDTM` in two steps (`lib/ftp.c`, `ftp_state_mdtm_resp`):
`ftp_213_date` takes the first fourteen characters as `YYYYMMDDHHMMSS`, refusing any
non-digit, a month above 12, a day above 31, an hour above 23, a minute above 59 or a second
above 60, and ignoring everything after the fourteen digits. It then hands
`"YYYYMMDD HH:MM:SS GMT"` to `Curl_getdate_capped`, whose `datecheck` fails a year before
1583 and month 00 (stored as -1, "lacks vital info"), and whose `time2epoch` counts days
from the first of the month without checking the day against the month. unit1668.c pins
the first step: month 00, day 00 and 31 February are accepted.

`FtpModificationTime.Of` used `DateTime.TryParseExact`, which refuses all three and accepts
years before 1583, so for those replies `-R`, `-z` and `-I` disagreed with curl.

Real curl 8.22.0 (Schannel build), measured with
`Record-CurlExchange.ps1 -Ftp -FtpReply 'MDTM=213 <date>' -CurlArgs -sS,-I,ftp://...`:

| MDTM date | curl `-I` |
| --- | --- |
| `19980231114234` | `Last-Modified: Tue, 03 Mar 1998 11:42:34 GMT` |
| `19980300114234` | `Last-Modified: Sat, 28 Feb 1998 11:42:34 GMT` |
| `19980320115960` | `Last-Modified: Fri, 20 Mar 1998 12:00:00 GMT` |
| `19980020114234` | no `Last-Modified` line |
| `09980320114234` | no `Last-Modified` line |

## Decision

`FtpModificationTime.TryReadDate` is `ftp_213_date`'s counterpart and returns its six
fields as an `FtpMdtmDate`. `Of` then dates them as `Curl_getdate_capped` does: no time for
a year before 1583 or month 00, and otherwise the first of the month plus `day - 1` days and
the hours, minutes and seconds, so a day the month lacks, day 00 and second 60 roll over into
the next field exactly as the measurements above show.

One case the BCL cannot hold: `99991231235960` rolls into year 10000, past
`DateTimeOffset.MaxValue`. `Of` returns no time for it. curl would date it, but no real
server sends such a date and holding it would need a wider time type through every
consumer of `Of`; the simplest drop-in behaviour is to treat it as unknown, which is what
curl does for any date it cannot read.

`TryReadDate` is internal, and `Curl.Original.Protocol.Ftp.UnitLibrary` grants
`InternalsVisibleTo` to `Curl.Conformance.UnitProgram.UnitLibrary`, so the unit1668 port
(BL-2845) calls it without this library changing again.

## Consequences

- unit1668.c's every row is pinned in `FtpModificationTimeTests`.
- `-R`, `-z` and `-I` over FTP now agree with curl for 31 February, day 00, second 60 and
  years before 1583.
