---
id: BL-020
title: Honour --crlf on a file:// upload
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Documentation/Planning/Decisions/ADR-0003-itransfercontext-carries-transfer-options.md, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-25
completed: 2026-09-26
---
# BL-020 — Honour `--crlf` on a `file://` upload

## Goal

`ITransferContext` can express `--crlf`, and a `file://` upload converts line feeds to
carriage-return line-feed pairs on the way to the destination, as curl 8.21.0 does.

## Context

Measured on this machine against curl 8.21.0 (Release-Date 2026-06-24): `--crlf` applies
to `file://`. Uploading a file whose bytes are `a\nb\n` with
`curl -T in.txt --crlf file:///C:/dir/out.txt` leaves `out.txt` holding
`a\r\nb\r\n`. `curl --help all` documents it as `--crlf  Convert LF to CRLF in upload`,
and the manpage as "Convert line feeds to carriage return plus line feeds in upload"
(<https://curl.se/docs/manpage.html>).

Nothing on `Curl.Protocol.Abstractions.UnitLibrary\ITransferContext.cs` carries the flag,
so the handler cannot see it. This extends the shared contract — the sixth transfer option
after the five ADR-0003 added — and so touches
`Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Protocol.File.UnitLibrary` (the upload
copy path in `UploadIntoAsync`/`CopyAsync`), both `.UnitTests` projects, and ADR-0003.

The member name is fixed by this task so the tests, the ADR and any later handler agree:
`bool ConvertLineEndings { get; }`, documented as `--crlf`.

Two behaviours are **not** yet measured and must be measured during this task rather than
guessed, because upstream tracks a preceding carriage return rather than blindly doubling:

- input already holding `a\r\nb`, uploaded with `--crlf`
- a line feed that lands on a 16384-byte chunk boundary, with the carriage return in the
  previous chunk

## Acceptance criteria

- [x] `ITransferContext.ConvertLineEndings` exists, `bool`, documented as `--crlf` with
      the measured `file://` behaviour and a note that it applies to uploads only;
      `FakeTransferContext` in `Curl.Protocol.File.UnitTests\Fakes` exposes it as a
      settable property defaulting to `false`.
- [x] A test named `ExecuteAsync_UploadWithCrlf_ConvertsEveryLineFeed` uploads the bytes
      `a\nb\n` and asserts the destination holds exactly `a\r\nb\r\n`
      (`FakeFileSystem.WrittenBytes`).
- [x] A test asserts an upload with `ConvertLineEndings` false is byte-for-byte unchanged,
      including a file containing `\n` and a file containing `\r\n`.
- [x] The already-`\r\n` case is measured against the curl 8.21.0 binary, the command and
      its result recorded in this task's `Notes`, and pinned by a named test.
- [x] The chunk-boundary case is measured the same way and pinned by a named test that
      uses a file larger than 16384 bytes with a `\r` as the last byte of the first chunk
      and a `\n` as the first byte of the second, so the conversion state has to survive
      across chunks.
- [x] `TransferResult.BytesTransferred` agrees with upstream for the `a\nb\n` case: run
      `curl -T in.txt --crlf file:///C:/dir/out.txt -w "%{size_upload}"` under curl
      8.21.0, record the number, and assert it in the first test above.
- [x] A download ignores `ConvertLineEndings`: a test asserts a `file://` download of
      `a\nb\n` with the flag set writes `a\nb\n` to `Output` unchanged.
- [x] ADR-0003 records the sixth member, why it is on the shared contract and not
      `file`-specific, and the measured `file://` behaviour, dated, citing curl 8.21.0.
- [x] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` and
      `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` are clean, and
      `dotnet test --filter "Category!=Integration"` is green across the solution — adding
      an interface member must not leave another project failing to compile.

## Notes

Conversion belongs on the upload path only, and the implementation must not read the whole
upload into memory: `CopyAsync` already moves 16384-byte chunks, and the converted output
of a chunk can be up to twice its size.

Every other `IProtocolHandler` will see the new member. Per ADR-0003 each protocol must
state whether it applies; for `file` the answer is "applies to uploads", and stating it
for the `file` upload path is BL-025.

### Measurements (2026-09-26, curl 8.21.0 at `C:\Program Files\Git\mingw64\bin\curl.exe`)

Run from `C:\crlfprobe` (a path without spaces: curl rejects an unencoded space in a
`file://` URL with exit 3) as `curl -s -T inN.txt --crlf file:///C:/crlfprobe/outN.txt -w "%{size_upload}"`:

| Input | Destination | `size_upload` |
| --- | --- | --- |
| `a\nb\n` | `a\r\nb\r\n` | 6 |
| `a\r\nb` | `a\r\nb` (unchanged) | 4 |
| `a\r\r\nb\rc\n\n` | `a\r\r\nb\rc\r\n\r\n` | 11 |
| 16383 × `x`, `\r`, `\n`, 10 × `y` (16395 bytes; `\r` is byte 16384) | unchanged | 16395 |
| `a\nb\n` without `--crlf` | `a\nb\n` | 4 |

Rule: a CR is inserted before an LF only when the byte immediately before that LF is not a
CR; a lone CR is untouched; `size_upload` counts converted bytes.

### Choices made unattended

- **Byte count is the converted count.** `TransferResult.BytesTransferred` reports bytes
  written, matching the measured `size_upload` of 6. `CopyAsync` now tracks bytes read
  (for the download window) apart from bytes written (for the result); on every path
  without `--crlf` the two are equal, so no existing behaviour moved.
- **Conversion as a per-chunk function passed to `CopyAsync`**, via a new internal
  `CrlfUploadConverter` holding a reused buffer of twice the chunk size and the
  previous-byte-was-CR flag. It is tested through `FileProtocolHandler` (the library has
  no `InternalsVisibleTo`, and `FileTransferMessages` is tested the same way). A fresh
  converter per upload; bytes skipped by `-C` are not seen by it (resume plus `--crlf`
  was not measured and is not in this task's criteria).
- **Only two implementers of `ITransferContext` exist** (`TransferContext` and the File
  fake), both inside `touches`, so no other project needed an edit. `TransferContext`
  gained an `init` property and `TransferContextTests` pins its default and round trip.
- The acceptance criterion's `Category!=Integration` filter is read as
  `TestCategory!=Integration`, the MSTest spelling CLAUDE.md uses.
- Requirement FR-013 updated with the measured rule; command-line parsing of `--crlf` is
  still not done and says so.

### Verification

`dotnet build` 0 warnings 0 errors; both `-warnaserror` library builds clean;
`dotnet format --verify-no-changes` clean; `dotnet test --filter "TestCategory!=Integration"`
green: 520 tests across 7 projects (File 180, Abstractions 63).

## Log

- 2026-09-25: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. A file:// upload with ConvertLineEndings (--crlf) converts LF to CRLF as curl 8.21.0 does, across chunk boundaries, and reports the converted byte count
