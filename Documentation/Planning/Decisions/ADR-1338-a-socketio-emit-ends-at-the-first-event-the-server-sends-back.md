# ADR-1338: A socketio emit ends at the first event the server sends back

- Status: Accepted
- Date: 2026-11-22
- Task: BL-5763
- Decided by Claude under Stewart's delegation.
- Amends ADR-1035.

## Context

A `Curl.Api` `SendAsync` that emitted an event on `socketio://` blocked until its timeout (28) although the server had echoed the event: the session, console and `Curl.Api` alike, listened until the server disconnected (bug report 2026-11-22T1308, finding 3). The console had the same wait; `Curl.Api` returns only when the transfer ends, so it cannot show the echo earlier.

## Decision

When the transfer emits an event (`-d` or `-T`), the session ends after it has written the first event the server sends back (and sent that event's acknowledgement). A transfer that emits nothing still listens until the server disconnects or closes. Socket.IO is an extension, not curl behaviour, so the choice is ours: an emit is a request and its first event the reply, and a caller that wants a stream omits the body.

## Consequences

`curl -d '["hello"]' socketio://...` and `SendAsync` return the echo at once. Servers that send several events after one emit have the later ones dropped.

## Amendment (BL-5835, fitness finding EXT-107)

Decided by Claude under Stewart's delegation. Ending at the first event dropped the later events of a server that sends several replies and a disconnect, against `--extended-help` ("writes each event received ... until the server disconnects"). A transfer that emits now writes every event until the server disconnects or closes, and also ends when the server has sent no further event for one second after an event, so a server that stays open still returns without waiting for a timeout (the BL-5763 case). The one-second quiet window restarts at every event.
