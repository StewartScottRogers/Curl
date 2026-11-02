---
id: BL-4546
title: Conformance: AMQP 1.0 reference container, split Accepts and AnswerAsync and verify the whole of BL-4527 (part 2 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4545]
touches: [Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceContainer.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceContainerOptions.cs, Curl.Conformance.Extended.UnitTests/Amqp1, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-02
completed:
---
# BL-4546 — Conformance: AMQP 1.0 reference container, split Accepts and AnswerAsync and verify the whole of BL-4527 (part 2 of 2)

## Goal

The reference AMQP 1.0 container's `Accepts` and `AnswerAsync` pass the quality gates, and every acceptance criterion of BL-4527 is verified and met.

## Context

Split from BL-4527 (over its cost cap); follows BL-4545, which lands the container and brings the decoder and encoder under the gates. Remaining gate failures of `Amqp1ReferenceContainer`: Accepts (complexity 12) and AnswerAsync (complexity 12, branch 83.3%). Split them into smaller methods and add tests to `Amqp1ReferenceContainerTests.cs`. Then verify the rest of BL-4527 as filed: `Documentation/Conformance/Extended/Amqp1.md` (every client MUST/MUST NOT/SHOULD/SHOULD NOT of 2.2 and 5.3 has an `Amqp1:` ID mapped to a test or `n/a: <reason>`), the header and SASL cases (`Amqp1ProtocolHeaderCases.cs`, `Amqp1SaslCases.cs`) for `amqp1://` and `amqp1s://`, and `KnownFailures.txt` (a failing case is fixed or listed with a filed High `Fix Amqp1 conformance: ...` task; none failed so far). Follow `Documentation/Conformance/Extended/README.md`, ADR-1186 and ADR-1188.

## Acceptance criteria

- [ ] `Amqp1ReferenceContainer` and every other type under `Curl.Conformance.Extended.UnitLibrary/Amqp1/` has every method at complexity at most 10, CRAP at most 30, and 100% line and branch coverage (`Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary`).
- [ ] The container lets BL-4467 add transfer behaviour in `Amqp1/Messaging/` and BL-4528/BL-4529 choose frame size, idle timeout, errors and encoding widths without editing its files (options and `IAmqp1ReferenceLinkBehaviour` carry them).
- [ ] `Documentation/Conformance/Extended/Amqp1.md` is in the matrix format with every client MUST, MUST NOT, SHOULD and SHOULD NOT of 2.2 and 5.3 given an `Amqp1:` ID and mapped to a test or `n/a: <reason>`; the harness matrix check passes.
- [ ] Cases for `amqp1://` and `amqp1s://` assert wire bytes, stdout, stderr and exit code for both protocol headers (AMQP and SASL), a server answering with another header, SASL PLAIN from `-u`, ANONYMOUS without it, and a SASL outcome other than ok.
- [ ] Every failing case is fixed or listed in `KnownFailures.txt` with the ID of its filed High `Fix Amqp1 conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`; no package added; every test is platform-neutral.

## Notes

- Split from BL-4527.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 6b771b0d 42a3c661 fcf5f5d2 7cd1715d 0f4dc4be 00203731.
