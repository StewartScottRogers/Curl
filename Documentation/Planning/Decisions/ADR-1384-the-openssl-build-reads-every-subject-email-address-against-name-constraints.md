# ADR-1384: The OpenSSL build reads every subject emailAddress against name constraints (BL-5940)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1383.

## Context
ADR-1383 left one case open: a subject emailAddress in a multi-valued relative name was not checked against name constraints. Measured with curl 8.18.0 and OpenSSL 3.5.5 (WSL Ubuntu, `openssl s_server`, subject CN `localhost` last, under a constrained intermediate given as `--cacert`; the same with the root as `--cacert`):

| Subject, before `CN=localhost` | Email constraint | Answer | `%{ssl_verify_result}` |
|---|---|---|---|
| `emailAddress=evil@other.org+OU=Unit` | excluded `other.org` | exit 60, `excluded subtree violation (48)` | 48 |
| the same | permitted `example.com` | exit 60, `permitted subtree violation (47)` | 47 |
| the same | permitted `other.org` | exit 0 | 0 |
| `emailAddress=a@example.com+emailAddress=evil@other.org` | excluded `other.org` | exit 60, `excluded subtree violation (48)` | 48 |

## Decision
- The OpenSSL build's name constraint check reads every subject emailAddress, multi-valued relative names included, as OpenSSL's `NAME_CONSTRAINTS_check` does.
- The Schannel build's answers do not change: it still reads an emailAddress of a single-valued relative name only.
