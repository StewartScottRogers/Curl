---
id: BL-1177
title: Write the --libcurl lines for -T and --etag-compare, and curl's SSH known-hosts failure
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1174]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1177 — Write the --libcurl lines for -T and --etag-compare, and curl's SSH known-hosts failure

## Goal

`--libcurl` writes curl 8.21.0's lines for `-T` and `--etag-compare`, so `WaitingForBl1177` in `LibcurlSourceCodeOptionCoverageTests` is empty and deleted, and an SSH transfer without a host key hash behaves as curl's Schannel build does.

## Context

- Split from BL-1174, which wrote every other waiting option. These need what only the console knows per transfer: which `-T` file a transfer uploads and its size, and the `--etag-compare` file's contents. `LibcurlSourceCode.Generate` takes `(CommandLineOptions, string Url)` per transfer from `CurlCommandRunner.RecordLibcurlTransfer`, before the upload URL is resolved.
- Measured 2026-10-02 (BL-1174 Notes), curl 8.21.0 mingw Schannel: `-T up.txt http://127.0.0.1:1/` writes `CURLOPT_URL "http://127.0.0.1:1/up.txt"` (the file name appended as `UploadUrl` does; `/d/` gives `/d/up.txt`, no slash gives `/up.txt`, `/d` keeps `/d`) and `CURLOPT_UPLOAD, 1L` right after `CURLOPT_NOPROGRESS`; with an existing file curl also writes `CURLOPT_INFILESIZE_LARGE` (measure its place). On `imap://` it writes `UPLOAD` the same way. `--etag-compare up.txt` adds an `If-None-Match:` header from the file to `CURLOPT_HTTPHEADER` (`"If-None-Match: \"\""` when the file is missing; measure an existing file).
- SSH: on `sftp://`/`scp://` without `--hostpubmd5`/`--hostpubsha256` and without `-k`, the Schannel build fails setting `CURLOPT_SSH_KNOWNHOSTS`: it exits 2 and the `--libcurl` output stops after the lines before the SSH block (`--create-file-mode 0600 sftp://127.0.0.1:1/f` writes BUFFERSIZE, URL, NOPROGRESS, USERAGENT and nothing more, not even the end of `main`). Measure the whole file and stderr, decide by ADR-0326 (Schannel on every platform) what Curl does, and pin it.
- `-C -` with an existing output file writes `RESUME_FROM_LARGE` with the file's size (BL-1106 Notes); BL-1106 and BL-1174 write only an explicit offset. Include it here: it is the same kind of file knowledge.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`; output copied into Notes.
- [ ] `Curl.Cli.UnitTests` (and `Curl.Console.UnitTests` where the console passes the new facts) reproduce each measured output byte for byte, and `WaitingForBl1177` is gone with the enumeration test passing.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.
- [ ] No option changes, so `--ai-help` stays as it is (say so in Notes).

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
