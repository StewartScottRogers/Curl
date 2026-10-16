# ADR-0631 — A serial transfer's behaviour, transfer and network options go through `curl_easy_setopt`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2491.

## Context

BL-2451 routed every serial transfer through a `Libcurl` `CurlEasy` (ADR-0624), but only `CURLOPT_URL` went
through `CurlEasy.SetOption`; `CurlCommandRunner.SetUpToolEasy` wrote the whole `EasyOptions` record onto the
engine transfer's hooks. Upstream's `config2setopts.c` instead makes one `curl_easy_setopt` call per option. Libcurl's
setters (`Libcurl/Options/Behaviour`, `Transfer`, `Network`) check and sometimes clamp a value, and the output
must stay byte for byte what it is.

## Decision

1. `EasySetoptsFromCommandLine.Of` turns the behaviour, transfer (with the transfer's `-T` upload) and network groups
   into `EasySetopt` calls: one per field with a `CURLOPT_*` whose value differs from libcurl's default
   (`new Easy*Options()`), in the groups' order, flags as `1L`/`0L`, enums as their `long` value, lists as `string[]`,
   times in the setter's unit (`CURLOPT_TIMEOUT_MS`, `CONNECTTIMEOUT_MS`, `HAPPY_EYEBALLS_TIMEOUT_MS`,
   `LOW_SPEED_TIME`, `TCP_KEEPIDLE`, `TCP_KEEPINTVL`), sizes through the `_LARGE` options.
2. `EasySetoptsFromCommandLine.KeptOnHooks` is what the hooks keep: every other group whole (BL-2492 and BL-2493
   move them), libcurl's defaults for the three groups, and only these fields:
   - `IpTypeOfService`, `VlanPriority` and `MultipathTcp` (`--ip-tos`, `--vlan-priority`, `--mptcp`): libcurl has no
     `CURLOPT_*` for them; curl's tool sets them in its socket callbacks.
   - `BufferSize` outside 1024 to 10 MB: `--limit-rate` below 1024 makes the tool's buffer the rate, and
     `CURLOPT_BUFFERSIZE` would raise it to 1024, changing how the engine paces the body.
   - A time that is not a whole number of the setter's unit, negative, or past `INT_MAX` units (`-m 0.0001`,
     `--connect-timeout` of more than 24 days): the setter would cut it.
3. `ToolEasyHandle.SetOptions` makes the calls in order and stops at the first that is not `CURLE_OK`, as
   `config2setopts` returns at its first failure. The transfer then ends before any perform with the call's code as
   the exit code and curl's `setopt 0x<option> got bad argument` text (the form BL-600 measured for
   `CURLOPT_INTERFACE`), and the run ends, as curl's tool ends it.
4. No command line reaches that failure: the parser already refuses every value a setter would (measured on
   curl 8.21.0: `-C -5`, `--max-filesize -1`, `-Y -1`, `-y -1`, `--keepalive-cnt -1`, `--connect-timeout -1`,
   `--proto -all` and `--proto =ipfs` are all refused while parsing with exit 2). The runner's
   `easySetoptsOf` parameter lets tests pass a call libcurl refuses.

## Consequences

- The fields set through `SetOption` reach the engine exactly as before, so no output changes; the tests pin a
  representative field of each group (`-f`, `--max-filesize`, `--path-as-is`) and the kept buffer size.
- `-Z` attempts take the same setup, so the multi handle's transfers get the same calls.
- Libcurl's setters were used as they are; none needed a change.
