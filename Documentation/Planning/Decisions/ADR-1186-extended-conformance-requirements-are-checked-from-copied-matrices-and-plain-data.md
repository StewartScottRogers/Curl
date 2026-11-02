# ADR-1186: Extended conformance requirements are checked from copied matrices and plain data

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4502 (split from BL-4462)
- Decided by Claude under Stewart's delegation.

## Context

Each extended protocol's conformance tests map to its specification through a requirement
matrix, `Documentation/Conformance/Extended/<Name>.md`, and `[SpecRequirement("<id>")]` on the
test methods. Some cases are known to fail until a task fixes them, listed in
`Curl.Conformance.Extended.UnitTests/KnownFailures.txt`. BL-4462 set the rules; this ADR records
how the harness carries them out.

## Decision

- `SpecRequirementAttribute`, `RequirementMatrix` and `KnownFailures` live in
  `Curl.Conformance.Extended.UnitLibrary/Harness`. The checks take plain data
  (`SpecRequirementCarrier`: a test's `Class.Method` and the IDs it carries), never an
  assembly: the reflection that collects the carriers stays in the test project
  (`RequirementMatrixTests.CarriersInThisAssembly`), so the library stays free of reflection and
  AOT-clean, and every branch is covered by tests over hand-built data.
- The test project copies every `Documentation/Conformance/Extended/*.md` to
  `Conformance/Extended/` in its output and `KnownFailures.txt` beside it; the checks read the
  output, never the repository. Matrices are found by file name, the protocol name is the file
  name, and a missing folder holds no matrices, so the check is green before any matrix exists.
- A matrix row is any line starting with `|` other than the header (first cell `ID`) and the
  separator (first cell of dashes). A missing cell reads as empty, which then fails as a row naming
  no test. A carried ID counts only in the matrix its prefix (before the first `:`) names.
- `KnownFailures.RunCaseAsync(ids, runCase)` runs a case: unlisted, as is; listed, any exception
  passes the case, and success throws `KnownFailurePassedException` with
  "<id> passes now: remove its KnownFailures.txt line (BL-####)". A malformed or repeated line and a
  listed ID no test carries are problems of the list's own check.
- `KnownFailures.txt` is `merge=union` and in `Tasks/SharedListFiles.txt`, like
  `PassingUpstreamCases.txt`, so lanes adding protocols add lines side by side.

## Consequences

A protocol task adds its matrix and tests and gets the check for free, with no registry to edit.
Catching every exception in a listed case also passes a case that fails for a new reason; the
list names a task for each line, which bounds how long that can hide.
