# ADR-0615 — The FTP wildcard download follows curl 8.22.0's `wc_statemach`, return values included

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2478.

## Context

BL-2478 drives BL-2476's `FtpFileListParser` and `FtpWildcardPattern` to download every file a wildcard URL
(`ftp://host/dir/*.txt`) matches, calling chunk-begin, chunk-end and name-match hooks that BL-2420 will adapt to
libcurl's `CURLOPT_CHUNK_BGN_FUNCTION`, `CURLOPT_CHUNK_END_FUNCTION` and `CURLOPT_FNMATCH_FUNCTION`. The task's text
said a failing chunk-end hook ends with exit 88 and a failing match hook with exit 87. Reading upstream curl 8.22.0's
`lib/ftp.c` (`init_wc_data`, `wc_statemach`, `ftp_done_wildcard`) and `lib/ftplistparser.c` (`ftp_pl_insert_finfo`)
shows otherwise, and the curl command-line tool never sets `CURLOPT_WILDCARDMATCH`, so only libcurl behaviour is at
stake.

## Decision

1. **The hooks are an interface in the FTP library**, `IFtpWildcardHooks` (`MatchName`, `BeginChunk`, `EndChunk`)
   with the answer enums `FtpNameMatchAnswer`, `FtpChunkBeginAnswer` and `FtpChunkEndAnswer`, whose values are
   libcurl's. `FtpProtocolHandler.WildcardHooks` (an `init` property) turns wildcard matching on; `null` leaves it off.
   `FtpDownloadEveryMatch` is libcurl's behaviour with no callback set. The handler property, not
   `ITransferContext`, carries them because the transfer context lives in `Curl.Protocol.Abstractions.UnitLibrary`,
   a shared contract every protocol task would wait on; BL-2420 builds the handler with the hooks of the easy handle.
2. **Exit codes as curl 8.22.0 returns them:** a malformed listing ends with the parser's code (87, or 27 for an entry
   past curl's buffer) and no `QUIT`; no kept entry is 78 after `QUIT`; a failing chunk-begin hook is 88 with no
   `QUIT` (`ftp_done` marks the connection dead for every code outside its "alive fine" list).
3. **A failing chunk-end hook is ignored**, as `ftp_done_wildcard` and the `CURLWC_SKIP` state ignore the callback's
   return value. Ending with 88 would make Curl differ from the library it replaces.
4. **A failing match hook leaves the entry out**, as `ftp_pl_insert_finfo` keeps only a 0 answer; with every entry
   left out the download ends with 78, not 87.
5. **The conversation is upstream test 574's:** `CWD`s and `LIST` (`TYPE A`), then for each file `EPSV`, `TYPE I`
   when the type changes, and `RETR`, with no further `CWD` and no `SIZE` (the listing's size is curl's
   `known_filesize`), and one `QUIT` at the end. `--ftp-method nocwd` becomes `multicwd`. The pattern is the URL
   path's last part still percent-encoded, and each file's path is the encoded directory plus the listed name,
   decoded again, as curl builds `tmp_path`. A chunk-begin hook sees every kept entry, a non-file included (which is
   then skipped), with the count of entries left, this one included; the chunk-end hook follows every entry not
   failed by its begin hook, whether downloaded, failed or skipped.

## Consequences

The acceptance criteria's 88 for a failing end hook and 87 for a failing match hook are replaced by the measured
upstream behaviour, pinned in `FtpProtocolHandlerWildcardTests`. Pre- and post-transfer quotes run for the listing
and for each file, as each is one `ftp_do` in curl.
