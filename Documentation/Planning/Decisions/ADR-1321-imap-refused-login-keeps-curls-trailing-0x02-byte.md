# ADR-1321 — A refused IMAP LOGIN keeps curl's trailing 0x02 byte

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5645).

## Context

A Surly cookbook report called the 0x02 after `Access denied. ` on a refused IMAP `LOGIN` a stray byte.

## Decision

Keep it. Measured against a loopback server answering `NO denied` to `LOGIN`, curl 8.22.0 (Schannel, Windows) exits 67 and writes `curl: (67) Access denied. ` followed by byte 0x02 and CRLF to stderr. curl's `failf("Access denied. %c", imapcode)` prints the response code as a character. Curl matches it byte for byte, and being a drop-in replacement outranks tidiness. `ImapSessionMessages.AccessDenied` stays as it is and its tests already pin the byte.

## Consequences

Scripts see the same stderr bytes under either binary. `Curl.Api` carries the same message, as it does for every other curl message.
