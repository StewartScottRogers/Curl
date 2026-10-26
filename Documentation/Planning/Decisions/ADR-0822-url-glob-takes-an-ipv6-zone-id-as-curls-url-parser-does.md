# ADR-0822: The URL globber takes an IPv6 zone id as curl's URL parser does

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3586)
- Relates to: [ADR-0032](ADR-0032-url-globs-expand-as-curl-8-21-0s-tool-expands-them.md)

## Context

Third-party gap report finding CGR-057: curl 8.22.0 sends `http://[::1%25a%20b]:20200/a` and
`http://[::1%25a:b]:20200/a`, where Curl failed both with exit 3, `bad range specification in position 9`.
curl's globber (`peek_ipv6` in `tool_urlglob.c`) takes `[...]` as literal text when libcurl's URL parser
accepts it as a URL, and Curl's `UrlGlobParser.IsZone` allowed only letters, digits, `-`, `.`, `_` and `~`
in the zone id.

Measured on 2026-10-26 with curl 8.22.0 (Schannel, Windows), `http://[::1%<zone>]:1/a`, exit 7 meaning
the bracket was taken as a host and exit 3 as a range:

- Taken: `25a%20b`, `25a:b`, `2525`, `25%`, `25` + 15 letters, `25a{b`, `25a\b`, `25a[b`, `25a"b`, `25a%b`,
  `25é`, `25é` + 14 letters, `xyz`, `eth0`, `a`, `ag`, `2`, `2g`, `xyz` + 12 letters (15).
- Refused: `25`, empty, `25` + 16 letters, 16 letters, `25a b`, `25a/b`, `25a?b`, `25a#b`, `25a@b`,
  `abc`, `ab%20`, `0a`, `26`, `abcdefghijklmn`.

## Decision

After the `%`, a leading `25` is skipped; otherwise two hexadecimal digits there refuse the zone (curl reads
them as a percent-encoding). What is left must be 1 to 15 characters with no space, control character,
`/`, `?`, `#` or `@`. Anything else, `:` and `%` included, is taken.

## Consequences

The two CGR-057 repros now expand to themselves and are sent. Zones curl refuses are still read as a range,
with curl's message. `Cgr057Tests` in `Curl.Core.UnitTests` pins each measured case.

The limit counts characters, not UTF-8 bytes: curl 8.22.0 on Windows receives its arguments in the ANSI code
page, where `é` is one byte, and took `25é` + 14 letters. A non-ASCII zone on Linux may differ by a byte; no
test pins that.
