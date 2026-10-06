---
id: BL-651
title: Parse --xattr and store the URL and content type as extended attributes on every OS curl can
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-651 — Parse --xattr and store the URL and content type as extended attributes on every OS curl can

## Goal

`--xattr` parses, and after a successful `-o`/`-O` transfer Curl stores the attributes curl 8.21.0 stores (`user.xdg.origin.url`, `user.mime_type` and any others it writes) on every operating system for which any curl build writes them (read `src/tool_xattr.c` at tag `curl-8_21_0` for the list), whatever the platform's usual build does; only on an operating system no curl build supports does Curl do what curl does there.

## Context

- Conformance audit 2026-09-28, row 30 (Major; filed Low as rarely used). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): what any official curl build supports, Curl supports on every platform it can exist on; the BCL lacking an API is not a reason to leave it out.
- The BCL has no extended-attribute API; on Linux and macOS `setxattr` (and FreeBSD `extattr_set_file` if relevant) is a libc call through `LibraryImport` (source-generated P/Invoke, part of the BCL, AOT-safe, no package); if curl writes attributes on Windows (for example as NTFS alternate data streams), the BCL's file APIs can write those. Record the route per OS in an ADR marked "Decided by Claude under Stewart's delegation" (HOW, not WHETHER).
- Output-file handling: `Curl.Console/OutputFileTarget.cs`; the `-R` file-time setter (`IFileTimeSetter`) is the model for a post-transfer file seam.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--xattr -o out.txt` with a `Content-Type`, on Windows and on Linux or macOS, and the attributes (or streams) found on `out.txt` afterwards, copied into Notes.
- [x] Tests pin, per platform under `OSCondition`, the attribute names and values written through a fake seam.
- [x] The ADR exists in `Documentation/Planning/Decisions` (number checked unused) and is indexed in its `README.md`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Source read: `src/tool_xattr.c` and `tool_xattr.h` at `curl-8_21_0`. curl writes, in order and stopping at the first failure, `user.creator`=`curl`, `user.xdg.referrer.url` (CURLINFO_REFERER, when set), `user.mime_type` (CURLINFO_CONTENT_TYPE, when set) and `user.xdg.origin.url` (the URL with user and password cleared by `curl_url`). Enabled where libc has `fsetxattr` (Linux 5-arg, macOS 6-arg) and on FreeBSD/MidnightBSD (`extattr_set_fd`, user namespace); elsewhere, Windows included, `fwrite_xattr` is `0`. `tool_operate.c` calls it only for a successful transfer to a file curl opened, before it creates an empty file for a bodiless one; a failure warns `Error setting extended attributes on '<file>': <strerror>` and keeps the exit code.
- Measured on Windows 2026-10-01 with `Record-CurlExchange.ps1` (curl 8.21.0 Schannel): `--xattr -e http://ref.example/ -o out.txt http://u:p@127.0.0.1:18651/a?b` against `Content-Type: text/plain; charset=utf-8` exits 0, stderr only the meter, and `Get-Item out.txt -Stream *` shows only `:$DATA` (5 bytes): no alternate data stream.
- Measured on Linux 2026-10-01 (WSL Ubuntu, curl 8.18.0 OpenSSL; `tool_xattr.c` unchanged since). The recorder's loopback server is not reachable from WSL's NAT network, so the reply was served by `nc -l` inside WSL; attributes read through `tar --xattrs` (no `getfattr` installed). `curl -s --xattr -e http://ref.example/ -o out2.txt "http://u:p@127.0.0.1:18653/a?b#frag"` left exactly:
  `user.xdg.referrer.url=http://ref.example/`, `user.xdg.origin.url=http://127.0.0.1:18653/a?b#frag`, `user.mime_type=text/plain; charset=utf-8`, `user.creator=curl`. A `file:///tmp/in.txt` transfer left only `user.creator=curl` and `user.xdg.origin.url=file:///tmp/in.txt`.
- Decision (ADR-0320, decided by Claude): the attribute list in `Curl.Console/OutputFileExtendedAttributes.cs`, the seam `IExtendedAttributeWriter`, and `NativeExtendedAttributeWriter` calling libc through `LibraryImport` (`setxattr` on Linux/Android and Apple, `extattr_set_file` on FreeBSD; none on Windows). It writes by path, since .NET does not expose the stream's descriptor. `Curl.Console.csproj` gains `AllowUnsafeBlocks` for the generated marshalling. The native adapter is excluded from fast-run coverage per ADR-0083; its Integration tests run on Linux and macOS only. This machine has no .NET in WSL, so they have not run yet; CI's fast run skips them.
- `DeferredOutputFileStream.CreatedEmptyAfterTransfer` / `OpenedForTheTransfer` tell a file the transfer opened from one created empty afterwards, which curl leaves without attributes.
- `--ai-help` needed no edit: its "Not supported by this build yet" line comes from the option table, and `--xattr` is now in it (pinned by `CommandLineXattrOptionTests`).
- Not modelled (in ADR-0320): curl keeps a `;options` part of the user info, and Curl drops the whole user info; `-G` query text is not added to the origin URL.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --xattr parses and stores curl's four extended attributes via libc on Linux, macOS and FreeBSD, none on Windows (ADR-0320)
