---
id: BL-144
title: Parse curl's upstream test-case file format into a test-case model
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-142]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-144 — Parse curl's upstream test-case file format into a test-case model

## Goal

`UpstreamTestCaseParser` turns the bytes of one upstream test file into an `UpstreamTestCase`
exposing its sections as bytes, with the section attributes applied.

## Context

- ADR-0013, decision 2. The format is documented in curl's `docs/tests/FILEFORMAT.md` at
  `curl-8_21_0` (https://github.com/curl/curl/blob/curl-8_21_0/docs/tests/FILEFORMAT.md).
- It is not well-formed XML: section bodies are raw bytes that may contain `<` and `&`, so
  `System.Xml` cannot read it. Hand-parse with the base class library; no parser package.
- The sections the harness needs first: `<info><keywords>`, `<reply><data>` (and `data1`…`dataN`),
  `<reply><servercmd>`, `<client><server>`, `<client><features>`, `<client><tool>`,
  `<client><name>`, `<client><command>`, `<client><file name=…>`, `<client><stdin>`,
  `<verify><protocol>`, `<verify><stdout>`, `<verify><stderr>`, `<verify><errorcode>`,
  `<verify><strip>`, `<verify><strippart>`, `<verify><file name=…>`.
- Attributes: `nonewline="yes"`, `crlf="yes"`, `base64="yes"`, `hex="yes"`, `mode="text"`.
- Variables and `%if` blocks are left untouched here; BL-145 handles them.

## Acceptance criteria

- [ ] `UpstreamTestCaseParser.Parse(ReadOnlySpan<byte>)` returns an `UpstreamTestCase`; tests in
      `Curl.Conformance.UnitTests/UpstreamTestCaseParserTests.cs` cover each section listed in
      Context and each attribute, from inline test-file text.
- [ ] A section body containing `<` and `&` that is not a tag survives byte for byte (tested).
- [ ] A malformed file (an unclosed section) returns a parse failure naming the section and line,
      not an exception (tested).
- [ ] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10 per
      method, per `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-26: Created.
