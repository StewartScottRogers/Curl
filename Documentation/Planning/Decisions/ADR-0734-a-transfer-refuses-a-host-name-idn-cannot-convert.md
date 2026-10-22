# ADR-0734 — A transfer refuses a host name IDN cannot convert with exit 3; the URL API keeps it

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2962
- Decided by Claude under Stewart's delegation.

## Context

Upstream cases 1034 (`invalid-utf8-\xe2\x90.local`, bytes that are not UTF-8) and 1035 (a label
longer than 63 characters once converted to punycode) expect exit 3 (`CURLE_URL_MALFORMAT`) in a
UTF-8 locale: curl's `Curl_idnconvert_hostname` fails the transfer before it connects. Curl kept
any name `IdnMapping` could not convert "as decoded" (ADR-0010), citing curl accepting `a%80b`
and `a%FFb`, so both cases connected to the proxy and ended with exit 7.

Measured with the Windows reference curl 8.22.0 (libidn2, `LC_ALL=C.UTF-8`, `-x 127.0.0.1:1`):

| Host | Exit |
| --- | --- |
| `invalid-utf8-%e2%90.local`, percent-encoded or raw in a `-K` file | 3 |
| the too-long label of test 1035, percent-encoded or raw | 3 |
| `a%90b.invalid` | 3 |
| `a%80b`, `a%FFb` | 7 (resolves `xn--ab-8fu`) |
| `a%c3%bcb.invalid` | 6 (resolves `xn--a14b-goa3139b`, two characters) |

That build ignores the UTF-8 locale and reads host bytes as Windows-1252: `%c3%bc` becomes
`Ã¼`, not `ü`, and `%80` becomes `€`. Only bytes Windows-1252 leaves undefined (`%90`) fail.
curl in a UTF-8 locale - the setting the upstream cases run under, and the OpenSSL build on Linux
and macOS - reads the bytes as UTF-8, where `a%80b` is not valid either.

The URL API (`curl_url_set`, lib1560, unit1675) converts no name unless asked, so it accepts
these hosts and keeps their bytes.

## Decision

- A transfer's URL (`CurlUrl.TryParse` and the overloads it backs) refuses a host name that is not
  ASCII and that `IdnMapping` cannot convert - bytes that are not UTF-8, or a label too long -
  as `CurlUrlRejection.BadHostname`, exit 3, as curl does in a UTF-8 locale.
- `CurlUrl.TryParseForUrlApi`, `TryParseAllowingEmptyHost` and the unit1675 port keep such a name
  as decoded and resolve it as written, as before.
- Host bytes are read as UTF-8 on every platform; the Windows reference build's Windows-1252
  reading of `a%80b` (exit 7) is not copied, because Curl's IDN is UTF-8 throughout and the
  upstream cases pin the UTF-8 answer.

## Consequences

- Upstream cases 1034 and 1035 pass and are on the ratchet.
- `curl http://a%80b/` now exits 3 where the Windows reference curl exits 7; on Linux and macOS
  in a UTF-8 locale both exit 3.
- This replaces ADR-0010's "kept as decoded" rule for transfers; it still holds for the URL API.
