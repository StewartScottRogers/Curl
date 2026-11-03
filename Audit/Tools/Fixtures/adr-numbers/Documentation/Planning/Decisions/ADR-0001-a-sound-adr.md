# ADR-0001: A sound ADR ends with exit 8

Fixture for Find-AdrNumberMismatches.ps1 -SelfTest. Nothing here is real.

## Decision

- The client exits 8 (`CURLE_WEIRD_SERVER_REPLY`) on a reply it cannot read.
- `CurlExitCode.CouldntConnect` (7) is not this ADR's, and CURLE_COULDNT_RESOLVE_HOST (6) neither.
