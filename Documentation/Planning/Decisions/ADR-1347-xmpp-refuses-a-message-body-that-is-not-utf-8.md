# ADR-1347: xmpp refuses a message body that is not valid UTF-8

- Status: Accepted
- Date: 2026-11-24
- Decided by Claude under Stewart's delegation
- Task: BL-5899 (fitness finding EXT-124)

## Context

`xmpp://` and `xmpps://` send the bytes of `-d` or `-T` as a message's `<body/>` (ADR-1068). An XMPP stream is XML in
UTF-8 (RFC 6120, 11.6). The body was decoded with a replacing decoder, so every invalid byte became U+FFFD and the
transfer ended with exit 0: the data changed and nothing said so.

## Decision

A message body from `-d` or `-T` that is not valid UTF-8 fails the transfer with exit 43 (`CURLE_BAD_FUNCTION_ARGUMENT`)
and `XMPP message body is not valid UTF-8`, before any connection is opened, so nothing reaches the server. Valid UTF-8,
U+FFFD itself and a leading byte order mark included, is sent as before.

## Why

Exit 43 is what the client already gives for a stanza it cannot carry, a character XML 1.0 forbids (ADR-1068).
Refusing is better than guessing a source encoding such as Latin-1, which would also change bytes the user did not ask
to have changed. A disco query sends no body, so its data is not checked.

## Consequence

On Windows a typed `-d` argument arrives as bytes in the ANSI code page (ADR-0838), so typed non-ASCII text such as
`-d café` is refused too; it was delivered with U+FFFD before. BL-5915 is filed to give the handler that text as UTF-8.
