---
id: BL-250
title: Expand the %b64[...]b64% and %hex[...]hex% test-file macros
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-145]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-250 — Expand the %b64[...]b64% and %hex[...]hex% test-file macros

## Goal

The test-case preprocessing expands `%b64[...]b64%` to the base64 encoding of its contents and
`%hex[...]hex%` to the bytes its `%XX` codes name, as `runtests.pl` does at `curl-8_21_0`.

## Context

- Found while doing BL-144: curl 8.21.0's test files have no `base64="yes"` or `hex="yes"`
  part attributes (BL-144's Context listed them); binary and encoded content comes from these two
  preprocessing macros instead. See "Base64 Encoding" and "Hexadecimal decoding" in
  https://github.com/curl/curl/blob/curl-8_21_0/docs/tests/FILEFORMAT.md.
- Both run in the preprocess stage, after variable substitution (BL-145), so `%b64[%HTTPPORT %9a]b64%`
  encodes the substituted port. Inside `%b64[...]`, `%XX` is a byte; inside `%hex[...]`, bytes
  that are not `%XX` are copied as they are.
- Base class library only: `Convert.ToBase64String`, `Convert.FromHexString`.

## Acceptance criteria

- [ ] Tests in `Curl.Conformance.UnitTests` show `%b64[%HTTPPORT %9a]b64%` expanding to the base64 of the
      substituted port, a space and byte 0x9a, and `%hex[%00%01%FF]hex%` expanding to bytes 00 01 FF.
- [ ] Text that is not a complete macro is left as written (tested).
- [ ] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10 per
      method, per `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-09-26: BL-145 implemented `%b64[...]b64%` and `%hex[...]hex%` in `UpstreamTestInstructions`
  (applied by `UpstreamTestFileExpander`), with tests that meet every criterion above:
  `Expand_EncodesBase64AfterSubstitutingVariablesAndPercentPairs`,
  `Expand_DecodesHexAndKeepsOtherCharactersAsWritten`, `Expand_LeavesAnUnclosedInstructionAsWritten`.
  Running this task should only need to verify those, tick the boxes and move it to Done.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
