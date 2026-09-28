# ADR-0127 — Parallel transfers write in completion order and exit with the first failure

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-518.

## Context

`-Z`/`--parallel` is parsed by BL-517; ADR-0126 made `CurlCommandRunner` run the `--next`
groups in order, one transfer at a time. This ADR fixes how a `-Z` run schedules its transfers,
where their output goes and which exit code it returns, so BL-519, BL-520 and BL-521 build one
design.

### Measurements

curl 8.21.0 (Windows, Schannel build, Git for Windows' mingw64), recorded with
`Record-CurlExchange.ps1 -Port 18518 -Connections 2 -ResponseDelayMilliseconds 800`. The
recorder answers connections one after another, so its two HTTP responses (`from-conn1\n`,
`from-conn2\n`, `Content-Length: 11`) finish in the order curl connected; a `file://` URL
(`file:///C:/ProgramData/bl518/b.txt`, holding `from-file\n`), a missing file
(`file:///nonexistent/missing.txt`, exit 37) and a refused port (`http://127.0.0.1:1/b`,
exit 7 after about 2 s on Windows) finish at other times, which is what makes completion order
differ from command-line order. `A` is `http://127.0.0.1:18518/a`, `C` is
`http://127.0.0.1:18518/c`, `F` the `file://` URL, `M` the missing file, `R` the refused port.

| # | Arguments | stdout | stderr | Exit |
| --- | --- | --- | --- | --- |
| 1 | `-s A F C` (serial, for comparison) | `from-conn1\nfrom-file\nfrom-conn2\n` | empty | 0 |
| 2 | `-Z -s A F C` | `from-file\nfrom-conn1\nfrom-conn2\n` | empty | 0 |
| 3 | `-Z -s -w '%{urlnum} %{url} %{exitcode}\n' A F C` | `from-file\n1 file:///C:/ProgramData/bl518/b.txt 0\nfrom-conn1\n0 http://127.0.0.1:18518/a 0\nfrom-conn2\n2 http://127.0.0.1:18518/c 0\n` | empty | 0 |
| 4 | `-Z --parallel-max 1 -s -w '%{urlnum}\n' A F C` | `from-conn1\n0\nfrom-file\n1\nfrom-conn2\n2\n` | empty | 0 |
| 5 | `-Z -sS -w '%{urlnum} %{exitcode}\n' A M C` | `1 37\nfrom-conn1\n0 0\nfrom-conn2\n2 0\n` | `curl: (37) Could not open file /nonexistent/missing.txt\n` | 37 |
| 6 | `-Z -sS A R C` | `from-conn1\nfrom-conn2\n` | `curl: (7) Failed to connect to 127.0.0.1:1 after 2016 ms: Could not connect to server\n` | 7 |
| 7 | `-Z -sS -f R A` (A answers 404) | empty | `curl: (22) The requested URL returned error: 404\ncurl: (7) Failed to connect to 127.0.0.1:1 after 2036 ms: Could not connect to server\n` | 22 |
| 8 | `-sS -f A R` (serial, A answers 404) | empty | `(22)` line, then `(7)` line | 7 |
| 9 | `-sS -f R A` (serial, A answers 404) | empty | `(7)` line, then `(22)` line | 22 |
| 10 | `-Z -sS -f A M C` (A and C answer 404) | empty | `(37)` line, then two `(22) The requested URL returned error: 404` lines | 37 |
| 11 | `-Z --fail-early -sS -w '%{urlnum} %{exitcode}\n' A M C` | `1 37\n0 42\n2 42\n` | `curl: (37) Could not open file /nonexistent/missing.txt\ncurl: (42) Transfer aborted due to critical error in another transfer\ncurl: (42) Transfer aborted due to critical error in another transfer\n` | 37 |
| 12 | `-Z --parallel-max 2 --fail-early -sS -w '%{urlnum} %{exitcode}\n' A M C` | `1 37\n0 42\n2 37\n` | `curl: (37) Could not open file /nonexistent/missing.txt\ncurl: (42) Transfer aborted due to critical error in another transfer\ncurl: (37) Could not read a file:// file\n` | 37 |
| 13 | `-Z --parallel-max 1 --fail-early -sS -w '%{urlnum} %{exitcode}\n' R A C` | `0 7\n1 7\n2 7\n` | `curl: (7) Failed to connect to 127.0.0.1:1 after 2019 ms: Could not connect to server\ncurl: (7) Could not connect to server\ncurl: (7) Could not connect to server\n` | 7 |
| 14 | `--fail-early -sS M A` (serial) | empty | `(37)` line only | 37 |
| 15 | `-Z -v -s A C` (300 ms delay) | `from-conn1\nfrom-conn2\n` | C waits: `* Found pending candidate for reuse and CURLOPT_PIPEWAIT is set`, `* Waiting on connection to negotiate possible multiplexing.`, and C's connection is opened only after A's ends; run 783 ms | 0 |
| 16 | `-Z --parallel-immediate -v -s A C` (300 ms delay) | `from-conn1\nfrom-conn2\n` | both `*   Trying` lines first, then both requests, then both responses, each line whole; run 680 ms | 0 |
| 17 | `-Z -v -s A http://localhost:18518/c` (300 ms delay) | `from-conn1\nfrom-conn2\n` | both hosts connect at once without waiting; A's and C's lines interleave line by line in event order | 0 |

What the table shows:

- A transfer's body reaches standard output as it arrives, and its `-w` text follows its body the
  moment it ends (rows 2, 3). The order is completion order, not command-line order; `%{urlnum}`
  still names the command-line position (row 3). With `--parallel-max 1` the order is the
  command line's again (row 4).
- A failed transfer's error line is written when it ends, in completion order (rows 5, 7, 10).
- The exit code of a `-Z` run is the code of the **first transfer to fail, in completion order**
  (rows 7, 10), where a serial run returns the **last** failure's (rows 8, 9; ADR-0126). A run
  with no failure exits 0.
- Under `--fail-early`, the first failure stops the run: every transfer still running ends with
  exit 42, `Transfer aborted due to critical error in another transfer` (rows 11, 12); every
  transfer not yet started ends with the first failure's code and curl's generic text for that
  code (`curl_easy_strerror`: `Could not read a file:// file` for 37, `Could not connect to
  server` for 7; rows 12, 13). Each still gets its `-w` output and, under `-S`, its error line,
  in command-line order after the failure (rows 11 to 13). The exit code is the first failure's.
  A serial `--fail-early` run prints nothing for the URLs it skips (row 14).
- Without `--parallel-immediate`, a transfer to a host that already has a connection being set
  up waits for it instead of opening its own (row 15); that belongs to BL-520.
- `-v` lines are written whole, one at a time, interleaved in event order (rows 16, 17).

## Decision

1. **Scheduling.** Under `-Z`, `CurlCommandRunner` builds one queue of every transfer in
   command-line order across all `--next` groups (each keeps its group's options and dispatch,
   ADR-0126) and keeps up to `--parallel-max` (default 50) of them running as tasks, starting the
   next queued transfer as each one ends. It awaits `Task.WhenAny` over the running set - async all
   the way, no thread per transfer, no `.Result`/`.Wait()`, time from the injected `TimeProvider`.
   Per-host limits and the reuse wait of rows 15 and 16 are BL-520's, layered on this queue.
   Without `-Z` the runner keeps its serial loop unchanged.
2. **Output ordering.** Each transfer writes its body to its destination as it arrives; a
   transfer bound for standard output writes each chunk under one run-wide write gate
   (`SemaphoreSlim(1, 1)`), so chunks from two transfers may interleave but a chunk is never
   split. When a transfer ends it writes, under the same gate and in this order, its error line
   (stderr) and its `-w` text (stdout or stderr), so both follow its body as measured. `-v` and
   trace lines are written whole under the gate, in event order. `%{urlnum}` stays the run-wide
   command-line number (ADR-0126); `%{xfer_id}` is taken when a transfer starts and `%{conn_id}`
   is the pool's connection number, not a counter bumped at completion.
3. **Exit code.** A `-Z` run returns the exit code of the first transfer to fail, in completion
   order, and 0 when none fails. The serial rule (the last failure's code) is untouched.
4. **`--fail-early`.** The first failure cancels every running transfer through a run-wide
   `CancellationTokenSource`; each cancelled transfer ends as `CurlExitCode.AbortedByCallback`
   (42) with `Transfer aborted due to critical error in another transfer`. Every queued transfer
   is not started and ends with the first failure's code and curl's generic text for it. The
   runner writes the aborted and skipped transfers' `-w` and `-S` lines in command-line order
   after the failing transfer's, and returns the first failure's code.
5. **Made safe for concurrent use.**
   - `PoolingConnector` (ADR-0050) already guards its idle list with a lock and numbers
     connections with `Interlocked`; it needs tests that prove two concurrent callers never get
     the same idle connection.
   - `CookieStore` (`Curl.Cookies.UnitLibrary`) has no lock; `GetCookieHeader` and
     `StoreFromResponse` must be made safe for concurrent callers, since every transfer of a run
     shares it (ADR-0126). The jar is written once, after the last transfer.
   - The runner's per-run fields (`nextTransferId`, `nextConnectionId`,
     `standardOutputSwitchedToBinary`, `progressMeterHeaderWritten`, the trace and standard error
     streams) are read and written only under the write gate or through `Interlocked`.
   - `TransferProgressRecorder` stays one per transfer; under `-Z` its single-transfer meter is
     replaced by the combined parallel meter, which reads every transfer's recorder.

## Consequences

- **BL-519** builds the queue, the write gate, the exit-code rule and `--fail-early` in
  `Curl.Console`, tests them with fake handlers that finish out of order, and makes the runner's
  per-run fields safe. The `CookieStore` lock lives outside `Curl.Console`, so it is its own task
  (BL-755), which BL-519 depends on.
- **BL-520** adds `--parallel-max-host` and `--parallel-immediate` on top of the queue: without
  `--parallel-immediate` a transfer to a host whose first connection is still being set up waits
  for it (row 15).
- **BL-521** replaces each transfer's single meter with curl's combined parallel meter, drawn from
  the transfers' `TransferProgressRecorder`s.
- Output bytes of a parallel run depend on timing, so tests pin order with fakes that complete
  when told to, never with real delays.
- Two large bodies bound for standard output can interleave chunk by chunk, as in curl; that is
  faithful, not a defect.

## Alternatives considered

- **Buffer each transfer's output and write it in command-line order.** Deterministic, but row 2
  shows curl does not: a script that reads the first finished body first would see a different
  stream. It also holds every body in memory.
- **One thread per transfer with blocking writes.** Breaks "async all the way" and scales badly
  at `--parallel-max 300`.
- **Return the last failure's code, as the serial loop does.** Rows 7 and 10 show curl returns the
  first; one rule for both modes would be simpler and wrong.
- **Stop queued transfers silently under `--fail-early`, as the serial loop does.** Rows 12 and 13
  show curl reports each one; silence would drop `-w` lines scripts count on.
