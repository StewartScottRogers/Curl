# ADR-0853: A user name or password in the `--doh-url` sends `Authorization: Basic` on each DoH POST

- **Status:** Accepted
- **Date:** 2026-10-26
- **Decided by Claude under Stewart's delegation** (BL-3685, CGR-156)

## Context

The third-party gap report (CGR-156) found that real curl sends `Authorization: Basic` on its DoH requests when
the `--doh-url` carries user information, and Curl sent none, so a DoH server that requires authentication refused
Curl's queries. Measured with curl 8.21.0 (Schannel, Windows) on 2026-10-26 through `Record-CurlExchange.ps1`,
`curl -sS --doh-url http://<userinfo>127.0.0.1:21800/dns-query http://foo.test:21802/x`:

| User information | Line after `Host:` on both POSTs |
| --- | --- |
| `u%41:p%3a@` | `Authorization: Basic dUE6cDo=` (`uA:p:`, percent-decoded) |
| `u@` | `Authorization: Basic dTo=` (`u:`) |
| `:p@` | `Authorization: Basic OnA=` (`:p`) |
| `:@` | none |
| none | none |

## Decision

`DohDnsResolver.BasicAuthorizationLine` writes the line between `Host` and `Accept` of every DoH POST: the user and
password split at the first colon, each percent-decoded, joined with a colon and base64-encoded from UTF-8; no line
when both are empty. No `--doh-url` option chooses another scheme, so Basic is the only one.

## Consequences

A DoH server behind Basic authentication answers Curl as it answers curl. The request bytes on the wire equal
curl's for each measured row.
