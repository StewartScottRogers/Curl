---
id: BL-023
title: Verify or correct the two unmeasured file:// failure messages
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-008, BL-021]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-25
completed: 2026-09-26
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

- [x] Each of the two messages has an attempt recorded in its XML documentation: the exact
      curl 8.21.0 command line tried, the output observed (including the
      `curl: (<n>) <message>` line), and the date — or a statement that no invocation of
      the binary reaches that path for `file://`, with what was tried.
- [x] Where the observed text differs from the current constant, the constant is changed
      to the observed text and every test asserting it is updated; where nothing could be
      observed, the constant is left as the documented `strerror` text and its
      documentation says explicitly that it is the `curl_easy_strerror` fallback, not a
      measured line.
- [x] A test in `Curl.Protocol.File.UnitTests` asserts each message verbatim through a
      handler failure path — `ErrorMessage` on the `TransferResult`, not the constant read
      directly — so the assertion survives a rename.
- [x] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and
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

### Outcome (2026-09-26, dark factory lane 3)

Both messages were measured with the curl 8.21.0 binary (`C:\Program Files\Git\mingw64in\curl.exe`)
on Windows. The trick that reaches the after-the-open paths: another process holds a byte-range
lock (`FileStream.Lock`) on the file, so the open succeeds and a later `read()`/`write()` fails.

| Case | Command | Observed |
| --- | --- | --- |
| Upload source read fails, known length | `curl -sS -T src.txt file:///Z:/repos/Curl.lanes/bl023tmp/out.txt` (100000 bytes, 99000+ locked) | `curl: (26) client read function EOF fail, only 65536/100000 of needed bytes read` |
| Same, whole file locked; with `-C 10`; with `--crlf` | as above | `only 0/100000`; `only 65536/100000` (size_upload 65526); same line |
| Upload source read fails, stdin | a locked file piped into `curl -T -`, and a 5-byte pipe with `-C 10` | exit 0, nothing written |
| Upload destination write fails | `curl -sS -T src.txt file:///.../dest.txt` (dest locked from 0 or 99000; also `-a`) | `curl: (55) Failed sending data to the peer` |
| Download source read fails | `curl -sS -o dl.txt file:///.../src.txt` (locked) | exit 0, empty `%{errormsg}`, body ends at the failure |

Neither `strerror` string is printed on these paths. Changes, all inside the File library:

- `ReadFailed` is replaced by `UploadSourceReadFailed(read, needed)`, exit 26, used only for an
  upload source of known length (`CanSeek`); `read` counts bytes skipped by `-C` as curl does.
- `DestinationWriteFailed` is now `Failed sending data to the peer` and the exit code is 55
  (`SendError`), not 23. Both failures carry the bytes written, matching `%{size_upload}`.
- A download read failure and an unknown-length upload read failure (including during the `-C`
  skip) end the transfer as a success with what was moved.

Choices taken without Stewart (rule 1), and why:

- Exit codes changed along with the messages (23 -> 55 on destination writes; 26 -> 0 on
  download reads). A measured message under the wrong exit code would still be a wrong
  `curl: (n) ...` line, and every change sits in this task's `touches`.
- "Known length" is `Stream.CanSeek`. curl knows the length of a `-T` file and not of `-T -`;
  a seekable redirected stdin would be treated as known here, an edge left as is.
- `read` in the message counts in this handler's 16384-byte chunks; curl reads uploads in
  65536-byte chunks, so the number matches curl only on 64 KiB boundaries. Recorded in the XML
  docs; left as follow-up rather than widening this task.
- Pre-existing uncovered branches found by the coverage run, not touched here:
  `FileProtocolHandler.cs` (the `count < 0 || length == 0` clamp) and `FileUrlPath.cs` lines 39
  and 249. Every line added by this task is covered.

## Log

- 2026-09-25: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. file:// upload read and destination write failures now report curl 8.21.0's measured exit 26/55 lines; download read failures end the body with exit 0
