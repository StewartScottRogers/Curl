# Planted defects

The audit office measures each auditor by planting known defects in a throwaway copy of the code
before an audit and counting how many each auditor finds: its **catch rate** (ADR-0267). An
auditor that misses its planted defects has its findings flagged unreliable on the scorecard.

**Auditors must never read this folder** - not the catalogue, not this README beyond this line.
`Audit/Instructions/Auditor-Rules.md` forbids it, and the seeder deletes the folder from the tree
the auditors audit. Dark factory lanes cannot read it either: the PreToolUse hook refuses them
every audit path.

## What is here

- `Catalogue.md.b64` - the catalogue, base64-encoded so that its text never appears in a search
  of the repository (BL-1028). It lists at least three defects per auditor (quality, security,
  performance, conformance, truthfulness, process), each with an `id` (`PD-###`), the auditor
  and finding kind it tests, what the defect is, how to plant it in general terms (a pattern to
  find and a change to make, so it still applies as the code moves on), whether the solution
  still builds, and how a correct finding is recognised.
- `Read-PlantedCatalogue.ps1` - prints the decoded catalogue. `-Encode <file.md>` rewrites
  `Catalogue.md.b64` from an edited copy; edit the decoded copy outside the repository, never
  commit it decoded.

## How it is used

The `audit-seeder` agent (`.claude/agents/audit-seeder.md`) decodes the catalogue, picks the
defects for one audit with a seed - at least one per auditor - and plants them in a detached
worktree at the audited commit (process defects go in a copy of the log folder, never the real
one). It deletes `Audit/PlantedDefects/`, `Audit/Findings/` and `Audit/Scorecards/` from that
worktree, commits it as `Audit baseline` on no branch, and writes a manifest of what it planted
outside the worktree, where only the scorecard (BL-1017) reads it.
