# ADR-0320 — `--xattr` stores curl's four attributes through libc where a curl build does

- **Status:** Accepted
- **Date:** 2026-10-01
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-651: curl 8.21.0's `--xattr` stores metadata about a transfer as extended attributes of its
output file. `src/tool_xattr.c` at `curl-8_21_0` writes, after a successful transfer to a file
curl opened (`config->xattr && outs->fopened && outs->stream`, before an empty file is created
for a transfer that wrote nothing), in this order, stopping at the first failure:

1. `user.creator` = `curl`
2. `user.xdg.referrer.url` = `CURLINFO_REFERER`, when set
3. `user.mime_type` = `CURLINFO_CONTENT_TYPE`, when set
4. `user.xdg.origin.url` = the transfer's URL with `curl_url` user and password cleared

`tool_xattr.h` enables it where libc has `fsetxattr` (Linux, five arguments; macOS, six) and on
FreeBSD and MidnightBSD (`extattr_set_fd`, user namespace). Elsewhere `fwrite_xattr` is `0`: the
option is accepted and nothing is written. Windows has no curl build that writes them, Schannel
or otherwise; no alternate data stream is written. A failure prints
`Warning: Error setting extended attributes on '<file>': <strerror>` and the exit code is kept.

Measured 2026-10-01 (BL-651 Notes): curl 8.21.0 (Windows, Schannel) leaves only `::$DATA` on
the file; curl 8.18.0 (Ubuntu, OpenSSL; `tool_xattr.c` unchanged since) leaves exactly the four
attributes above, the origin URL without `u:p@` and with its query and fragment.

The BCL has no extended-attribute API.

## Decision

- `--xattr` (negatable) sets `CommandLineOptions.ExtendedAttributes`.
- `Curl.Console`'s `OutputFileExtendedAttributes` lists the four attributes in curl's order from
  the URL as `UrlEffective` normalises it (curl re-serialises it through `curl_url_get`), the
  `Referer` the last request sent (as `%{referer}` has it) and the report's content type, and
  `stripcredentials` is the removal of everything up to the authority's last `@`.
- The writer is a seam, `IExtendedAttributeWriter`, given to `CurlCommandRunner`.
  `NativeExtendedAttributeWriter.ForCurrentPlatform` picks the libc call curl's build makes for the
  running OS through source-generated `LibraryImport` (BCL, AOT-safe, no package): `setxattr` with
  five arguments on Linux and Android, with six on Apple systems, and `extattr_set_file` in the user
  namespace on FreeBSD; it gives none on Windows and any other OS, where curl writes nothing. It
  writes through the path rather than the descriptor, because .NET does not expose a stream's
  descriptor; the path names the same open file. The failure text is
  `Marshal.GetPInvokeErrorMessage`, which is `strerror` off Windows.
- The native adapter is excluded from fast-run coverage under ADR-0083's rule for thin system
  adapters; its Integration tests run where it can, on Linux and macOS. `Curl.Console` sets
  `AllowUnsafeBlocks` for the generated marshalling.

## Consequences

- Scripts on Linux, macOS and FreeBSD get the attributes curl writes; on Windows `--xattr` is
  accepted and silent, as in curl.
- Not modelled: curl keeps a `;options` part of the user info (it clears only user and password);
  Curl drops the whole user info. `-G` query text is not in the origin URL Curl writes.

## Alternatives considered

- NTFS alternate data streams on Windows: no curl build writes them, so Curl would differ from the
  platform's curl.
- A package for extended attributes: against the BCL-only rule, and `LibraryImport` suffices.
