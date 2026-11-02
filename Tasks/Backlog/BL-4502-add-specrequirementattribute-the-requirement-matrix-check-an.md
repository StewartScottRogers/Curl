---
id: BL-4502
title: Add SpecRequirementAttribute, the requirement-matrix check and the KnownFailures.txt check to the extended-protocol conformance harness
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4501]
touches: [Curl.Conformance.Extended.UnitLibrary/Harness/SpecRequirementAttribute.cs, Curl.Conformance.Extended.UnitLibrary/Harness/RequirementMatrix.cs, Curl.Conformance.Extended.UnitLibrary/Harness/KnownFailures.cs, Curl.Conformance.Extended.UnitTests/Harness/RequirementMatrixTests.cs, Curl.Conformance.Extended.UnitTests/Harness/KnownFailuresTests.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, .gitattributes, Tasks/SharedListFiles.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4502 — Add SpecRequirementAttribute, the requirement-matrix check and the KnownFailures.txt check to the extended-protocol conformance harness

## Goal

The extended-protocol conformance harness has `SpecRequirementAttribute`, a matrix check over `Documentation/Conformance/Extended/<Name>.md` and a known-failures check over `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`, each tested both ways.

## Context

Split from BL-4462; read BL-4462 (in `Tasks/Deferred`) for the rules. Requirement IDs are `<Name>:<source>-<locator>` (`Mqtt5:MQTT-3.1.2-3`, `Coap:RFC7252-5.4.1-1`); a test method carries `[SpecRequirement("<id>")]` (repeatable). Matrices: one Markdown table per file, rows `| ID | Level | Requirement | Covered by |`, Covered by a `Class.Method` or `n/a: <reason>`, copied into the test output as content so the check reads no repository file at run time. Matrix check: every row's test exists and carries that ID; every `[SpecRequirement]` ID appears in its protocol's matrix; no ID twice; a matrix with no rows fails. Known failures: one `<requirement id> BL-####` per line, `#` comments, order-free; a listed case passes while it still fails and fails with "remove its KnownFailures.txt line" once it passes; a listed ID that no test carries fails the check. Matrices are found by file name, with no central registry. The check stays green while no protocol matrix exists yet.

## Acceptance criteria

- [ ] `SpecRequirementAttribute`, the matrix check and the known-failures check exist in `Harness/` and are tested both ways (a passing listed case fails with the "remove its KnownFailures.txt line" message; an unmapped matrix row fails; a duplicate ID fails; an empty matrix fails).
- [ ] `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` exists (comments only), is marked `merge=union` in `.gitattributes` and listed in `Tasks/SharedListFiles.txt`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral.

## Notes

## Log

- 2026-11-02: Created.
