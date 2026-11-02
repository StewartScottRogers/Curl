---
id: BL-4543
title: Conformance: AMQP 1.0 reference container, split decoder and encoder under the quality gates (BL-4527 part 1 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceDecoder.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceEncoder.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceDescribed.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceDescriptors.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceSymbol.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceFrame.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceSession.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceLinkRefusal.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/IAmqp1ReferenceLinkBehaviour.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceContainer.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceContainerOptions.cs, Curl.Conformance.Extended.UnitTests/Amqp1, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-02
completed:
---
# BL-4543 — Conformance: AMQP 1.0 reference container, split decoder and encoder under the quality gates (BL-4527 part 1 of 2)

## Goal

The reference AMQP 1.0 container's type decoder and encoder in `Curl.Conformance.Extended.UnitLibrary/Amqp1/` pass the quality gates, and the stashed container, cases and matrix of BL-4527 land.

## Context

Split from BL-4527 (over its cost cap). The stashed work holds the whole container, `Amqp1ProtocolHeaderCases.cs`, `Amqp1SaslCases.cs`, `Amqp1ReferenceContainerTests.cs` and `Documentation/Conformance/Extended/Amqp1.md`; all 121 tests in `Curl.Conformance.Extended.UnitTests` passed. What is left is the quality gate: `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary` failed `Amqp1ReferenceDecoder` Sized (complexity 16, branch 87.5%), Read (14), WidthOf (13, branch 92.3%), Variable (branch 80%, no test for an unknown 0xa? code), and `Amqp1ReferenceEncoder` Write (14), WriteSymbolArray (12), WriteNonNumeric (12). Split the switches into smaller methods or lookup tables and add the missing tests to `Amqp1ReferenceContainerTests.cs`. The container keeps decoding only the format codes a client sends (plus symbol arrays); do not widen it. Container.Accepts and AnswerAsync are the next task (BL-4544).

## Acceptance criteria

- [ ] `Amqp1ReferenceDecoder` and `Amqp1ReferenceEncoder` have every method at complexity at most 10, CRAP at most 30, and 100% line and branch coverage from the fast tests (`Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary`).
- [ ] `Amqp1ReferenceContainerTests.cs` has a test for each previously uncovered branch (Sized, WidthOf, Variable with an unknown 0xa? code).
- [ ] `dotnet test Curl.Conformance.Extended.UnitTests --filter "TestCategory!=Integration"` is green, the matrix check included.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; no package added; every test is platform-neutral.

## Notes

- Split from BL-4527.
- Continues the stashed work of BL-4527.

## Log

- 2026-11-02: Created.
