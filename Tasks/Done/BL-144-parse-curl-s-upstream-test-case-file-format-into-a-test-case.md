---
id: BL-144
title: Parse curl's upstream test-case file format into a test-case model
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-148]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
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

- [x] `UpstreamTestCaseParser.Parse(ReadOnlySpan<byte>)` returns an `UpstreamTestCase`; tests in
      `Curl.Conformance.UnitTests/UpstreamTestCaseParserTests.cs` cover each section listed in
      Context and each attribute, from inline test-file text.
- [x] A section body containing `<` and `&` that is not a tag survives byte for byte (tested).
- [x] A malformed file (an unclosed section) returns a parse failure naming the section and line,
      not an exception (tested).
- [x] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10 per
      method, per `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

Delivered in `Curl.Conformance.UnitLibrary`: `UpstreamTestCaseParser.Parse(ReadOnlySpan<byte>)`
returns an `UpstreamTestCaseParseResult` holding an `UpstreamTestCase` (parts as
`UpstreamTestSection`: section, name, attributes, body bytes, opening line) or an
`UpstreamTestCaseParseFailure` (section path, line, message). `UpstreamTestSectionLineEndings`
holds the attribute transforms. Tests: `UpstreamTestCaseParserTests`,
`UpstreamTestSectionTests`, `UpstreamTestSectionLineEndingsTests` (102 tests in the project).

Decisions, taken under Stewart's delegation (rule: match upstream curl 8.21.0), each checked
against `FILEFORMAT.md`, `getpart.pm`, `runtests.pl` and `testutil.pm` at `curl-8_21_0`:

- **Bodies are kept as written; attributes are exposed, and applied by named transforms rather
  than at parse time.** The Goal said "with the section attributes applied", but upstream applies
  `nonewline`, `crlf` and `mode="text"` only where it uses a part, after variable substitution
  (so `%CR` counts), and in a different order per part (`<verify><stdout>` chomps then forces
  CRLF; `<verify><file>` forces CRLF, strips, then chomps; `<verify><upload>` chomps, forces CRLF
  and chomps again). Applying once at parse time would give wrong bytes. The transforms are
  `CutFinalNewline` (Perl `chomp`), `ForceCrlf` (upstream's CR-run-plus-LF to CRLF substitution, per line),
  `ForceHeaderCrlf` (`crlf="headers"`, `subnewlines`' header guess) and `NormalizeText`
  (`mode="text"`), each tested. The comparison stage calls them in each part's upstream order.
- **`base64="yes"` and `hex="yes"` are not implemented: curl 8.21.0 has no such attributes.**
  Its `FILEFORMAT.md` documents only `%b64[...]b64%` and `%hex[...]hex%` preprocessing macros;
  `getpart.pm` decodes nothing. Filed as BL-151. `crlf="headers"`, which Context did not list, is
  implemented because 8.21.0 uses it on data, stdout, stderr, protocol and file parts.
- **Attribute truth follows Perl** (`IsAttributeSet`): present and not empty or `0`, so
  `nonewline="no"` is on, as upstream's `if($hash{'nonewline'})` has it.
- **Tag lines follow `getpart.pm`**: `^ *<name[ >]` / `^ *</name[ >]`, text after the tag
  ignored, double- or single-quoted attributes whose name is everything before `=`. A body line
  opening the part's own name nests; a line closing the enclosing section inside a part is a
  failure (upstream reports "missing </part> tag before </section>").
- `crlf="headers"` state starts cleared per body; upstream keeps it in a module variable across
  parts. `%if` lines outside a part are ignored, so BL-145 should expand the file's bytes before
  parsing (noted on BL-145).
- No `Regex`: the source-generated regex code is compiled into the library and counted by the
  coverage gate, so tags and header lines are recognised by hand.


## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. UpstreamTestCaseParser parses curl 8.21.0 test files into sections with raw bodies and attributes; UpstreamTestSectionLineEndings applies nonewline, crlf and mode=text as upstream does
