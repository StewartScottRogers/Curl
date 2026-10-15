# ADR-0531 — The `-F` parts map onto `EasyHttpOptions.MimePost`, which the engine's form mapping reads

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2344.

## Context

ADR-0527 built `EasyOptionsFromCommandLine.HttpOf` without the `-F` body. BL-2344 maps the `-F` /
`--form-string` parts onto `EasyHttpOptions.MimePost` as curl 8.21.0's tool builds its `curl_mime`
tree (`tool_formparse.c`, and the lines `--libcurl`'s `MimeLines` writes), and has the engine's
`MultipartFormPartMapping` build the body from that record instead of `CommandLineOptions.FormParts`,
with behaviour byte for byte unchanged. Two things `FormPartSpecification` says looked missing from
the record: whether a file part is `name=@file` or `name=<file`, and whether it reads standard input.

## Decision

- **`@file` and `<file` differ by `EasyMimePart.FileName`, as libcurl's part does.** `curl_mime_filedata`
  sets the part's file name to the path's base name, and the tool clears it with
  `curl_mime_filename(part, NULL)` for `<file`. So `HttpOf` sets `FileName` to the `;filename=` value or
  else the base name (after the last `/` or `\`, as `Curl.Core`'s `MultipartPartHeaders` takes it) for
  `@file`, and leaves it `null` for `<file`. A file part with no file name is file content; one with a
  file name is an upload. `EasyMimePart` needs no new member.
- **Standard input is a new data kind, `EasyMimeStandardInput`.** For `@-` and `<-` the tool sets a
  `curl_mime_data_cb` part reading `stdin` (`--libcurl` writes `fread`/`fseek` on `stdin`, size -1);
  `@-` keeps the file name `-` unless `;filename=` names it, `<-` none. The record's
  `EasyMimeCallback` would need a live delegate, and reading standard input into `EasyMimeBytes` while
  mapping options would read it when the options are mapped rather than when the body is built, after
  the transfer is checked. `EasyMimeStandardInput` names the part's source and
  the engine's builder reads it when the body is built, as it does now.
- **Text goes as UTF-8 bytes** (`EasyMimeBytes`), and the engine decodes them as UTF-8 before the
  builder encodes the text in the platform's command-line encoding, as before. A .NET string survives
  the UTF-8 round trip unchanged except a lone surrogate, which both encodings send as a replacement
  character, so the bytes on the wire are unchanged.
- `name=(` ... `=)` is a part with `SubParts` (empty `EasyMimeBody` for an empty multipart) and no
  data; `@a,b` is such a part holding one upload per file. `;type=`, `;encoder=` and `;headers=` go to
  `Type`, `Encoder` and `Headers` as parsed. `MimePost` is `null` without `-F` or `--form-string`.
- `MultipartFormPartMapping.FromMimeBody` replaces `FromCommandLine`. An `EasyMimeCallback` part, which
  no command line produces, is refused with `NotSupportedException`. `NameEscapingOf` (`--form-escape`
  and the mail schemes) and `RequestContentTypeOf` (the `-H Content-Type` lookup) still read
  `CommandLineOptions`, and `--libcurl`'s `MimeLines` still reads `FormParts`; both agree with the
  record's shape above.

## Consequences

The runner builds `MimePost` with `HttpOf` at each transfer and hands it to the form builder.
`Curl.Api` and `Libcurl` can hand the engine a `curl_mime` tree of bytes, files and standard input;
callback parts wait for the engine's builder to read a `ReadCallback`.
