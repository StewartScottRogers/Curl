---
id: BL-2478
title: FTP: download each file a wildcard URL matches, with chunk begin, chunk end and match hooks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2476]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2478 — FTP: download each file a wildcard URL matches, with chunk begin, chunk end and match hooks

## Goal

With wildcard matching on, an FTP URL whose last path part holds a pattern (`ftp://host/dir/*.txt`) lists the directory, matches each entry's name, and downloads every match in turn, calling injected chunk-begin (transfer, skip, fail), chunk-end (carry on, fail) and name-match (match, no match, fail) hooks, as upstream's `ftp.c` wildcard state machine does.

## Context

Prerequisite of BL-2420, which adapts libcurl's CURLOPT_CHUNK_BGN_FUNCTION, CHUNK_END_FUNCTION and FNMATCH_FUNCTION to these hooks and sets them from `EasyBehaviourOptions.WildcardMatch`. Builds on BL-2476's listing parser and matcher. Follow upstream curl 8.22.0's `lib/ftp.c` (`wc_statemach`, `init_wc_data`, `wc_dl_*`): a failing chunk-begin hook ends with `CURLE_CHUNK_FAILED` (88), a failing chunk-end hook likewise, a failing match hook with `CURLE_FTP_BAD_FILE_LIST` (87); no match at all is `CURLE_REMOTE_FILE_NOT_FOUND` (78). Default hooks (none injected) download every match. The hook interface lives in the FTP library; protocol tests use a fake connection, never the network.

## Acceptance criteria

- [x] Unit tests pin a three-file listing with one skipped, a failing begin hook (88), a failing end hook (88), a failing match hook (87) and a listing with no match (78), each with the commands sent.
- [x] `dotnet build` is clean and the fast tests pass; `PassingUpstreamCases.txt` unchanged or grown (upstream test 1149/574-575 if they now pass).
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 0 failing members.
- [x] Every new public member has an XML doc comment.

## Notes

- 2026-10-16 (lane 1): read upstream curl 8.22.0's `lib/ftp.c` (`init_wc_data`, `wc_statemach`,
  `ftp_done_wildcard`, `ftp_done_status`), `lib/ftplistparser.c` (`ftp_pl_insert_finfo`) and tests
  574 and 576. Two acceptance details differ from upstream and follow upstream instead (ADR-0615):
  curl ignores the chunk-end callback's return value, so a failing end hook is pinned as ignored
  (every match still downloaded, exit 0), and a failing match hook only leaves the entry out, so
  with every entry left out the download ends with 78, not 87. 87 comes from a malformed listing
  (pinned, no `QUIT`), 27 from an entry past curl's buffer (pinned), 88 from a failing begin hook
  (pinned, no `QUIT`), 78 for no match (pinned, after `QUIT`).
- Hooks: `IFtpWildcardHooks` with `FtpNameMatchAnswer`, `FtpChunkBeginAnswer`, `FtpChunkEndAnswer`;
  `FtpDownloadEveryMatch` is libcurl's no-callback default; `FtpProtocolHandler.WildcardHooks`
  turns wildcard on (the transfer context is the shared Abstractions contract, outside `touches`).
- Per file, as test 574: `EPSV`, `TYPE I` only when it changes, `RETR`, no `CWD`, no `SIZE`; one
  `QUIT` at the end. `FtpProtocolHandlerWildcardTests` pins 15 cases.
- Upstream 574, 575, 576 and 1149 need libtest programs (the curl tool never sets
  `CURLOPT_WILDCARDMATCH`), so `PassingUpstreamCases.txt` is unchanged.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary`: 0 failing members.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. FTP wildcard URLs list the directory and download each match with match, chunk-begin and chunk-end hooks, as curl 8.22.0 does
