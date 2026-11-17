---
id: BL-5321
title: Let stomp:// name the virtual host, default it to /, and show the ERROR frame body
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Stomp.UnitLibrary/Client, Curl.Extended.Protocol.Stomp.UnitTests, Documentation/Protocols.md]
requirement: none
created: 2026-11-16
completed:
---
# BL-5321 — Let stomp:// name the virtual host, default it to /, and show the ERROR frame body

## Goal

`curl -u u:p -d x stomp://127.0.0.1:36613/queue/sq` works against a stock RabbitMQ 4 STOMP broker (CONNECT `host` defaults to the virtual host `/`, not the URL host), the user has a documented way to name another virtual host, and a STOMP ERROR is reported with its frame body.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding EXT-066 (Medium, Confirmed, 6 runs; cause proven). Area: stomp, stomps; Windows. This is an extension, so it is judged against the port's own docs and ADRs, not curl.

**Repro.**
1. RabbitMQ 4 with `rabbitmq-plugins enable --offline rabbitmq_stomp`, port 61613 published on 127.0.0.1:36613, user `u` / `p`.
2. `curl -sS -m 5 -u u:p -d x stomp://127.0.0.1:36613/queue/sq`
3. `curl -sS -m 5 -u u:p -d x stomp://localhost:36613/queue/sq`
4. A raw TCP listener shows the frame the port sends: `CONNECT\naccept-version:1.2\nhost:127.0.0.1\nheart-beat:0,0\nlogin:u\npasscode:p\n\n\0`.

**Expected.** `--extended-help`: "Sends and subscribes with STOMP 1.2 … --user gives login and passcode". `Stomp.md` row `Stomp:STOMP12-Connect-2`: CONNECT carries `host`, "the virtual host it connects to". The send should succeed against the default vhost `/`, or the user should have a way to name the vhost.

**Actual.** Steps 2 and 3 print `curl: (67) STOMP server sent ERROR: Bad CONNECT`, exit 67; the broker log says `virtual host '127.0.0.1' does not exist` (or `'localhost'`). After `rabbitmqctl add_vhost 127.0.0.1` the same command works. The message also leaves out the ERROR frame body, so a wrong password gives the same text.

**Where to start.** `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:59`, `new("host", host)`. Let the user name the virtual host (for example a URL query parameter or `-H host:`), default to `/` (or document the rule), and include the ERROR frame body in the message.

**Design decision, Claude's to make.** How the user names the virtual host (URL query parameter, `-H`, an option, or the first path segment) and the default are design choices delegated to Claude (root `CLAUDE.md`, "Decisions"). Decide by the standing rules (simplest thing that works against stock brokers, no new option unless needed, `CURL_EXTENSIONS=0` still turns it off), record the decision and why in an ADR marked "Decided by Claude under Stewart's delegation", and tell Stewart afterwards. If the decision adds or changes an option or a URL rule, keep `--ai-help` and `--extended-help` right, and update the Stomp row in `Documentation/Protocols.md` and the `Stomp.md` spec table (`Stomp:STOMP12-Connect-2`).

## Acceptance criteria

- [ ] An ADR "Decided by Claude under Stewart's delegation" exists under `Documentation/Planning/Decisions/` stating how the virtual host is named, its default (`/`), and why.
- [ ] New tests in `Curl.Extended.Protocol.Stomp.UnitTests` (a scripted in-memory connection, no real broker) prove: with no vhost named the CONNECT frame carries `host:/`, not the URL host; a vhost named by the chosen mechanism is sent as the `host` header; the rest of the CONNECT frame (`accept-version`, `heart-beat`, `login`, `passcode`) is unchanged.
- [ ] A test proves an ERROR frame with a body ends in exit 67 with a message that includes the ERROR frame's `message` header and its body text; a test proves an ERROR frame with no body keeps the existing short message.
- [ ] Tests are platform-neutral.
- [ ] If an option or URL rule changed, `--ai-help` and `--extended-help` describe it and a test or the Log shows they do; the Stomp row in `Documentation/Protocols.md` states the vhost rule.
- [ ] Review acceptance check recorded in the Log as a hand check: against stock `rabbitmq:4-alpine` with the STOMP plugin, `curl -sS -u u:p -d x stomp://127.0.0.1:36613/queue/sq` exits 0, and a parallel `curl -sS -u u:p -m 3 stomp://127.0.0.1:36613/queue/sq` prints `x`. A real RabbitMQ broker cannot run in a lane; the Log says so when not run, and records the raw-listener check of the CONNECT frame instead (PowerShell listener, never Python).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: against stock `rabbitmq:4-alpine` with the STOMP plugin, `curl -sS -u u:p -d x stomp://127.0.0.1:36613/queue/sq` exits 0, and a parallel `curl -sS -u u:p -m 3 stomp://127.0.0.1:36613/queue/sq` prints `x`.
- The ADR and any help-text files it changes are added to this task's `touches` when the decision is made.

## Log

- 2026-11-16: Created.
