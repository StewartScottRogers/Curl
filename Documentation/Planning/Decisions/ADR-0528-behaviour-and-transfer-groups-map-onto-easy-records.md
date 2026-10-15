# ADR-0528: The behaviour and transfer command-line groups map onto EasyBehaviourOptions and EasyTransferOptions

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2308 (ADR-0506 plan phase 1 step 4)
- Decided by Claude under Stewart's delegation.

## Context

ADR-0506 moves the engine from the curl tool's `CommandLineOptions` to the `Curl.Api` option
groups. `Curl.Console`'s `EasyOptionsFromCommandLine` is the tool's `config2setopts.c`, one method
per group; BL-2308 adds the behaviour and transfer groups. Their engine readers
(`TransferContextFactory`, `TransferPerformer`'s `-m` and `-Y`/`-y` watchdogs, `TransferTimers`,
`HttpRequestOptionsMapping`'s method and fail mode, `MailRequestOptionsMapping`'s command) carry
about as many test call sites as BL-2303's did, so the retarget is split, as BL-2303 and BL-2307
split theirs.

## Decision

1. `EasyOptionsFromCommandLine.BehaviourOf` builds `EasyBehaviourOptions`, agreeing with the
   `CURLOPT_*` lines `--libcurl` writes:
   - `Verbose` is on for `-v`, `--trace` and `--trace-ascii`; what is written stays the tool's
     (its debug callback, phase 3).
   - `NoProgress` is on for `-s` or `--no-progress-meter` and off otherwise, as curl's tool sets
     `CURLOPT_NOPROGRESS` to 0 to get its progress callback; so a bare command line differs from
     libcurl's default of on. `-#` draws through the same callback, so it leaves it off.
   - `FailOnError` is `-f` only: `--fail-with-body` is checked by the tool after the transfer,
     never set as `CURLOPT_FAILONERROR`, as `--libcurl` shows.
   - `NewFilePermissions` is `--create-file-mode` when given and not 0.
   - Where the command line's behaviour wins over `--libcurl`: `IncludeHeader` is on for `-i` and
     `-I` (which sets it too), as the engine sends their header lines into the body output, the
     effect of `CURLOPT_HEADER`; curl 8's tool does it in its header callback and writes no
     `CURLOPT_HEADER` line.
   - `-S`, `-#`, `-N`, `--retry*` and the output files never reach libcurl, so nothing maps.
2. `EasyOptionsFromCommandLine.TransferOf` builds `EasyTransferOptions`: `-r`, a `-C` offset,
   `-X` as `CustomRequest`, `FileTime` for `-R` or `-I`, `-l` as `DirListOnly`, `-I` as `NoBody`,
   `--max-filesize`, `-z` (its direction and date), `-B` as `TransferText`, `--crlf`, the
   low-speed pair with `-Y` alone meaning 30 seconds and `-y` alone a limit of 1, `--limit-rate`
   as both speeds, `-m` when positive, and `BufferSize` as the tool's 100 KiB or a smaller
   `--limit-rate` - so a bare command line's buffer differs from libcurl's 16 KiB, as `--libcurl`
   always writes `CURLOPT_BUFFERSIZE`.
3. `-C -` and `-T` are per transfer: the offset comes from the URL's output file and the upload
   from the URL's own `-T` file. `TransferOf` leaves `ResumeFrom` 0 for `-C -`, and
   `UploadTransferOf(transfer, size)` adds `Upload` and the file's size (`-1` when unknown, as for
   standard input; `0` for an empty file, which `--libcurl` leaves out only because it skips zero
   values).
4. The engine retarget is BL-2347; BL-2310 waits on it too.

## Consequences

No engine class reads either record yet, so behaviour is unchanged. BL-2347 moves the readers
against a mapping already tested option by option; the "given as zero" `-m` and `-C -` it needs
come from the tool per transfer.
