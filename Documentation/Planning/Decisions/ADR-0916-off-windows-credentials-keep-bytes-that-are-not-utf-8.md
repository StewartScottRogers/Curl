# ADR-0916: Off Windows, credentials keep every byte that is not valid UTF-8

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3582, CGR-053)
- Relates to: [ADR-0022](ADR-0022-basic-and-bearer-credentials-are-sent-as-the-platform-curl-sends-them.md), [ADR-0909](ADR-0909-url-and-netrc-credentials-are-sent-as-their-bytes.md), [ADR-0909](ADR-0909-linux-and-macos-read-argv-raw-bytes-with-surrogate-escapes.md)

## Context

`curl -sS "http://user:pa%FFss@127.0.0.1:20582/a"` sends `Authorization: Basic dXNlcjpwYf9zcw==`
(`user:pa\xFFss`) on curl 8.22.0 Schannel (measured 2026-10-26 with `Record-CurlExchange.ps1`) and on
curl 8.18.0 OpenSSL on Linux (the gap report). After ADR-0910 Curl matched on Windows, where the ANSI code
page Windows-1252 round-trips every byte, but off Windows the credential encoding was `Encoding.UTF8`: the
decoded byte FF read back as U+FFFD and went out as EF BF BD.

## Decision

`CredentialEncoding.ForPlatform(false)` is `InvalidUtf8ByteEscapingEncoding`, UTF-8 with the surrogate-escape
scheme `Curl.Cli`'s `InvalidUtf8ByteEscape` uses for argv: a byte outside a valid sequence decodes to the lone
surrogate U+DC00 + byte and encodes back to the byte; valid text is exactly UTF-8, and it reports code page
65001. `TransferCredentialLookup` reads the decoded bytes back through it, so the authenticators send them as
they are. The type is internal to `Curl.Authentication` (a copy rather than a move, since `Curl.Cli` does not
reference `Curl.Authentication` and its file is held by live tasks); a `-u` argument holding argv's escapes now
also goes out as its bytes.

## Consequences

Every user of `ForPlatform` off Windows (authenticators, SigV4, SSH, TFTP, proxy credentials, `-J` names,
`If-None-Match`) keeps bytes that are not UTF-8 instead of replacing them with U+FFFD; valid UTF-8 is unchanged.
