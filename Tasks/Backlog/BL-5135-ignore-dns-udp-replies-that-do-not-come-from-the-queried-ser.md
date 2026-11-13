---
id: BL-5135
title: Ignore dns:// UDP replies that do not come from the queried server's address and port (EXT-046)
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Udp/DnsUdpTransport.cs, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport, Documentation/Conformance/Extended/DnsQuery.md]
requirement: none
created: 2026-11-13
completed:
---
# BL-5135 — Ignore dns:// UDP replies that do not come from the queried server's address and port (EXT-046)

## Goal

A `dns://` query over UDP takes only a reply whose source address and port are the server that was queried, so an off-path datagram from another address or port cannot supply the answer.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-046 (Medium, Confirmed, twice per scheme; DNS re-reproduced by the orchestrator). Weakens security: off-path response spoofing. The CoAP half of the same finding is filed as its own task (`Curl.Extended.Protocol.Coap`); do not touch it here, and do not change `Curl.Networking.UnitLibrary/UdpDatagramChannel.cs` (it is unconnected on purpose for TFTP's RFC 1350 transfer IDs).

**Repro.** Run this script, then `curl -sS -m 5 dns://127.0.0.1:29061/example.com` (port the script to PowerShell or a C# file-based app; never Python):
```python
import socket,struct,time
g=socket.socket(socket.AF_INET,socket.SOCK_DGRAM); g.bind(("127.0.0.1",29061))
s=socket.socket(socket.AF_INET,socket.SOCK_DGRAM); s.bind(("127.0.0.2",29062))
def ans(q,ip):
    qd=q[12:]; qsec=qd[:qd.index(b"\0")+5]
    return q[:2]+b"\x81\x80"+struct.pack(">HHHH",1,1,0,0)+qsec+b"\xc0\x0c"+struct.pack(">HHIH",1,1,300,4)+bytes(ip)
q,a=g.recvfrom(512)
s.sendto(ans(q,[6,6,6,6]),a); time.sleep(0.3); g.sendto(ans(q,[1,2,3,4]),a)
```
That is: the queried server's socket (127.0.0.1:29061) receives the query; a different socket (127.0.0.2:29062) first sends a valid-looking answer with `6.6.6.6`; 0.3 s later the real server socket sends the answer with `1.2.3.4`.

**Expected.** RFC 5452 section 9.1 requires matching the response's source address and port as well as the ID and question; `Documentation/Conformance/Extended/DnsQuery.md` applies section 9.1 only to the ID (`DnsQuery:RFC5452-9.1-1`) and question (`-9.1-2`). Output should be `example.com. 300 IN A 1.2.3.4`.

**Actual.** `example.com.	300	IN	A	6.6.6.6`, exit 0. A reply from another port on the same IP is also accepted.

**Where to start.** `Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Udp/DnsUdpTransport.cs`, `ReceiveAnswerAsync` (about line 108): `channel.ReceiveAsync` returns `DatagramReceived(Length, RemoteEndPoint)` and then only `IsAnswerTo` is checked, never `RemoteEndPoint` against `channel.ServerEndPoint`. Drop datagrams whose source is not the server endpoint and keep waiting (the attempt timeout still applies). Compare address and port; treat an IPv4-mapped IPv6 address as equal to its IPv4 form.

## Acceptance criteria

- [ ] New tests in `Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/DnsUdpSourceEndpointTests.cs` (new, using the existing `ScriptedDnsServer` or an in-memory `IDatagramChannel`) prove that a well-formed reply with the right ID and question from another IP is ignored and the genuine reply after it is the answer.
- [ ] A test proves the same for a reply from the right IP but another port, and a test proves that when only spoofed replies arrive the attempt times out like a lost reply (the existing retry path) instead of accepting one.
- [ ] A test proves a reply from the server endpoint written in IPv4-mapped IPv6 form is still accepted, and the existing UDP tests stay green.
- [ ] `Documentation/Conformance/Extended/DnsQuery.md` gains a MUST row (RFC 5452 section 9.1, source address and port of a UDP reply) whose "Covered by" names a test from this task.
- [ ] The tests are platform-neutral (no real socket outside loopback unit tests already used, no second loopback address such as 127.0.0.2 in a unit test, since macOS does not route it by default).
- [ ] `dotnet build Curl.Extended.Protocol.DnsQuery.UnitLibrary -warnaserror` is clean, `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The review's mock servers were Python; use PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python. Keep the 100% line and branch coverage gate for the library.
- Review acceptance check, for a hand check: the DNS command prints `example.com. 300 IN A 1.2.3.4` (the genuine answer) and exits 0.

## Log

- 2026-11-13: Created.
