# ADR-1448 - The OpenSSL build matches rfc822Name constraints as OpenSSL does

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation

## Context

ADR-1437 changed how the Schannel build matches an empty rfc822Name constraint and one starting
`@`, and left the OpenSSL build comparing a constraint holding `@` with the whole address ignoring
case, and any other as a host, taking an address with no `@` as a bare host (BL-6054).

## Measurement

`openssl verify -CAfile ca.pem leaf.pem` with OpenSSL 3.5.7 (Git for Windows), 2026-11-26: a
self-signed CA carrying one rfc822Name subtree, permitted or excluded, and a leaf it signs with one
rfc822Name subjectAltName. 47 is `permitted subtree violation`, 48 `excluded subtree violation`,
53 `unsupported or invalid name syntax`, 0 accepted.

| Constraint | Address | Permitted | Excluded |
| --- | --- | --- | --- |
| `@evil.test` | `u@evil.test`, `U@evil.test`, `u@EVIL.TEST`, `@evil.test`, `a@b@evil.test` | 0 | 48 |
| `@evil.test` | `u@sub.evil.test`, `u@` | 47 | 0 |
| empty | `u@evil.test`, `U@evil.test`, `u@EVIL.TEST`, `@evil.test`, `a@b@evil.test` | 47 | 0 |
| empty | `u@` | 0 | 48 |
| `u@evil.test` | `u@evil.test`, `u@EVIL.TEST` | 0 | 48 |
| `u@evil.test` | `U@evil.test`, `@evil.test`, `a@b@evil.test`, `u@`, `u@sub.evil.test` | 47 | 0 |
| `a@b@evil.test` | `a@b@evil.test` | 0 | 48 |
| `a@b@evil.test` | `A@b@evil.test`, `b@evil.test` | 47 | 0 |
| `b@evil.test` | `a@b@evil.test` | 47 | 0 |
| `@` | `u@` | 0 | 48 |
| `@` | `b@evil.test` | 47 | 0 |
| `evil.test` | `u@evil.test`, `a@b@evil.test`, `@evil.test` | 0 | 48 |
| `.evil.test` | `u@sub.evil.test` | 0 | 48 |
| `.evil.test` | `u@evil.test` | 47 | 0 |
| `.a@evil.test` | `.a@evil.test` | 0 | 48 |
| `.a@evil.test` | `x.a@evil.test` | 47 | 0 |
| each of `@evil.test`, empty, `u@evil.test`, `evil.test`, `.evil.test` | `evil.test`, `x.evil.test` | 53 | 53 |

These are the answers of `nc_email` in OpenSSL's `crypto/x509/v3_ncons.c`.

## Decision

The OpenSSL build compares an rfc822Name, a subject emailAddress included, with an rfc822Name
constraint so:

1. An address with no `@` cannot be compared: unsupported name syntax (verify result 53).
2. A constraint starting `.` and holding no `@` matches the end of the address, ignoring case.
3. Any other constraint's part after its last `@` (all of it when it has none) must equal the
   address's part after its last `@`, ignoring case, and anything before the constraint's last `@`
   must equal the address's local part exactly, case included.

So `@host` matches every local part at that host, and an empty constraint only an address with an
empty host. The bytes are read as Latin-1, so no two bytes outside ASCII become equal in the local
part.

The Schannel build is unchanged (ADR-1437). A SmtpUTF8Mailbox keeps its own comparison (ADR-1442).
