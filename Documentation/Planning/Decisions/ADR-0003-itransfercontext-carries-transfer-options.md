# ADR-0003 — `ITransferContext` carries transfer options

- **Status:** Accepted
- **Date:** 2026-09-25

## Context

`ITransferContext` (`Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`)
currently carries only `Url`, `Output`, `Upload`, `TimeProvider` and
`CancellationToken`. That is not enough for a protocol handler to implement curl's
`-r`/`--range`, `-C`/`--continue-at`, `-z`/`--time-cond`, `-R`/`--remote-time`, or
`-i`/`-I`/`-D` (header output and body suppression). These options apply to HTTP and
FTP as much as to `file` — a range or a time condition is not specific to one
scheme — so the contract is shared on `ITransferContext` rather than duplicated
per-protocol.

## Decision

`ITransferContext` is extended with:

- `long? ResumeFrom` — the byte offset for `-C`/`--continue-at`.
- `ByteRange? Range` — the parsed form of `-r`/`--range`.
- `bool NoBody` — `-I`/`--head`, suppressing the response body.
- `TimeCondition? TimeCondition` — the parsed form of `-z`/`--time-cond`.
- `Stream? HeaderOutput` — the destination for `-i`/`-D`-style header output.

with supporting `ByteRange` and `TimeCondition` types added to
`Curl.Protocol.Abstractions.UnitLibrary`.

This is the shape another agent is implementing concurrently; this ADR records it
rather than designing an alternative.

## Consequences

Good:

- Range, resume, time-condition and header-output options are expressible for every
  protocol from one contract, instead of five near-duplicate mechanisms.

Costs and caveats:

- Every current and future `IProtocolHandler` now sees these five fields and must
  decide whether each applies to its scheme. curl itself ignores most options for
  most schemes, and "ignored" is a legitimate, expected answer here too — but it
  must be stated per protocol (in that protocol's own documentation or tests), not
  left implicit.

## Alternatives considered

- **Per-protocol option types, one per handler.** Rejected: range, resume and
  time-condition are not HTTP-specific or FTP-specific concepts; duplicating them
  per protocol would drift the moment one implementation gained a fix the others
  did not.
- **A single untyped `IReadOnlyDictionary<string, object>` bag of options on
  `ITransferContext`.** Rejected: it defeats nullable-reference-type checking and
  moves a compile-time error ("this handler forgot to read `Range`") to a runtime
  one.

## Known limitation, recorded but not decided here

`ITransferContext.Url` remains a `System.Uri`, and `System.Uri` cannot represent
every URL curl accepts. Measured against curl 8.21.0 (2026-06-24) on this machine:

- `Uri` throws on `file:///C:%2FWindows/win.ini` (curl exits 0).
- `Uri` throws on `file://user:pass@localhost/x` (curl exits 3).
- `Uri` rewrites `c|` to `c:`, where curl does not.
- `Uri` folds `file:////server/share` into a UNC authority, destroying the only UNC
  form curl accepts.
- Dot segments (corrected 2026-09-26, measured against curl 8.21.0; an earlier note
  here said curl passes `..` straight to the OS, which is false). Both `Uri` and curl
  remove `.` and `..` segments, but `Uri` does it always, where curl skips it under
  `--path-as-is`. Both also turn every `\` into `/` first, and curl does that with or
  without `--path-as-is`: `file:///C:/dir\..\secret.txt` opens `C:/secret.txt`, and
  with `--path-as-is` `file:///C:/dir\..\x` is quoted as `C:/dir/../x`. Because
  `FileUrlPath` works from `Uri.OriginalString`, it does both steps itself, with a
  `pathAsIs` switch (task BL-015).
- `Uri` throws on `file:///C:` and `file:///Q:dir/../x`, which curl accepts (both exit
  37 on this machine, quoting `C:` and `/x`); the `file://localhost/` spellings of the
  same paths reach `FileUrlPath`.

This affects HTTP equally — `%2F` in a path, `--path-as-is` — not only `file`. It is
**not** decided by this ADR; it is deferred to a separate decision owned by the
Core and HTTP work (see `Documentation/Planning/Decisions/README.md` for the ADR
process, and task BL-010 in `Tasks/` for the tracking item). Sources:
<https://curl.se/docs/manpage.html> (`--path-as-is`) and
<https://curl.se/docs/url-syntax.html>, both checked against curl 8.21.0.

## Amendment, 2026-09-26 — an unknown timestamp under `-z` (BL-018)

A handler deciding `TimeCondition` must also decide what an unknown last-write time
means. For `file://` the timestamp is optional (`FileOpenResult.LastWriteTimeUtc` is a
`DateTimeOffset?`, see ADR-0002's amendment of the same date), and an unknown one
transfers the body under either `TimeConditionKind`, matching libcurl 8.21.0's
`Curl_meets_timecondition`: a condition that cannot be evaluated does not suppress
data. The same absence drops the `Last-Modified` line from the `HeaderOutput` block
rather than printing a made-up date. Sources: <https://curl.se/docs/manpage.html>
(`-z`, `--time-cond`) and <https://curl.se/libcurl/c/CURLOPT_TIMECONDITION.html>,
checked against curl 8.21.0.

## Amendment, 2026-09-26 — the result carries transfer-option data too (BL-019)

Transfer-option data does not only travel in on `ITransferContext`; some of it travels
back out on `TransferResult`. The first case is `-R`/`--remote-time`, which curl 8.21.0
applies to `file://`: `curl -R -o out.txt file:///C:/dir/hello.txt` leaves `out.txt`
with the source's modification time, truncated to whole seconds. A `file://` handler
cannot apply it itself, because its destination is `ITransferContext.Output`, a `Stream`
it neither opened nor owns, and behind which there may be no file at all. So
`TransferResult` gains `DateTimeOffset? SourceLastWriteTimeUtc` as its last positional
member (default `null`), set by `TransferResult.Success(bytes, timestamp)` and never by
`TransferResult.Failure`, and whoever opened `Output` applies it when `-R` was asked
for. `null` means the time is unknown or no source was opened; an upload reports `null`.
The `file` handler reports it on every successful download, `-I`/`--head` included,
already truncated to whole seconds. Sources: <https://curl.se/docs/manpage.html>
(`-R`, `--remote-time`), checked against curl 8.21.0.
