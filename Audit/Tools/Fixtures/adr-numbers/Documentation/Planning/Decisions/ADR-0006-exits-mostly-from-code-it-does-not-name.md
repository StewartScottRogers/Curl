# ADR-0006: Blocks are written through a sink

Fixture for Find-AdrNumberMismatches.ps1 -SelfTest. Nothing here is real.

## Decision

- `IBlockSink` writes each block that `BlockPlanner` sizes; a sink that fails ends the transfer with exit 6,
  and a refused block with exit 9, both raised by code this ADR does not name.
- A length the planner cannot read fails with exit 8.
