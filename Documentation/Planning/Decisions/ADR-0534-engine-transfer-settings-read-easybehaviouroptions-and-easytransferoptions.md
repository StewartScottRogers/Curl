# ADR-0534 — The engine's transfer settings read `EasyBehaviourOptions` and `EasyTransferOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2347.

## Context

ADR-0528 built `EasyOptionsFromCommandLine.BehaviourOf`, `TransferOf` and `UploadTransferOf`. BL-2347 has
`TransferContextFactory`, `TransferPerformer`'s `-m` and `-Y`/`-y` watchdog starts, `TransferTimers`,
`HttpRequestOptionsMapping`'s method and fail mode and `MailRequestOptionsMapping`'s command read them in
place of `CommandLineOptions`, with behaviour unchanged. Three settings do not carry over as they stood: the
records keep no "given as zero", no `-C -`, and `EasyTransferOptions.TimeValue` was a `DateTimeOffset`, which
cannot hold a `-z` date past 9999-12-31 that ADR-0410 keeps.

## Decision

- **`TimeValue` is whole seconds since 1970 (`long`)**, as libcurl's `CURLOPT_TIMEVALUE_LARGE` takes a
  64-bit `time_t`, so a `-z` date past year 9999 reaches the handlers with its seconds intact.
  `TransferContextFactory` rebuilds the handlers' `TimeCondition` from it with `TimeCondition.FromUnixSeconds`.
- **`-C -` with `-T` is an upload resuming from -1.** The runner builds each transfer's record
  (`TransferOptionsOf`): the group's, with `UploadTransferOf` for a `-T` upload, and `ResumeFrom` -1 under
  `-C -`, as curl's tool asks libcurl to resume an upload from the end of the remote file. `-C` takes digits
  only, so -1 never comes from an offset. The upload's size stays unknown (-1): the engine reads the stream.
- **Zero is libcurl's "none".** `--max-filesize 0` and `-m 0` reach the context as no limit (`null`), as
  libcurl treats `CURLOPT_MAXFILESIZE` and `CURLOPT_TIMEOUT` of 0. Every handler already read a limit of 0
  as none except the SMTP command output, which failed `--max-filesize 0` with exit 63; it now has no limit,
  as curl's does.
- **`-R` is `FileTime`.** The context's `RemoteTime` is `FileTime`, which `-I` also sets. Only FTP and SMB
  read it: FTP asked for `MDTM` under `-I` already, and both put the time on the result, which the runner
  applies to the output file only under `-R`, so no byte changes.
- **`-f` is `FailOnError`; `--fail-with-body` stays the tool's.** `HttpRequestOptions.Fail` is `Fail` under
  `FailOnError`, otherwise `CommandLineOptions.FailMode`, which is then none or `--fail-with-body`.

## Consequences

The engine reads range, resume, upload, file time, time condition, text mode, CRLF, speed limits, `-m`,
`-X`, `-I`, `-i`, `-f` and the file size limit from the records. `--fail-with-body`, the tool's `-C -` and
`-R` handling and the other groups' settings still come from `CommandLineOptions` until their tasks.
