---
id: BL-118
title: Measure and report the accepted byte count in MQTT's failed output write message
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-114]
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-118 — Measure and report the accepted byte count in MQTT's failed output write message

## Goal

An `mqtt://` subscription whose output fails prints the same
`curl: (23) Failure writing output to destination, passed N returned M` line as curl
8.21.0 for the measured cases, with `M` taken from
`OutputWriteFailedException.BytesAccepted` (BL-114) instead of a literal 0.

## Context

BL-099 measured only `file://` and `telnet://`; there is no MQTT measurement yet, so
**measure with curl 8.21.0 first**. The telnet rows from BL-099's Notes ("Measurements,
2026-09-26") show what a stream of small writes produced there:

| size x count | curl 8.21.0 stderr | exit | ours |
| --- | --- | --- | --- |
| 100 x 100 | `curl: (23) Failure writing output to destination, passed 100 returned 96` | 23 | `... passed 100 returned 0` |
| 30 x 400 | `curl: (23) Failure writing output to destination, passed 30 returned 16` | 23 | `... passed 30 returned 0` |
| 5000 x 5 | `curl: (23) Failure writing output to destination, passed 4096 returned 0` | 23 | `... passed 10000 returned 0` |

BL-099's rule: `M` is the room left in curl's 4096-byte stdio buffer when the overflowing
write arrives. MQTT may differ; that is what the measurement is for.

Measure with `/mingw64/bin/curl` (curl 8.21.0, x86_64-w64-mingw32) from Git Bash: a Python
loopback broker that answers CONNECT with CONNACK and SUBSCRIBE with SUBACK, then sends
`count` QoS 0 PUBLISH packets on the subscribed topic with `size`-byte payloads, 5 ms
apart, and closes; `curl -sS mqtt://127.0.0.1:<port>/t 2>&1 >&- </dev/null`, for size x
count of 100 x 100, 300 x 100, 1000 x 20, 30 x 400 and 5000 x 5. Record each command, its
exact standard error and exit code, and ours (`dotnet run --project Curl.Console`, same
command line) in this task's `Notes` before changing code. Also record in `Notes` which
bytes each PUBLISH puts on standard output in curl (for example whether the topic is
written as well as the payload, and in how many writes), since that decides `N`.

Where the literal lives: `Curl.Protocol.Mqtt.UnitLibrary/MqttTransferMessages.cs`,
`OutputWriteFailed(int passed)` (around line 61), called from
`MqttSession.WriteOutputAsync` (around line 229), which catches `IOException` from
`output.WriteAsync` and passes only `bytes.Length`. Carry
`OutputWriteFailedException.BytesAccepted` (from `Curl.Protocol.Abstractions.UnitLibrary`)
into the message; a plain `IOException` gives 0. If the measurement shows `N` differs from
ours for a reason inside `Curl.Protocol.Mqtt.UnitLibrary` (for example how PUBLISH output
is split into writes), match it here; anything needing a project outside `touches`
becomes a follow-up task. Keep every method within cyclomatic complexity 10 and the
library at 100% line and branch coverage.

## Acceptance criteria

- [ ] `Notes` records, for curl 8.21.0, each of the five commands above with its exact
      standard error and exit code, and ours before the change.
- [ ] `Curl.Protocol.Mqtt.UnitTests` has one test per measured row: a fake `IConnection`
      delivers the broker's packets, a fake output stream throws
      `OutputWriteFailedException` with the `BytesAccepted` that row needs, and the
      result is `CurlExitCode.WriteError` with the message after `curl: (23) ` byte for
      byte.
- [ ] A test whose output stream throws a plain `IOException` still gets `returned 0`.
- [ ] No literal `returned 0` remains in `MqttTransferMessages.cs`.
- [ ] `dotnet build Curl.Protocol.Mqtt.UnitLibrary -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Stewart: dark factory run ended in Doing, exit 1; see logs\BL-118-20260926-083111-L3.jsonl
- 2026-09-26: Blocked -> Backlog. Not blocked: the 2026-09-26 shift ran out of tokens (usage limit), which it misfiled as a stall
