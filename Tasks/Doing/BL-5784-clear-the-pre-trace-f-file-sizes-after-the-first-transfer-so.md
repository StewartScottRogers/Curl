---
id: BL-5784
title: Clear the pre-trace -F file sizes after the first transfer so a later transfer declares a shrunk file at its new size
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Console.UnitTests/FormFileSizedBeforeTraceTests.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5784 — Clear the pre-trace -F file sizes after the first transfer so a later transfer declares a shrunk file at its new size

## Goal

The `-F` file sizes taken before the trace file opens (AF-0982, BL-5779) apply only to the run's first transfer, so a later URL, glob match or `--next` group sizes each form file as it is when that transfer builds its form, as curl 8.22.0 does.

## Context

Found by the shift-end review of 2026-11-22 (ADR-0710; silent-failure-hunter, type-design-analyzer and code-reviewer all reported it).

- `Curl.Console/CurlCommandRunner.cs:1876-1894` (`SizeFormFilesBeforeTraceFileOpensAsync`) records the sizes once, before the first URL, and stores them on the runner-wide `formBodyBuilder` (`FileSizesBeforeTrace`). Every later call returns early because `eventStandardError` is set, and nothing ever clears the map.
- `Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs:421` (`SizeBeforeTrace`) keeps declaring the recorded size for every later build whenever the file is now smaller.
- Failure: `curl --trace t -o a.txt http://a/ --next -F f=@a.txt http://b/` where `a.txt` held 100 bytes and the first transfer writes 10 into it. The second transfer declares 100 bytes, reads short and exits 26; real curl sizes the part when it sets up that transfer and succeeds. The same applies to `-F f=@x -o x URL1 URL2`.
- The public mutable `{ get; set; }` also breaks the builder's constructor/`init` pattern: the value's lifetime is "the first build only" but its shape says "until replaced". Consider passing the snapshot to the one build that needs it, or clearing it once the first transfer's body is built. Recording only parts whose path is `options.TraceFile` would also stop every `-F` file (a FIFO, a device) being opened up front.

## Acceptance criteria

- [ ] After the run's first transfer has built its form, no later transfer of the same command line declares a form file at a size recorded before the trace file opened.
- [ ] A unit test in `Curl.Console.UnitTests` runs two transfers under `--trace` where the first shrinks a file the second sends with `-F`, and asserts the second declares the file's new length and exits 0.
- [ ] `RunAsync_TraceFileIsTheFormFile_DeclaresTheSizeTheFileHadBeforeTheTrace` still passes.
- [ ] `dotnet build` is clean and the fast tests are green; the touched libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: b9f4bda4 c06a16e8 872c54fe 708e5fec.
