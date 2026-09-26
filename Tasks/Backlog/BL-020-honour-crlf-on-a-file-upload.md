---
id: BL-020
title: Honour --crlf on a file:// upload
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008]
requirement: none
created: 2026-09-25
completed:
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

- [ ] `ITransferContext.ConvertLineEndings` exists, `bool`, documented as `--crlf` with
      the measured `file://` behaviour and a note that it applies to uploads only;
      `FakeTransferContext` in `Curl.Protocol.File.UnitTests\Fakes` exposes it as a
      settable property defaulting to `false`.
- [ ] A test named `ExecuteAsync_UploadWithCrlf_ConvertsEveryLineFeed` uploads the bytes
      `a\nb\n` and asserts the destination holds exactly `a\r\nb\r\n`
      (`FakeFileSystem.WrittenBytes`).
- [ ] A test asserts an upload with `ConvertLineEndings` false is byte-for-byte unchanged,
      including a file containing `\n` and a file containing `\r\n`.
- [ ] The already-`\r\n` case is measured against the curl 8.21.0 binary, the command and
      its result recorded in this task's `Notes`, and pinned by a named test.
- [ ] The chunk-boundary case is measured the same way and pinned by a named test that
      uses a file larger than 16384 bytes with a `\r` as the last byte of the first chunk
      and a `\n` as the first byte of the second, so the conversion state has to survive
      across chunks.
- [ ] `TransferResult.BytesTransferred` agrees with upstream for the `a\nb\n` case: run
      `curl -T in.txt --crlf file:///C:/dir/out.txt -w "%{size_upload}"` under curl
      8.21.0, record the number, and assert it in the first test above.
- [ ] A download ignores `ConvertLineEndings`: a test asserts a `file://` download of
      `a\nb\n` with the flag set writes `a\nb\n` to `Output` unchanged.
- [ ] ADR-0003 records the sixth member, why it is on the shared contract and not
      `file`-specific, and the measured `file://` behaviour, dated, citing curl 8.21.0.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` and
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

## Log

- 2026-09-25: Created.
