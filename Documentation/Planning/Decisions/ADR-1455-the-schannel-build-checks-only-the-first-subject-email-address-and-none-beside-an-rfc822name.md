# ADR-1455: The Schannel build checks only the first subject emailAddress, and none beside an rfc822Name (BL-6026)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1422.

## Context
Fitness finding STD-229: the Schannel build checked every subject emailAddress against a partial-chain anchor's name constraints, so a leaf curl accepts was refused. Measured with curl 8.21.0 (Schannel, Windows), a loopback TLS server, `--ssl-no-revoke`, the constrained intermediate given as `--cacert`, and a leaf with subjectAltName `DNS:localhost, IP:127.0.0.1` plus the addresses shown. "Bad" is `evil@other.org`; each row gave the same answer under excluded `other.org` (`schannel: error 0x00008000`) and under permitted `example.com` (`schannel: error 0x00004000`):

| Subject emailAddresses, in encoding order | rfc822Name alternative names | Answer |
|---|---|---|
| good, bad (a relative name each, before or after the CN) | none | exit 0 |
| bad, good | none | exit 60 |
| good, bad in one relative name (DER puts the shorter first) | none | exit 0 |
| bad, longer good in one relative name | none | exit 60 |
| good beside `O` in one relative name, then bad | none | exit 0 |
| bad alone | none | exit 60 |
| bad | good | exit 0 |
| good | bad | exit 60 |
| none | good, bad | exit 60 |

A leaf with no dNSName and two subject CNs, one outside the DNS constraint, was exit 60 in either order, so every CN is still checked.

## Decision
- The Schannel build checks the first subject emailAddress in encoding order and no later one.
- It checks no subject emailAddress when the subjectAltName holds an rfc822Name. RFC 5280 §4.2.1.10 applies rfc822Name constraints to the subject's address only in that case, and curl on Windows does the same.
- The OpenSSL build still checks every subject emailAddress (ADR-1387).

## Consequences
- On Windows a certificate may carry an address outside the constraints after the first, or beside an rfc822Name, and be accepted, as curl accepts it. Matching the platform's curl is the standing rule.
- ADR-1422's reading of multi-valued relative names stands; its "every subject emailAddress" no longer holds for the Schannel build.
