---
id: BL-4676
title: Turn a KDC's closed UDP port into an IOException so the next KDC is tried
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/KerberosKdcSocketTransport.cs, Curl.Networking.UnitTests/KerberosKdcSocketTransportTests.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4676 — Turn a KDC's closed UDP port into an IOException so the next KDC is tried

## Goal

When a realm's first KDC has its UDP port closed, Kerberos tries the realm's next KDC instead of ending the HTTP transfer with exit 7 naming the KDC.

## Context

Found by the shift-end review (ADR-0710) of `f37ba71c...dfa29449`: silent-failure-hunter.

Since BL-4654, `UdpDatagramConnector` wraps every channel in `PortUnreachableReportingDatagramChannel`, including the KDC channels that `Curl.Networking.UnitLibrary/KerberosKdcSocketTransport.cs` (`ExchangeDatagramAsync`, ~line 25) opens. That method catches only `OperationCanceledException`. `KerberosKdcSender` (`Curl.Kerberos.UnitLibrary/KerberosKdcSender.cs:49`) fails over to the next KDC only on `IOException`. The new `DatagramPortUnreachableException` is a `SocketException`, so it gets past the failover and reaches `Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs`, which reports exit 7 `Failed to connect to <kdc>:88 ...` as if the web server had refused the connection.

Scenario: `curl --negotiate -u : http://web/` off Windows, with the first KDC in `krb5.conf` having UDP port 88 closed: exit 7 naming the KDC, and the next KDC is never tried.

Fix it inside `KerberosKdcSocketTransport.ExchangeDatagramAsync`: turn a `SocketException` from the send or receive into an `IOException` naming the KDC's host and port, so the failover runs (and `Curl.Kerberos` stays free of socket types).

## Acceptance criteria

- [ ] `KerberosKdcSocketTransportTests` has a test where the datagram channel's receive throws `DatagramPortUnreachableException` and `ExchangeDatagramAsync` throws `IOException`.
- [ ] No `SocketException` escapes `ExchangeDatagramAsync`, from send or receive.
- [ ] `dotnet build` is clean and the fast tests of `Curl.Networking.UnitTests` and `Curl.Kerberos.UnitTests` pass.

## Notes

## Log

- 2026-11-03: Created.
