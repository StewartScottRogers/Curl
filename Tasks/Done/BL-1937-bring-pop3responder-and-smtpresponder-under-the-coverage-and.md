---
id: BL-1937
title: Bring Pop3Responder and SmtpResponder under the coverage and complexity gates
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1937 — Bring Pop3Responder and SmtpResponder under the coverage and complexity gates

## Goal

Every failing member of Pop3Responder.AnswerByDefault, SmtpResponder.AnswerByDefault (complexity 56-72), SmtpResponder.Mail/IsAddressCharacter/Data/IsAddress/Recipient/Verify, and the Pop3 and Smtp IsBase64Line lambdas is under 100% branch coverage and complexity of at most 10.

## Context

Split from BL-1936 (BL-1929's measurement, 2026-10-09). Split methods (a lookup table of commands, say) and add the missing branch tests; never raise a threshold. Measure once with `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary -ReportPath <file>` and read the report.

## Acceptance criteria

- [x] The members named in the Goal are absent from the failing list of `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Replaced the two AnswerByDefault switches with per-instance command tables (OrdinalIgnoreCase), split TrySplitCommand, SMTP IsAddress, Mail, Recipient and Verify into helpers, and added branch tests (base64 `+` and `/`, MAIL with other parameters, DATA before any hello, 5-letter top-level domain, SMTPUTF8 with a non-address ASCII character). Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary no longer lists any Pop3Responder or SmtpResponder member. The Imap IsBase64Line lambda and the other members still failing belong to other tasks.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Pop3Responder and SmtpResponder pass the coverage and complexity gates
