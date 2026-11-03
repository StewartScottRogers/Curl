# ADR-0002: The client ends with exit 8 on an unsupported version

Fixture for Find-AdrNumberMismatches.ps1 -SelfTest. Nothing here is real.

## Decision

- An unsupported version ends the transfer: the client exits 9 (`CURLE_WEIRD_SERVER_REPLY`).
- A missing capability exits 8.
