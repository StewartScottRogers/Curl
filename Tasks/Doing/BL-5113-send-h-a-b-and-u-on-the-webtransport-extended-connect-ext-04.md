---
id: BL-5113
title: Send -H, -A, -b and -u on the webtransport extended CONNECT (EXT-048)
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportSession.cs, Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportRequest.cs, Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportConnectHeaderFields.cs, Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportSessionTests.cs, Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportConnectHeaderFieldsTests.cs, Documentation/Conformance/Extended/WebTransport.md]
requirement: none
created: 2026-11-13
completed:
---
# BL-5113 — Send -H, -A, -b and -u on the webtransport extended CONNECT (EXT-048)

## Goal

The webtransport extended CONNECT carries the header fields curl's options add (`-H`, `-A`, `-b`, and the authorization from `--oauth2-bearer` or `-u`), as the port's other upgrade-based schemes (mqtt5+ws, signalr, socketio) already do.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-048 (Medium, Confirmed by server log and code).

**Repro.**
1. Run an HTTP/3 server on UDP 127.0.0.1:27081 with WebTransport enabled (the review patched aioquic 1.8's `_get_local_settings` to add `0x2c7cf000: 1`; use a C# file-based stand-in instead, never Python), accepting `:protocol webtransport-h3`, echoing `echo:`+data on the bidirectional stream, and logging the request headers.
2. `curl -sS -m 5 -k -H 'Authorization: Bearer t' -A agW -d x webtransport://127.0.0.1:27081/wt`

**Expected.** Consistency with the port's other upgrade-based schemes: since the EXT-033 fix, mqtt5+ws, signalr and socketio all send `-H`, `-A` and `-b` on the upgrade (needed behind an authenticating gateway). The CONNECT should carry `authorization: Bearer t` and `user-agent: agW`.

**Actual.** The CONNECT carries only `:method CONNECT`, `:protocol webtransport-h3`, `:scheme https`, `:authority 127.0.0.1:27081`, `:path /wt`. stdout `echo:x`, exit 0.

**Where to start.**
- `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportSession.cs` lines 66-73, `ConnectFields(WebTransportRequest)`, a fixed list. Append the `-H` fields, `user-agent` and cookies after the pseudo-headers.
- The model to follow is `Curl.Extended.Protocol.SocketIo.UnitLibrary/HandshakeHeaderFields.cs` (also copied into `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/`; ADR-1243): `Authorization` from `--oauth2-bearer` else `-u`, `User-Agent` from `-A` or curl's own, `Cookie` from `-b`, then each `-H` (`Name: value` replaces, `Name:` removes, `Name;` sends empty). Extension isolation forbids referencing another extension, so add a copy in the WebTransport library (a new internal `Session/WebTransportConnectHeaderFields.cs`), with field names lowercased as HTTP/3 requires and connection-specific fields never added. `WebTransportRequest.Of(CurlUrl, data)` builds the request, so carry the fields there or pass the transfer context to `ConnectFields`.
- A separate task makes `-v` write the connection and TLS lines for webtransport and also edits files in `Session/`; keep this change to the CONNECT fields.

## Acceptance criteria

- [ ] New tests in `Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportConnectHeaderFieldsTests.cs` (new) prove that `-H 'Authorization: Bearer t'` and `-A agW` appear in the CONNECT as `authorization: Bearer t` and `user-agent: agW` (lowercase names), `-b` appears as `cookie`, `--oauth2-bearer` and `-u` give the authorization field in the same precedence as `HandshakeHeaderFields`, and `-H 'Name:'` removes and `-H 'Name;'` sends empty.
- [ ] A test in `WebTransportSessionTests.cs` using `ScriptedHttp3Connection` proves the CONNECT request the server sees carries those fields after the pseudo-headers, and that with no options it carries only the default `user-agent` as the SocketIo model does and no other extra field.
- [ ] A test proves a `-H` naming a pseudo-header or `host` cannot replace or duplicate the pseudo-headers.
- [ ] `Documentation/Conformance/Extended/WebTransport.md` gains a row stating the CONNECT carries `-H`, `-A`, `-b` and the authorization, "Covered by" naming a test from this task; the `--extended-help` and `--ai-help` text for webtransport is checked and corrected if it says otherwise.
- [ ] The tests are platform-neutral (no real socket).
- [ ] `dotnet build Curl.Extended.Protocol.WebTransport.UnitLibrary -warnaserror` is clean, `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Review acceptance check, for a hand check: the logged CONNECT headers include `authorization: Bearer t` and `user-agent: agW`.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 9e1a412a 7905ccf7 b1df1197 f16ae3a5 d6cd4299 37433153.
