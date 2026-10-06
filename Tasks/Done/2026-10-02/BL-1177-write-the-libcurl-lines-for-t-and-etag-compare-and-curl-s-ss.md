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
completed: 2026-10-02
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

- [x] Measured first with `Record-CurlExchange.ps1`; output copied into Notes.
- [x] `Curl.Cli.UnitTests` (and `Curl.Console.UnitTests` where the console passes the new facts) reproduce each measured output byte for byte, and `WaitingForBl1177` is gone with the enumeration test passing.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.
- [x] No option changes, so `--ai-help` stays as it is (say so in Notes).

## Notes

Measured 2026-10-02, curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Port 18080` with `-s --libcurl -`
(file paths absolute: the script runs curl from another directory). Setopt lines only, `B`=BUFFERSIZE,
`NP`=NOPROGRESS, `UA`=USERAGENT, `MR`=MAXREDIRS 50, `T`=SSLVERSION TLSv1_2 + TCP_KEEPALIVE:

- `-T up.txt` (5 bytes) `http://h:p/`: B, URL `"http://h:p/up.txt"`, NP, `CURLOPT_UPLOAD, 1L`, UA, MR, T,
  `CURLOPT_INFILESIZE_LARGE, (curl_off_t)5` (last line). Empty file: no INFILESIZE. Missing file: UPLOAD,
  no INFILESIZE, exit 26. `-T -`: URL as typed (`HTTP://h:p/a/../d/` kept), UPLOAD, no size.
  Schemeless `h:p` gives `http://h:p/up.txt`; `HTTP://h:p/a/../d/` gives `http://h:p/d/up.txt`
  (= `UploadTransferUrl.TryResolve`).
- imap `-T`: UPLOAD likewise; with `--resolve --disallow-username-in-url --upload-flags answered
  --happy-eyeballs-timeout-ms 5`: ... RESOLVE, HAPPY_EYEBALLS, DISALLOW_USERNAME, UPLOAD_FLAGS 17L, INFILESIZE.
- `-T -f -u a:b -x P -a --oauth2-bearer t`: NP, XOAUTH2_BEARER, PROXY, FAILONERROR, UPLOAD, APPEND, USERPWD.
- `--etag-compare` file `"abc"\n`: `slist1 = curl_slist_append(slist1, "If-None-Match: \"abc\"");`,
  HTTPHEADER after NP. File `"x"\r\nsecond\n` gives `"If-None-Match: \"x\"second"`; empty or missing file
  `"If-None-Match: \"\""`. After a `-H X-A: 1` in the same list. Also written on `ftp://`.
- `-C - -o out.bin` (7 bytes): `CURLOPT_RESUME_FROM_LARGE, (curl_off_t)7` after MR; empty or missing file:
  none. `-C - -T up.txt`: `(curl_off_t)-1`.
- SSH, no known_hosts, no `-k`/fingerprint: `sftp://h:p/f` without `-s` writes B, URL, UA, then
  `curl_easy_cleanup`, `curl = NULL;`, the blank line, `return (int)result;`, `}` and the end comment (the
  end of `main` IS written); stderr `curl: Could not find a known_hosts file` + `curl: (2) Failed
  initialization`, exit 2. A preceding http URL is written whole; the sftp one cut, nothing after.
  `-sS --key k -T up.txt --create-file-mode 0600 sftp://h:p/`: cut after `SSH_PRIVATE_KEYFILE`.
  `-k`: no KNOWNHOSTS line, full transfer. `--hostpubmd5` alone: MD5 line, no KNOWNHOSTS, full transfer.
  With `CURL_HOME` holding `.ssh/known_hosts` and `--key k --hostpubmd5 .. --compressed-ssh`:
  PRIVATE_KEYFILE, MD5, COMPRESSION, `CURLOPT_SSH_KNOWNHOSTS, "<CURL_HOME>\\.ssh/known_hosts"`, SSLKEY.
  `--knownhosts f`: `SSH_KNOWNHOSTS "f"`.

Decisions (ADR-0379): the console records a `LibcurlTransfer` per transfer and fills its facts where it
already opens each file (`UpdateLibcurlTransfer`, indexed through `RunningTransferState.LibcurlTransferIndex`);
the generator cuts at the known-hosts point. The old tuple `Generate` overload stays and maps onto the new one.
The `--request-target` + `-T` order (UPLOAD after REQUEST_TARGET, before DIRLISTONLY) follows curl's
`config2setopts` order; not measured since `-T` with those options is unusual.

Also fixed `CommandLineOptions.LibcurlAcceptsEchMode` (from BL-1172), complexity 16, the one failing member
`Measure-CodeQuality.ps1` reported for the library: now a set lookup and `HasValueAfter`.

Measure-CodeQuality: Curl.Cli.UnitLibrary 100% line, 100% branch, 0 failing members, worst CRAP 10.
No option was added or changed, so `--ai-help` is unchanged.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --libcurl writes -T, --etag-compare, -C - and SSH known-hosts lines as curl 8.21.0 does, and stops where its known-hosts setopt fails
