# ADR-1430: The Schannel build matches otherNames against name constraints as CryptoAPI does (BL-6011)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1300.

## Context
Fitness finding STD-218: below a non-self-signed `--cacert` anchor the Schannel build ignored `otherName` name constraints, so a User Principal Name (UPN, `1.3.6.1.4.1.311.20.2.3`) the anchor excludes, or does not permit, was accepted where curl answers exit 60.

curl 8.23.0 (Windows `System32` `curl.exe`, Schannel), a loopback TLS server, `--ssl-no-revoke` and the constrained intermediate given as `--cacert`: an excluded UPN `evil.test` against a leaf UPN `user@evil.test` is `schannel: error 0x00008000`, a permitted one against `user@good.test` is `schannel: error 0x00004000`, and the compliant leaves exit 0.

CryptoAPI's matching was measured with `X509Chain` on Windows (a trusted root, a constrained intermediate, a leaf with `otherName`s), the chain status being CryptoAPI's `CERT_TRUST_` bits:

| Constraint | Matches | Does not match |
|---|---|---|
| UPN `evil.test` | `USER@EVIL.TEST`, `evil.test`, `a@b@evil.test`, `@evil.test` | `user@sub.evil.test`, `user@notevil.test`, `sub.evil.test`, `xevil.test`, `evil.test@good.test`, empty |
| UPN `.evil.test` | `user@a.b.evil.test`, `sub.evil.test` | `user@evil.test` |
| UPN `@evil.test` | `user@evil.test`, `a@b@evil.test` | `user@sub.evil.test`, `evil.test` |
| UPN `user@evil.test` | `USER@evil.TEST` | `xuser@evil.test`, `other@evil.test` |
| UPN `user@` | `user@` | `user@evil.test` |
| UPN empty | every value | |
| other type-id `evil.test` | `xevil.test`, `sub.evil.test` | `u@sub.evil.test`, `u@xevil.test`, `evil.testx` |
| other type-id `abc` | `ABC`, `xabc`, `x@abc` | `a@xabc`, `abcx` |

An `otherName` constraint applies only to `otherName`s of its type-id. Values of every character string type compare by their text. A value that is not a character string matches nothing; such a permitted subtree, met before a matching one, makes the answer `0x00001000` (not supported) unless an excluded name is also broken (`0x00008000`); such an excluded subtree alone is ignored.

## Decision
- The Schannel build checks the leaf's `otherName`s against `otherName` constraints with the rules above: an empty constraint, or one starting `.` or `@`, matches the end of the value; so does any constraint of a type-id other than UPN against a value with no `@`; every other pair compares as an rfc822Name constraint.
- One corner is not copied: an unsupported excluded subtree listed before a matching one answers `0x00008000`, where CryptoAPI answers `0x00009000`. Both refuse the chain with exit 60.
- The OpenSSL build is unchanged.

## Consequences
- A chain breaking an `otherName` constraint below an intermediate anchor is exit 60 on Windows, as with curl.
