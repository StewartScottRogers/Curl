# ADR-0005: Blocks stay within the limit

Fixture for Find-AdrNumberMismatches.ps1 -SelfTest. Nothing here is real.

## Decision

- `BlockPlanner.SizeFor` grows the block until the upload fits; an upload that cannot fit fails with exit 6
  before the first block.
- A length the planner cannot read fails with exit 8.

## Consequences

`BlockPlanner` pins both paths.
