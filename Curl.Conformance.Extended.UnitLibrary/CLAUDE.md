# Curl.Conformance.Extended.UnitLibrary

The extended protocols' conformance harness (BL-4462, BL-4501): it runs a real curl command line
in process against in-memory reference servers and reports the exit code, standard output,
standard error and, per connection or datagram channel, the bytes each side sent.

- `Harness/` holds the shared harness: `ExtendedConformanceHarness.RunAsync` takes the curl entry
  point as an `InProcessCurlRunner` (the tests pass `Curl.Console`'s `InProcessCurl.RunAsync`, which
  this library cannot reference), the `ReferenceRoute`s (TCP, TLS or UDP, each on a port; every
  host name resolves to 127.0.0.1), the arguments and the environment. `ScriptedReferenceServer`
  and `ScriptedDatagramServer` replay fixed bytes. Both sides' bytes are recorded at the
  reference server; on a TLS route (BL-4506) they are the plaintext behind the handshake, which
  `TlsServerStream` serves with the route's certificate (`ReferenceCertificate.CreateSelfSigned`
  makes one curl accepts under `-k`). A server that fails, such as a TLS accept curl never
  completes, makes `RunAsync` throw its exception; one still waiting when curl ends is cancelled.
- Each protocol's reference server goes in `<Name>/`, one file per server, registered nowhere.
- It references `Curl.Protocol.Abstractions`, `Curl.Networking`, `Curl.Conformance.UnitLibrary`
  and the shared layers only, never an extension (`VerifyExtensionIsolation`).
