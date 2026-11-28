# ADR-1466 — SASL DIGEST-MD5 challenges are read as `key=value` directives, as each platform's curl reads them

- **Status:** Accepted
- **Date:** 2026-11-24

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-5881.
Amends ADR-0139: its message formats stand; how the challenge is read changes.

## Context

Dovecot sends `realm="",nonce="…",qop="auth",charset="utf-8",algorithm="md5-sess"`.
RFC 2831 permits the quotes on `charset` and `algorithm`. Curl searched the challenge for
the substrings `algorithm=` and `charset=utf-8`, so the quoted forms did not match: exit 94
on Windows and 67 on Linux, where both platforms' curl log in.

Measured on 2026-11-24 with `Record-CurlExchange.ps1 -Script`, one challenge per exchange,
against curl 8.22.0 (Schannel) on Windows and curl 8.18.0 (OpenSSL) on Ubuntu:

| Challenge | OpenSSL build | Schannel build (SSPI) |
| --- | --- | --- |
| Quoted or unquoted value, any key | accepted | accepted |
| `NONCE=` in capitals | rejected | accepted |
| Blank before a comma | rejected | accepted |
| Blank before `=` | part of the key | accepted |
| Segment with no `=` before the keys | rejected | rejected |
| Segment with no `=` after the keys | ignored | rejected |
| Leading or doubled comma | leading rejected | leading rejected, doubled accepted |
| Trailing comma | accepted | rejected |
| Text after a closing quote, unterminated quote | ends the list | rejected |
| Repeated `nonce`, `algorithm`, `charset` | first counts | rejected |
| Repeated `qop` | first counts | last counts |
| `qop="auth-int, auth"` | rejected | accepted |
| `qop=""` | not measured | answered as `auth` |
| `nonce` of 64 characters | rejected | accepted at 100 |
| `realm` of 128 characters | sent empty | not used |
| Backslash in a quoted value | escapes the next character | escapes the next character |

## Decision

`SaslDigestMd5` splits the challenge into directives, one regular expression for each
platform's rules (interpreted, as a source-generated matcher would fall under the quality
gates), and looks keys up in them: the OpenSSL answer takes
the first value of an exactly matching key and treats one that does not fit curl's buffer
as absent; the SSPI answer rejects a malformed list or a repeated `nonce`, `algorithm` or
`charset`, and matches keys in any case.

## Consequences

Default IMAP, POP3 and SMTP logins work against servers that quote `algorithm` or
`charset`. A `nonce` over 63 characters, which Curl cut to 63 before, is now cancelled in
the OpenSSL path, as measured.

## Alternatives considered

- **Strip quotes in the substring search.** Fixes Dovecot only and leaves the other rows
  above different from the platform's curl.
- **Reuse `DigestChallengeParameters`.** It follows curl's HTTP reading, which skips blanks
  before a comma and accepts an unterminated quote; the SASL measurements show neither.
