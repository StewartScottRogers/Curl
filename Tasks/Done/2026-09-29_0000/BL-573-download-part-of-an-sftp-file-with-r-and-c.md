---
id: BL-573
title: Download part of an SFTP file with -r and -C
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-569]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-573 — Download part of an SFTP file with -r and -C

## Goal

`-r first-last`, `-r first-`, `-r -suffix` and `-C <n>`/`-C -` on an SFTP download read only the requested bytes (with `FSTAT` for the size where needed), and a range or offset past the end fails with the exit code and message curl 8.21.0 gives.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-569. `ITransferContext.Range`/`RangeText`/`ResumeFrom` are already there (`ByteRangeParser` in `Curl.Core.UnitLibrary` parses `-r`).
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: each range form, a range past the end, `-C 3`, `-C -` with an existing shorter output file, `-C` past the end.

## Acceptance criteria

- [x] Measured first as above; stdout bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the `READ` offsets and lengths and the output and outcome for each case against the in-memory peer.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measurement setup.** BL-572's WSL OpenSSH 10.2 copied to `~/bl573` (`sshd -D` on port
  2235, public-key auth, `sftp-server -l DEBUG3` behind a wrapper logging every request).
  The reference curl 8.21.0 (libssh2 1.11.1, Schannel) ran through `Record-CurlExchange.ps1
  -NoServer` as `curl -sS -k --key <rsa> --pubkey <pub> -u <user>: <option>
  sftp://localhost:2235/~/bl573/files/a.txt`; `a.txt` holds `0123456789` (10 bytes).
  `sftp_download_stat` (`lib/vssh/libssh2.c`) and `Curl_ssh_range` (`lib/vssh/vssh.c`) at
  `curl-8_21_0` explain every result.
- **Measured** (stdout / stderr / exit; requests after `OPEN` and `STAT`):
  - `-r 2-5`: `2345` / empty / 0; `READ off 2 len 16`, `CLOSE`
  - `-r 7-` and `-r -3`: `789` / empty / 0; `READ off 7 len 12`, `CLOSE`
  - `-r 5-100`: `56789` / empty / 0; `READ off 5 len 20`
  - `-r -20`: `0123456789` / empty / 0; `READ off 0 len 40`
  - `-r 3-3`: `3` / empty / 0; `READ off 3 len 4`
  - `-r 10-`: empty / `curl: (33) Bad range: start offset larger than end offset` / 33; `CLOSE` only
  - `-r 11-`: empty / `curl: (33) Offset (11) was beyond file size (10)` / 33; `-r 20-30`: `Offset (20) was beyond file size (10)`, 33; `CLOSE` only
  - `-C 3`: `3456789` / empty / 0; `READ off 3 len 28`
  - `-C 0`: the whole file, `READ off 0 len 40`, 0
  - `-C 10`: `curl: (33) Bad range: start offset larger than end offset` / 33; `-C 11`: `curl: (33) Offset (11) was beyond file size (10)` / 33
  - `-C - -o f` with `f` = `012`: empty / empty / 0, `f` = `0123456789`; `READ off 3 len 28`
  - `-C - -o f` with `f` = 10 bytes: `curl: (33) Bad range: start offset larger than end offset` / 33; with 12 bytes: `curl: (33) Offset (12) was beyond file size (10)` / 33; `f` unchanged
  - empty file, `-C 3`: `curl: (36) Offset (3) was beyond file size (0)` / 36; `CLOSE` only
  - empty file, `-r 2-5`: empty / empty / 0; the unknown-size reads (14 of 30000 from 0)
  - `-C 3 -r 5-6`: `curl: --continue-at is mutually exclusive with --range` (tool refusal, exit 2; no handler)
  - `-r 1000-250999 -o f -w '%{size_download}'` of 300000 random bytes: `250000` / empty / 0; 14 `READ`s of 30000 from off 1000, 17 in all
- **Decisions (ADR-0253, decided under Stewart's delegation):** port `setup_range`,
  `Curl_ssh_range` and `sftp_download_stat` as `SftpDownloadPart.Choose`: a nonzero `-C` is
  the range `N-` (so exit 33, not 36, when the size is known); a range needs a `STAT` size;
  `-C` with no size is exit 36. A refused part closes the handle and runs no `-` quote.
  `SftpReadAhead` reads from the part's offset with its length as the size; answers past
  the part are cut. Over the large range ADR-0220's read-ahead sends 19 reads to libssh2's
  17 - all extra reads fall past the range and nothing curl prints differs; pinned as ours.
- **Not end to end.** `Curl.Console` does not route `sftp://` to the handler yet (`curl: (1)
  Protocol "sftp" not supported` from our build), so the in-memory peer is the gate. `-C -`
  needs nothing here: the console resolves it to the output file's size before a handler runs.
- **Coverage.** `RsaSshPrivateKey.Integer`'s zero branch had been unreached since BL-972
  refused a zero coefficient first; a test with a zero private exponent reaches it again
  (`Assert.Throws`, as OpenSSL's refusal is a subclass of `CryptographicException`).
- **Scope.** The ADR and its index line sit outside `touches`, as every SSH task's ADR does;
  no task in `Doing` names `Documentation`. ADR numbered 0253 to stay clear of numbers other
  lanes may take. `--ai-help` is unchanged: no option was added or changed.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. -r and -C on an SFTP download read only the requested bytes at libssh2's offsets, and a part past the end fails with curl's exit 33 or 36 and message; ADR-0253
