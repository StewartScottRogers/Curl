# ADR-1422: The Schannel build reads multi-valued relative names against name constraints (BL-6007)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1387. Amended by ADR-1452.

## Context
Fitness finding STD-161: the Schannel build checked a subject emailAddress and CN against a partial-chain anchor's name constraints only when it had a relative name to itself, so a refused address beside another attribute was accepted. Measured with curl 8.21.0 (Schannel, Windows), a loopback TLS server, `--ssl-no-revoke` and the constrained intermediate given as `--cacert`:

| Leaf subject | subjectAltName | Constraint | Answer |
|---|---|---|---|
| `emailAddress=evil@other.org+CN=localhost` | `DNS:localhost` | excluded email `other.org` | exit 60, `schannel: error 0x00008000` |
| `O=x+emailAddress=evil@other.org, CN=localhost` | `DNS:localhost` | the same | exit 60, `schannel: error 0x00008000` |
| `emailAddress=good@example.com+CN=localhost` | `DNS:localhost` | the same | exit 0 |
| `emailAddress=evil@other.org+CN=localhost` | `DNS:localhost` | permitted email `example.com` | exit 60, `schannel: error 0x00004000` |
| `emailAddress=good@example.com+CN=localhost` | `DNS:localhost` | the same | exit 0 |
| `O=x+CN=localhost` | none | excluded DNS `localhost` | exit 60, `schannel: error 0x00008000` |

## Decision
- The Schannel build's name constraint check reads every subject emailAddress, and every subject CN of a leaf with no dNSName, multi-valued relative names included. Both builds now read the same attributes.

## Consequences
- ADR-1383's and ADR-1387's reading of multi-valued relative names no longer differs between the builds.
