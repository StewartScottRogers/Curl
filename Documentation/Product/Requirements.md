# Requirements

> **TODO** — functional requirements for the `file://` scheme are authored
> (2026-09-26). The other Phase 1 option groups — HTTP, the command-line layer, and
> output formatting — are not yet.

Each requirement gets a stable identifier so planning, commits and tests can cite
it. Identifiers are never reused or renumbered, even after a requirement is
dropped — mark it `Withdrawn` instead.

## Functional

### The `file://` scheme

Every requirement below was checked against curl 8.21.0 (2026-06-24), the version
`Documentation/Product/Product-Overview.md` measures the compatibility surface
against, using the manpage, `docs/URL-SYNTAX.md` and `libcurl-errors` on
[curl.se](https://curl.se). A requirement marked with an open task describes
upstream behaviour this project has not yet built; it is not a claim that the
behaviour works today. Source code for the current handler lives in
`Curl.Protocol.File.UnitLibrary`.

| ID | Requirement | Upstream reference | Priority | Status | Checked against |
| --- | --- | --- | --- | --- | --- |
| FR-001 | A `file://` URL is accepted with an empty authority (`file:///path`), or an authority of `localhost` (compared ignoring case) or the literal `127.0.0.1`. Any other authority is rejected as exit 3 (`CURLE_URL_MALFORMAT`). | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-002 | An authority that is exactly one ASCII letter followed by `:` or `\|` is not a host at all; it is the head of a Windows drive path, so `file://C:/dir/x` and `file:///C:/dir/x` resolve to the same path and the `\|` spelling is kept, not rewritten to `:`. | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-003 | `file:////server/share` — an empty authority immediately followed by two more slashes — is accepted as the one Universal Naming Convention spelling curl recognises for `file://`, and both leading slashes of `//server/share` are preserved. | [URL syntax](https://curl.se/docs/url-syntax.html) | Should | Draft | curl 8.21.0 |
| FR-004 | Downloading a local file: a plain `file://` request reads the named file and writes its bytes, unaltered, to the transfer's output. A source that cannot be opened for reading is exit 37 (`CURLE_FILE_COULDNT_READ_FILE`), reported the same way whatever the operating-system reason. | [URL syntax](https://curl.se/docs/url-syntax.html); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |
| FR-005 | Uploading to a local file with `-T`/`--upload-file` truncates and (re)writes the destination, unless a positive `-C`/`--continue-at` offset is also given (FR-006). A destination that cannot be opened for writing is exit 23 (`CURLE_WRITE_ERROR`). Resolving a destination URL whose path names no file (it ends in `/` or is absent) by appending the source file's percent-encoded base name exists as `UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile` in `Curl.Cli.UnitLibrary` (encoding: ADR-0004), but nothing calls it yet, because there is no option parser or transfer dispatcher; wiring it in is an open gap for a follow-up task. Until then this requirement covers only a `file://` URL that already names a file. | [`-T`/`--upload-file`](https://curl.se/docs/manpage.html#-T) | Must | Draft | curl 8.21.0 |
| FR-006 | `-C`/`--continue-at`: on download, a positive offset seeks the source and appends to the destination; on upload, a positive offset skips that many bytes of the local source before writing, and the destination is opened in append mode rather than truncated. An offset past the end of a download source is exit 36 (`CURLE_BAD_DOWNLOAD_RESUME`); the same offset past the end of an upload source is not an error. Rejecting a negative or non-numeric `-C` value in the option parser, before any URL is examined, is an open gap (task BL-027). | [`-C`/`--continue-at`](https://curl.se/docs/manpage.html#-C) | Must | Draft | curl 8.21.0 |
| FR-007 | `-r`/`--range` on download selects a byte window of a local file in any of curl's three forms (`first-last`, `first-`, `-suffix`). A first byte position past the end of the file is exit 36 (`CURLE_BAD_DOWNLOAD_RESUME`); a suffix asking for more than one byte more than the file holds is exit 36 with a distinct message from the `-C` case. Refusing `-r` combined with `-C` (exit 2, `CURLE_FAILED_INIT`) and reporting exit 33 (`CURLE_RANGE_ERROR`) for a range that fails to parse are open gaps (task BL-013). | [`-r`/`--range`](https://curl.se/docs/manpage.html#-r) | Must | Draft | curl 8.21.0 |
| FR-008 | `-I`/`--head` suppresses the body of a `file://` transfer but still opens the resource — a directory still fails exactly as it would for a body request — and, when header output was asked for, still writes the pseudo-header block. | [`-I`/`--head`](https://curl.se/docs/manpage.html#-I) | Must | Draft | curl 8.21.0 |
| FR-009 | `-z`/`--time-cond` transfers the body of a local file only when its last-write time meets the given condition (newer than, for a plain date; older than, for a date prefixed with `-`); an unmet condition is a success with no body, exit 0. Comparing at whole-second resolution and strictly in both directions (task BL-017), suppressing the header block for an unmet condition (task BL-016), and treating an unknown source timestamp as "transfers" rather than as the year 0001 (task BL-018) are open gaps. | [`-z`/`--time-cond`](https://curl.se/docs/manpage.html#-z) | Should | Draft | curl 8.21.0 |
| FR-010 | `-i`/`--include` and `-D`/`--dump-header`: a `file://` transfer emits a synthesised header block — `Content-Length`, `Accept-ranges: bytes` and `Last-Modified` — before any body, to whichever stream the caller designated for headers. | [`-i`/`--include`](https://curl.se/docs/manpage.html#-i); [`-D`/`--dump-header`](https://curl.se/docs/manpage.html#-D) | Should | Draft | curl 8.21.0 |
| FR-011 | `-R`/`--remote-time`: the modification time of a downloaded local file, truncated to whole seconds, is available to be applied to the output file. Actually applying the timestamp to a real output file — the value is currently only carried, not applied — is an open gap (task BL-019). | [`-R`/`--remote-time`](https://curl.se/docs/manpage.html#-R) | Should | Draft | curl 8.21.0 |
| FR-012 | `--path-as-is`: a `file://` path has every backslash converted to a forward slash and then, unless `--path-as-is` is given, its `.` and `..` segments removed per RFC 3986 section 5.2.4, before the operating-system path is formed. This is entirely unimplemented today — backslashes and dot segments both survive unresolved (task BL-015). | [`--path-as-is`](https://curl.se/docs/manpage.html#--path-as-is); [URL syntax](https://curl.se/docs/url-syntax.html) | Should | Draft | curl 8.21.0 |
| FR-013 | `--crlf` on upload: uploading to a `file://` destination with `--crlf` converts each line feed to a carriage-return/line-feed pair on the way to the destination; a download ignores the option. Not yet implemented — `ITransferContext` carries no such flag (task BL-020). | [`--crlf`](https://curl.se/docs/manpage.html#--crlf) | Could | Draft | curl 8.21.0 |
| FR-014 | `--create-file-mode` on POSIX: a file created by a `file://` upload receives the given octal mode on POSIX platforms, and the option has no effect on Windows. Not yet implemented (task BL-011). | [`--create-file-mode`](https://curl.se/docs/manpage.html#--create-file-mode) | Could | Draft | curl 8.21.0 |
| FR-015 | `--max-filesize`: a `file://` transfer that would exceed the given size fails with exit 63 (`CURLE_FILESIZE_EXCEEDED`) rather than completing. Not yet implemented (task BL-013). | [`--max-filesize`](https://curl.se/docs/manpage.html#--max-filesize); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Should | Draft | curl 8.21.0 |
| FR-016 | A `file://` URL whose authority is not empty, `localhost`, `127.0.0.1`, or a drive spelling is rejected before any file-system access, as exit 3 (`CURLE_URL_MALFORMAT`). | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-017 | A write failure to a download's destination is reported through `%{errormsg}` as `Failure writing output to destination, passed <n> returned 0`, where `<n>` is the size of the chunk offered to the destination, with exit 23 (`CURLE_WRITE_ERROR`). | [`--write-out`](https://curl.se/docs/manpage.html#-w); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Should | Draft | curl 8.21.0 |
| FR-018 | A failed `file://` transfer reports the number of bytes that reached the destination before the failure, not zero, for `%{size_download}` and `%{size_upload}` once `--write-out` exists. Not yet implemented — a failure currently reports zero bytes regardless of progress made (task BL-022). | [`--write-out`](https://curl.se/docs/manpage.html#-w) | Should | Draft | curl 8.21.0 |
| FR-019 | A `file://` path is handed to the operating system exactly as parsed, with no sandboxing or confinement between the URL and the local file system: a path such as `file:///C:/Windows/win.ini` is reachable like any other. | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |

Priorities use MoSCoW (Must / Should / Could / Won't). "Must" means the release is
not shippable without it — if everything is a Must, nothing is.

## Non-functional

Qualities rather than behaviours. Each one needs a number, or it is not a
requirement but a wish.

| ID | Quality | Target | Status |
| --- | --- | --- | --- |
| NFR-001 | > **TODO** (e.g. throughput, latency, footprint) | measurable target | Draft |

## Out of scope

Requirements considered and explicitly rejected, with the reason. Keeping them
here stops them being re-proposed every few months.

## Open questions

Unresolved points that block requirements from leaving `Draft`. Each should name
who can answer it.
