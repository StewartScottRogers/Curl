---
id: BL-023
title: Verify or correct the two unmeasured file:// failure messages
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-008, BL-021]
requirement: none
created: 2026-09-25
completed:
---
# BL-023 — Verify or correct the two unmeasured `file://` failure messages

## Goal

`FileTransferMessages.ReadFailed` and `FileTransferMessages.DestinationWriteFailed` either
match what the curl 8.21.0 binary prints, or are replaced by what it does print, and each
one records how it was established.

## Context

`Curl.Protocol.File.UnitLibrary\FileTransferMessages.cs` carries two strings taken from
libcurl's `curl_easy_strerror` table rather than from an observed run:

- `ReadFailed = "Failed to open/read local data from file/application"` (exit 26,
  `CURLE_READ_ERROR`)
- `DestinationWriteFailed = "Failed writing received data to disk/application"` (exit 23,
  `CURLE_WRITE_ERROR`)

The conformance audit could not provoke exit 26 from the binary for a `file://` transfer,
so both are unverified — and tests in `Curl.Protocol.File.UnitTests` assert them verbatim,
which means the solution is now pinned to text nobody has seen curl print. Severity is
Minor, but a pinned guess is worse than an unpinned one because it looks settled. The
strings themselves are the documented `strerror` text
(<https://curl.se/libcurl/c/libcurl-errors.html>), and what is unverified is whether the
curl tool prints that text, or something more specific, in these two situations.

## Acceptance criteria

- [ ] Each of the two messages has an attempt recorded in its XML documentation: the exact
      curl 8.21.0 command line tried, the output observed (including the
      `curl: (<n>) <message>` line), and the date — or a statement that no invocation of
      the binary reaches that path for `file://`, with what was tried.
- [ ] Where the observed text differs from the current constant, the constant is changed
      to the observed text and every test asserting it is updated; where nothing could be
      observed, the constant is left as the documented `strerror` text and its
      documentation says explicitly that it is the `curl_easy_strerror` fallback, not a
      measured line.
- [ ] A test in `Curl.Protocol.File.UnitTests` asserts each message verbatim through a
      handler failure path — `ErrorMessage` on the `TransferResult`, not the constant read
      directly — so the assertion survives a rename.
- [ ] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Two invocations worth trying first, both local and harmless:

- exit 26 on the upload side: `curl -T <a path that cannot be read> file:///C:/dir/out.txt`
- exit 23 on the upload side: an upload destination on a volume with no free space, or a
  destination path whose directory is removed between the open and the write

Depends on BL-021, which rewrites `OutputWriteFailed` in the same file; that message is
already measured and is not in scope here.

If it turns out the tool never prints either line for `file://`, that is a valid result:
record it, keep the `strerror` text, and say so. Do not invent a message to make the tests
look decisive.

## Log

- 2026-09-25: Created.
