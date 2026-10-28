---
id: BL-3968
title: CGR-110 part 1: add ClosedStandardInputStream and make the data file reader throw for it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3570, BL-3893]
touches: [Curl.Cli.UnitLibrary/DiskDataFileReader.cs, Curl.Cli.UnitLibrary/ClosedStandardInputStream.cs, Curl.Cli.UnitLibrary/ConfigFileApplier.cs, Curl.Cli.UnitTests/Cgr110ClosedStandardInputStreamTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3968 — CGR-110 part 1: add ClosedStandardInputStream and make the data file reader throw for it

## Goal

A closed standard input is represented by a new public Curl.Cli.ClosedStandardInputStream, and DiskDataFileReader.ReadStandardInput throws IOException for it, with no change to what any command line does yet.

## Context

Split from BL-3639 (CGR-110, Documentation/Reviews/Curl-Gap-Report-2026-10-25.md: closed stdin with @- is read as empty and the request is sent; real curl 8.22.0 exits 26 for -d/--data-binary/--json and 42 for -F). Read BL-3639's Notes for the measurements and the decided plan.

## Acceptance criteria

- [ ] A unit test in Curl.Cli.UnitTests/Cgr110ClosedStandardInputStreamTests.cs shows ClosedStandardInputStream reads as end of file and DiskDataFileReader.ReadStandardInput throws IOException for it, and still reads a normal stream and Stream.Null as empty.
- [ ] ConfigFileApplier (-K -) reads a ClosedStandardInputStream as empty, pinned by a unit test.
- [ ] dotnet build is clean and dotnet test --filter "TestCategory!=Integration" passes, with the touched library at 100% line and branch coverage.

## Notes

- Split from BL-3639. Continues the stashed work of BL-3639.
- Program.cs is NOT changed here (BL-3969 swaps Stream.Null for the new stream), so nothing observable changes until that task lands. Record the ADR (Decided by Claude under Stewart's delegation) for the whole CGR-110 plan here.

## Log

- 2026-10-28: Created.
