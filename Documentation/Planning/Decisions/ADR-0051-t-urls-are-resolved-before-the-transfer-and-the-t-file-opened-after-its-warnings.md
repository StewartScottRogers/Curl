# ADR-0051 — `-T` URLs are resolved before the transfer, and the `-T` file opened after its warnings

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-030, 2026-09-27).

## Context

BL-012 left `UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile` uncalled, and nothing parsed
`-T` / `--upload-file`. BL-030 had to decide how `-T` values pair with URLs, where the URL of an
upload is parsed and normalised, in what order the steps of a `-T` transfer run, and what each
failure prints.

Measured on 2026-09-27 with the Windows reference build (`/mingw64/bin/curl`, curl 8.21.0
x86_64-w64-mingw32, Schannel), `local.txt` existing and `nosuchfile` not:

| Command | Standard error | Exit | `%{url_effective}` |
| --- | --- | ---: | --- |
| `-T nosuchfile 'http://h/d ir/'` | `curl: (3) URL using bad/illegal format or missing URL` | 3 | (empty) |
| `-T nosuchfile 'http://h/d ir/x'` | the same | 3 | |
| `-T - 'http://h/d ir/'` | `curl: (3) URL rejected: Malformed input to a URL function` | 3 | |
| `-T nosuchfile http://127.0.0.1:1/d/` | `curl: cannot open 'nosuchfile'`, `curl: try 'curl --help' or 'curl --manual' for more information`, `curl: (26) Failed to open/read local data from file/application` | 26 | `http://127.0.0.1:1/d/nosuchfile` |
| the same with `-s` | the first two lines only | 26 | |
| `--capath x -T nosuchfile 'http://h/d ir/'` | the `(3)` line only, no `--capath` warning | 3 | |
| `--capath x -T nosuchfile http://127.0.0.1:1/d/` | the two `--capath` warning lines, then the three lines above | 26 | |
| `--capath x -D /nonexist/zz -T nosuchfile http://127.0.0.1:1/d/` | `curl: Failed to open …/nonexist/zz`, `curl: (23) …` | 23 | |
| `-T nosuchfile -T local.txt URL1/ URL2/` | the three lines; URL2 is not transferred | 26 | |
| `-T local.txt URL1/ URL2/` | URL2 is fetched unchanged, with no upload | | `URL2/` |
| `-T '' -T local.txt URL1/ URL2/` | URL1 unchanged with no upload, `local.txt` to URL2 | | `URL1/`, `URL2/local.txt` |
| `-T local.txt http:/127.0.0.1:1` | | | `http://127.0.0.1:1/local.txt` |
| `-T local.txt 127.0.0.1:1/dir/` | | | `http://127.0.0.1:1/dir/local.txt` |
| `-T local.txt HTTP://User:pw@127.0.0.1:80/d/` | | | `http://User:pw@127.0.0.1:80/d/local.txt` |
| `-T local.txt http://127.0.0.1:1/a/../d/` | | | `http://127.0.0.1:1/d/local.txt` |
| `-T local.txt 'http://[fe80::1%25eth0]:1/d/'` | | | `http://[fe80::1%25eth0]:1/d/local.txt` |

## Decision

1. **Pairing.** `CommandLineOptions.UploadFiles` keeps every `-T` value in order; the Nth goes to
   the Nth URL wherever each was typed, a URL past the end uploads nothing, and an empty value
   keeps its place and uploads nothing. A value that looks like a flag is kept with curl's
   `looks like a flag` warning.
2. **Resolution lives in `Curl.Cli`.** `UploadTransferUrl.TryResolve` appends the file name with
   `UploadUrl`, parses the result with `CurlUrl` (never path-as-is, as curl's
   `add_file_name_to_url` does not pass `CURLU_PATH_AS_IS`) and writes it back out from the
   `CurlUrl` parts. `UploadUrl` itself stays pure string work. Standard input (`-` or `.`) is not
   resolved.
3. **Order of a `-T` transfer** in `CurlCommandRunner`: resolve the URL (failure: exit 3 with
   `URL using bad/illegal format or missing URL`, before any warning line, and an empty
   `%{url_effective}`); open the `-D` file; print the before-transfer warning lines; open the `-T`
   file through the runner's `IFileSystem` (failure: `curl: cannot open '<file>'` and the try-help
   line even under `-s`, then exit 26 with `Failed to open/read local data from file/application`
   under the usual `-s`/`-S` rule, and the run stops); then the transfer as before, with the
   opened file, or standard input, as `ITransferContext.Upload`.
4. The resolved URL is the one the transfer parses, `%{url_effective}` prints and the `-c` jar
   check reads.

## Consequences

- `CurlUrl` has no whole-URL writer, so the one in `UploadTransferUrl` omits a typed port equal
  to the scheme's default (curl keeps `:80`), and writes the host as `CurlUrl.Host` holds it,
  percent-decoded. Both change only the text of `%{url_effective}`, not the request.
- Sending the upload is each handler's work: `file` and `tftp` already read
  `ITransferContext.Upload`; HTTP PUT is BL-184. Globbing the `-T` value is BL-031.
