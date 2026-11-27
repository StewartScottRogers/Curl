# ADR-1441: The OpenSSL build checks otherNames against name constraints as OpenSSL does (BL-6047)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1300 and ADR-1430.

## Context
Below a non-self-signed `--cacert` anchor the port's own check decides, and in the OpenSSL build it read no `otherName`, so no `otherName` name constraint was evaluated (ADR-1430 changed the Schannel build only).

Measured with curl 8.18.0 and OpenSSL 3.5.5 (WSL Ubuntu), an `openssl s_server` with a leaf holding `DNS:localhost` and the name below, signed by a constrained intermediate given as `--cacert`. Every refusal is exit 60, `curl: (60) SSL certificate OpenSSL verify result: <text> (<code>)`, and `%{ssl_verify_result}` is the code.

`otherName` constraints (permitted and excluded alike):

| Constraint | Leaf name | Answer |
|---|---|---|
| UPN `evil.test` | UPN `user@evil.test` or `user@good.test` | `unsupported name constraint type (51)` |
| UPN INTEGER 5 | UPN INTEGER 5 | 51 |
| other type-id `1.2.3.4` | otherName `1.2.3.4` | 51 |
| UPN | no otherName, an rfc822Name, or an otherName of another type-id | exit 0 |
| SmtpUTF8Mailbox, any value | SmtpUTF8Mailbox or rfc822Name, matching or not | exit 0 |

With a UPN constraint and an excluded DNS name both broken, the first alternative name in the certificate decides (51 for the UPN first, 48 for the DNS name first). A root as `--cacert` also answers 51.

An rfc822Name constraint applies to a SmtpUTF8Mailbox (`1.3.6.1.5.5.7.8.9`). "Within" is `excluded subtree violation (48)` when excluded and exit 0 when permitted; "outside" is exit 0 when excluded and `permitted subtree violation (47)` when permitted:

| Constraint | Within | Outside |
|---|---|---|
| `evil.test` | `user@evil.test`, `USER@EVIL.TEST`, `a@b@evil.test`, `@evil.test` | `user@a.evil.test` |
| `.evil.test` | `user@a..evil.test`, `@..EVIL.test` | `user@evil.test`, `user@a.evil.test`, `..evil.test@` |
| `.` | `user@evil..` | |
| `@evil.test`, `user@evil.test` | | every mailbox |
| empty | `user@` | `user@evil.test` |
| `xn--bcher-kva.test` | `user@bücher.test`, `user@BüCHER.test` | `user@bÜcher.test`, `user@xn--bcher-kva.test` |
| `XN--bcher-kva.test` | `user@xn--bcher-kva.test` | `user@bücher.test` |
| `.xn--bcher-kva.test` | `user@a..bücher.test` | |
| `xn--.test`, `xn--abc-.test` | `user@.test`, `user@abc.test` | |

Either subtree answers `unsupported or invalid name syntax (53)` for a value with no `@`, an IA5String or INTEGER value, and a constraint holding a NUL; and `unspecified certificate verification error (1)` for a constraint that is not Punycode (`xn--!!`), or longer than 255 bytes once decoded, the extra dot of a leading-dot constraint counted (255 characters without a leading dot are compared, 256 are not; with one, 254 are compared, 255 are not). A SmtpUTF8Mailbox under DNS constraints only is not checked.

## Decision
- The OpenSSL build lists the `otherName` alternative names in certificate order. A SmtpUTF8Mailbox is checked against the rfc822Name subtrees (`OpenSslSmtpUtf8Mailbox`); every other `otherName` under a subtree of its type-id is refused with 51.
- Per name, the permitted subtrees are taken before the excluded ones, and the first subtree that matches, or cannot be compared, decides.
- Punycode is decoded by hand (`PunycodeLabel`): `IdnMapping` refuses labels OpenSSL takes, and may differ between ICU and invariant mode.
- Codes 51 and 1 travel as two private bits of `X509ChainStatusFlags` that no platform chain engine sets.
- The Schannel build is unchanged.

## Consequences
- Below an intermediate anchor the OpenSSL build gives curl's exit code and message for each case above.
- Where the platform chain engine decides instead (a root anchor), a refusal flagged only as unsupported is still reported as 53 (ADR-1383), not the 51 measured with a root as `--cacert`; BL-6055 covers that.
