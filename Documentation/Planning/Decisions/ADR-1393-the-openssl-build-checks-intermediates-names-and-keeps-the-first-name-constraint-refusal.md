# ADR-1393: The OpenSSL build checks intermediates' names, and keeps the first name-constraint refusal (BL-5953)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1391.

## Context
`CertificateNameConstraints` checked the server certificate's names only. With a self-signed
`--cacert` root, .NET's chain engine flags the chain's name-constraint refusals itself, and
`OpenSslVerifyResult` answered 47 whenever 47 and 48 were both flagged. The chain engine keeps
status flags per element with no order among them, so the order cannot be read from it. Measured
with curl 8.18.0 and OpenSSL 3.5.5 (WSL Ubuntu, `openssl s_server`, root, intermediate and a leaf
with SAN `DNS:localhost`). Each refusal was exit 60.

| Root | Intermediate | `%{ssl_verify_result}`, root as `--cacert` | intermediate as `--cacert` |
|---|---|---|---|
| excludes email `bad.org` | subject emailAddress `ca@bad.org`, permits DNS `other.org` | 48 | 47 |
| permits email `good.org` | subject emailAddress `ca@bad.org`, excludes DNS `localhost` | 47 | 48 |
| excludes DNS `localhost` | permits DNS `other.org` | 48 | 47 |
| permits DNS `other.org` | excludes DNS `localhost` | 47 | 48 |
| excludes email `bad.org` | SAN `email:ca@bad.org` | 48 | 0 |
| subject equals the intermediate's, excludes email `bad.org` | self-issued, subject emailAddress `ca@bad.org` | 0 | 0 |
| excludes DNS `bad.example.com` | subject CN `bad.example.com`, no SAN | 0 | 0 |

## Decision
- The OpenSSL build checks the certificates from the top of the chain (or the listed anchor) down,
  skipping a self-issued one unless it is the server's own, each against the CAs above it from the
  top down, as OpenSSL's `check_name_constraints` does. An intermediate's names are taken in the
  order ADR-1391 gives the leaf's; subject CNs are checked for the server certificate only.
- When the chain's status flags a subtree refusal (47 or 48), alone or beside other name-constraint
  flags, the OpenSSL build evaluates the chain itself and keeps only the flag for the first refusal
  (amended by BL-5954: the Linux chain engine may flag another CA's refusal than the one OpenSSL
  stops at, so even a single subtree flag is not trusted). A chain with no subtree flag, or one in
  which that evaluation finds nothing (a constraint kind it does not read), keeps the engine's flags.
- The Schannel build's answers do not change: it checks the server certificate's names only.
