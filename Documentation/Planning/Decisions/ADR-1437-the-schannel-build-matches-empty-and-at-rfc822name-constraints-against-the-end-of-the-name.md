# ADR-1437: The Schannel build matches empty and `@` rfc822Name constraints against the end of the name (BL-6046)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1300.

## Context
Found in BL-6011 (ADR-1430): below a non-self-signed `--cacert` anchor the Schannel build compared an rfc822Name constraint `@evil.test` for equality and an empty one as a host, so an excluded one accepted `u@evil.test` and a permitted one refused it.

CryptoAPI's matching was measured with `X509Chain` on Windows (a trusted root, a constrained intermediate, a leaf with an rfc822Name), both as an excluded and as a permitted subtree. Excluded reported `0x00008000` exactly where permitted reported no error; every other pair was no error excluded and `0x00004000` permitted.

| Constraint | Matches | Does not match |
|---|---|---|
| empty | every name, the empty one included | |
| `@evil.test` | `u@evil.test`, `U@EVIL.TEST`, `a@b@evil.test`, `@evil.test` | `u@sub.evil.test`, `u@xevil.test`, `evil.test`, `u@evil.testx`, empty |
| `@` | `u@` | `u@evil.test`, `evil.test` |
| `.a@evil.test` | `.a@evil.test` | `x.a@evil.test`, `a@evil.test` |

A host (`evil.test`), a leading-dot domain (`.evil.test`) and a mailbox (`u@evil.test`, `u@`) match as the Schannel build already matched them. A subject emailAddress is judged as an rfc822Name alternative name is.

## Decision
- In the Schannel build an empty rfc822Name constraint, and one starting `@`, matches a name that ends with it, ignoring case.
- The OpenSSL build is unchanged: it was not measured here.

## Consequences
- A chain breaking such a constraint below an intermediate anchor is exit 60 on Windows, and a compliant one under a permitted one is accepted.
