# Curl.Conformance.Extended.UnitLibrary

The extended protocols' conformance harness (BL-4462, BL-4501): it runs a real curl command line
in process against in-memory reference servers and reports the exit code, standard output,
standard error and, per connection or datagram channel, the bytes each side sent.

- `Harness/` holds the shared harness: `ExtendedConformanceHarness.RunAsync` takes the curl entry
  point as an `InProcessCurlRunner` (the tests pass `Curl.Console`'s `InProcessCurl.RunAsync`, which
  this library cannot reference), the `ReferenceRoute`s (TCP, TLS or UDP, each on a port; every
  host name resolves to 127.0.0.1), the arguments, the environment and the clock (BL-4503: every
  timer of the run, `-m`'s among them, runs on it, so a test drives a timeout with a fake clock). `ScriptedReferenceServer`
  and `ScriptedDatagramServer` replay fixed bytes. Both sides' bytes are recorded at the
  reference server; on a TLS route (BL-4506) they are the plaintext behind the handshake, which
  `TlsServerStream` serves with the route's certificate (`ReferenceCertificate.CreateSelfSigned`
  makes one curl accepts under `-k`). A server that fails, such as a TLS accept curl never
  completes, makes `RunAsync` throw its exception. When curl ends, its end of every TCP connection is closed so
  each server reads what curl last sent and then the end of the stream (BL-4543); one still waiting
  after that, on anything else, is cancelled.
- Requirements (BL-4502, ADR-1186): a test carries `[SpecRequirement("<Name>:<source>-<locator>")]`;
  `RequirementMatrix` checks `Documentation/Conformance/Extended/<Name>.md` (copied to the test output)
  against the carriers the test project collects, and `KnownFailures.RunCaseAsync` runs a case listed in
  `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` so it passes while failing and fails once it passes.
- Each protocol's reference server goes in `<Name>/`, one file per server, registered nowhere.
- `Grpc/` (BL-4530) is a gRPC server written from PROTOCOL-HTTP2.md over HTTP/2 prior knowledge, on
  `Curl.Http2.UnitLibrary`'s `HpackEncoder`, `HpackDecoder` and frame codec (not
  `Curl.Conformance.Http2Server.UnitLibrary`, a relay to a backend). `GrpcReferenceServer` records each
  `GrpcReferenceCall` (decoded header list, DATA frames) and sends the `GrpcReferenceReply` of the first
  `IGrpcReferenceService` that answers; a case scripts its reply with `GrpcScriptedService`, and another
  service (such as reflection in `Grpc/Reflection/`) is its own `IGrpcReferenceService`.
- `WebTransport/Server/` (BL-4607) is a WebTransport over HTTP/3 server written from
  draft-ietf-webtrans-http3-16 on `Curl.Conformance.Http3Server.UnitLibrary`'s `Http3ServerConnection`
  (never from `Curl.Extended.Protocol.WebTransport`). `WebTransportReferenceServer` is an
  `IDatagramReferenceServer`; its `WebTransportServerScript` chooses the SETTINGS sent, the CONNECT
  status, the stream and datagram echoes and a `WT_CLOSE_SESSION`, and it records the client's SETTINGS,
  CONNECT fields, streams, datagrams, capsules and CONNECT FIN.
- It references `Curl.Protocol.Abstractions`, `Curl.Networking`, `Curl.Conformance.UnitLibrary`,
  `Curl.Conformance.Http3Server.UnitLibrary` and the shared layers only, never an extension
  (`VerifyExtensionIsolation`).
