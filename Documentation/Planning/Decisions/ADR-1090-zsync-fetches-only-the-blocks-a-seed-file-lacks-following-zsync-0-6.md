# ADR-1090 — `--zsync` fetches only the blocks a seed file lacks, following the zsync 0.6 control file format

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4130.

## Context

BL-4130 adds the extension `Curl.Extension.DeltaDownload.UnitLibrary` (ADR-0999): use a `.zsync` control file to
fetch only the changed parts of a file the user already holds an older copy of. Upstream curl has no such option;
the zsync 0.6 control file format (as written by zsyncmake 0.6.2) and RFC 9110 §14 "Range Requests" decide the
behaviour. Transfer hooks can run curl's handler several times and capture what it received (ADR-1019), as
`--segments` does (ADR-1087).

## Decision

- **Names.** `--zsync <control-file>` turns it on; `--zsync-seed <file>` names the local copy to take unchanged
  blocks from, by default the control file's `Filename` (its file-name part) in the current directory. The
  extension is `zsync` in `curl --extensions` and `curl --ai-help extensions`. No scheme: the ranges ride on
  curl's own `http` and `https`, and the URL fetched is the command line's, not the control file's `URL` header.
- **Registration.** No protocol handler, so `ExtensionCatalogue.cs` gets no line (as ADR-1069 and ADR-1087); the
  two options, the description and the hook activator are one line each in `ExtensionOptionCatalogue`,
  `ExtensionDescriptionCatalogue` and `ExtensionTransferHookCatalogue`. With `CURL_EXTENSIONS=0` the options are
  not registered, so curl refuses them as unknown options (exit 2). `--zsync-seed` without `--zsync` does nothing.
- **Control file.** Header lines `Name: value` up to an empty line; `zsync`, `SHA-1` (40 hex digits), `Blocksize`
  and `Length` are required, `Hash-Lengths` (sequence matches 1 or 2, weak bytes 1 to 4, MD4 bytes 3 to 16)
  defaults to `1,4,16`; then for each block the last weak-checksum bytes (rsync `a` and `b`, big-endian) and the
  first MD4 bytes, the last block zero-padded. Unknown headers are ignored. A control file that cannot be read,
  or is not one, refuses the transfer with exit 26 (`ReadError`, as curl reports a local file it cannot read)
  without contacting the server.
- **Matching.** The weak checksum rolls over the seed, padded by one zero block; a weak match is confirmed by MD4,
  and with sequence matches of 2 by the following block too (the last block stands alone). MD4 (RFC 1320) is
  written by hand: the base class library has none.
- **Fetching.** Only a plain GET of the whole resource (no `-X`, body, upload, `-I`, `-r`, `-C`, `-z`) is
  handled; anything else runs as curl runs it. Each run of missing blocks is one `Range: bytes=first-last`
  request, made in turn. Once every range is a 206 whose `Content-Range` is exactly `bytes first-last/Length` and
  whose body is that long, and the joined file's SHA-1 equals the control file's, it is written; `-i`/`-D` show
  the first range's headers. A seed holding every block contacts no server (`%{http_code}` is then `000`).
- **When it cannot be joined.** A 200 to a range (the server ignores `Range`) is written as received. A failed
  request fails the transfer with nothing written. Any other answer, or a SHA-1 mismatch, runs the transfer
  again as curl runs it, so the output is never a wrong file.

## Left out, and why

- **Compressed targets** (`Z-URL`, `Z-Map2`, `Recompress`): zsync's gzip mapping is a large separate feature;
  such a control file still works for its plain `URL` target fetched through the command line's URL.
- **The control file's `URL` and `MTime`**: curl's URL and `-R` already decide where and how; following the
  control file's URL would make one command line fetch two hosts.
- **Several seed files** (zsync's repeated `-i`): one seed covers the update case; more can be added later.
- **Parallel or multipart range requests**: requests run in turn, one per run of missing blocks, keeping every
  response a plain 206.

## Consequences

- Without `--zsync`, and for every transfer it does not handle, curl's behaviour is unchanged.
- The whole target is held in memory before it is written, so a failure never leaves half a file.
