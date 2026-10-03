# ADR-0379: --libcurl writes the transfer-file lines and stops where the known-hosts setopt fails

- Status: Accepted
- Date: 2026-10-02
- Decided by Claude under Stewart's delegation (BL-1177)

## Context
BL-1174 left `-T`, `--etag-compare`, `-C -` and the SSH known-hosts file out of `LibcurlSourceCode`: their
lines depend on what only the console learns per transfer (the upload file's size, the compare file's
contents, the output file's size, the known-hosts file found). curl 8.21.0 (mingw, Schannel) was measured on
2026-10-02 with `Record-CurlExchange.ps1` and `--libcurl -` (BL-1177 Notes).

## Decision
- The console notes each transfer as a `LibcurlTransfer` and fills in its file facts as it learns them;
  `LibcurlSourceCode.Generate` writes from those facts. The text stays the Schannel build's on every
  platform (ADR-0326).
- `-T`: `CURLOPT_URL` is the upload URL `UploadTransferUrl` resolves (the URL as given for standard input
  or a URL that does not parse), `CURLOPT_UPLOAD` follows `REQUEST_TARGET` and precedes `DIRLISTONLY`,
  and `CURLOPT_INFILESIZE_LARGE` is the transfer's last line when the opened file is not empty.
- `--etag-compare`: its `If-None-Match` lines join `CURLOPT_HTTPHEADER` after the `-H` and `--json` ones,
  on every scheme, as curl writes them.
- `-C -`: `CURLOPT_RESUME_FROM_LARGE` is `-1` on an upload, and the output file's size on a download
  when that is not zero.
- SSH: the known-hosts file is `CURLOPT_SSH_KNOWNHOSTS`, after `--compressed-ssh`'s line. Without one, and
  without `-k`, `--hostpubmd5` or `--hostpubsha256`, curl's tool fails setting it with exit 2: the source
  stops after the SSH lines before it, without that transfer's list of options or `curl_easy_perform`, and
  no later transfer is written - but the cleanup and end of `main` are, as measured.

## Consequences
- Variables declared for lines after the cut (for instance a `--resolve` list on the failing transfer) are
  still declared and freed; curl would not declare them. Not measured, and rare enough to leave.

## Alternatives considered
- Stopping the file mid-`main`, as BL-1177's Context guessed: the measurement shows curl writes the end.
- Probing the files inside `Curl.Cli`: the library has no file system, and the console already opens each
  file at the point curl does.
