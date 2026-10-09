# ADR-0443 — `-D -` switches standard output to binary mode for `-w`

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (task BL-1816, GF-0023).

## Context

ADR-0081 writes a `-w` line feed to standard output as CR LF on Windows while curl's standard
output would still be in text mode. Upstream test 1341 (`-J -O -D - -w '...\n'`) expects LF:
curl's `tool_operate` sets standard output to binary mode for `-D -` header output ("always use
binary mode for protocol header output") when it sets the transfer up, whatever `-o` and `-B`
say. Measured on 2026-10-08 with curl 8.21.0 (Windows, Schannel):

| Command | Standard output |
| --- | --- |
| `-s -o NUL -D - -w "%{exitcode}\n" file:///nonexist/x` | `37\n` |
| `-s -B -o NUL -D - -w "%{exitcode}\n" file:///nonexist/x` | `37\n` |
| `-s -o NUL -w "%{exitcode}\n" file:///nonexist/x` | `37\r\n` |

## Decision

`CurlCommandRunner.SwitchesStandardOutputToBinary` is true for every transfer of an option group
under `-D -`, failed or not and under `-B` too, and `UrlFromSwitchesStandardOutputToBinary` counts
a later URL of such a group, so an earlier transfer's `-w` text still in curl's buffer is written
as LF too, as ADR-0081 models for a later body on standard output.

## Consequences

`-D -` runs print `-w` line feeds as LF on Windows, as upstream test 1341 expects. `-D %` sets
standard error to binary in curl as well; that is not modelled here.

## Alternatives considered

- **Leave CR LF and record a reference divergence.** Rejected: the Windows reference curl writes LF.
