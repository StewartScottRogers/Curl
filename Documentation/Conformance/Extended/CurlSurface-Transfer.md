# CurlSurface: curl's transfer surface for every extended scheme

curl's generic transfer options and exit codes, checked for every scheme an extension serves through the real curl
command line, run in process (BL-4496). Expected values come from curl's manual page for curl 8.22.0
(curl.se/docs/manpage.html: "-v", "-o", "-O", "-w", "--fail", "--fail-with-body", "-m", "-s", "-S" and "EXIT CODES")
and, where an extension deliberately differs, from that extension's ADR, which the case names.

The checks, per scheme, and the ID part each check's rows carry (`CurlSurface:<check>-<scheme>-<n>`):

- `-v` (`verbose`): curl's connection lines (and for TLS schemes the TLS lines) on standard error, nothing extra on standard output.
- `-o file` (`output`): The body is written to the file, in a temporary directory.
- `-O` (`remote-name`): The body is written to the URL's file name, or `n/a` where the URL has none.
- `-w` (`write-out`): `%{exitcode}`, `%{errormsg}`, `%{url}`, `%{url_effective}`, `%{scheme}`, `%{remote_ip}`, `%{remote_port}`, `%{size_download}`, `%{time_total}`.
- `--fail` (`fail`): A server error the library maps to exit 22.
- `--fail-with-body` (`fail-with-body`): The same, with the body still written.
- `-m` (`max-time`): A server that never answers ends with exit 28 at the deadline on the injected clock.
- Connection refused (`connection-refused`): Exit 7.
- Unresolvable host (`unresolvable-host`): Exit 6.
- Bad port (`url-bad-port`): Exit 3.
- Bad percent-encoding (`url-bad-percent-encoding`): Exit 3.
- A space in the URL (`url-space`): Exit 3.
- `-s` (`silent`): No progress meter and no error message.
- `-S` (`show-error`): With `-s`, only the error message.

The rows live in one part matrix per group of schemes, each with its cases in the matching folder under
`Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/`:

- `CurlSurface-Transfer-UrlErrors.md`: every scheme; checks: connection refused, unresolvable host, bad port, bad percent-encoding, space.
- `CurlSurface-Transfer-Brokers-Amqp0.md`: `amqp`, `amqps`; checks: `-v`, `-o`, `-O`, `-w`, `--fail`, `--fail-with-body`, `-m`.
- `CurlSurface-Transfer-Brokers-Amqp1.md`: `amqp1`, `amqp1s`; checks: the same.
- `CurlSurface-Transfer-Brokers-Kafka.md`: `kafka`, `kafkas`; checks: the same.
- `CurlSurface-Transfer-Brokers-Nats.md`: `nats`, `natss`; checks: the same.
- `CurlSurface-Transfer-Brokers-Stomp.md`: `stomp`, `stomps`; checks: the same.
- `CurlSurface-Transfer-MessagingStreams-Coap.md`: `coap`, `coaps`; checks: the same.
- `CurlSurface-Transfer-MessagingStreams-Dns.md`: `dns`; checks: the same.
- `CurlSurface-Transfer-MessagingStreams-Mqtt5.md`: `mqtt5`, `mqtt5s`, `mqtt5+ws`; checks: the same.
- `CurlSurface-Transfer-MessagingStreams-Redis.md`: `redis`, `rediss`; checks: the same.
- `CurlSurface-Transfer-MessagingStreams-Xmpp.md`: `xmpp`, `xmpps`; checks: the same.
- `CurlSurface-Transfer-Storage.md`: `az`, `abfs`, `abfss`, `gs`, `oci`, `s3`; checks: the same.
- `CurlSurface-Transfer-Realtime.md`: `grpc`, `grpcs`, `signalr`, `signalrs`, `socketio`, `socketios`, `webtransport`, `mcp`, `mcps`; checks: the same.
- `CurlSurface-Transfer-Processes.md`: `mcp+stdio` and a `curl-proto-<scheme>` helper scheme; checks: the same.
- `CurlSurface-Transfer-Silent.md`: every scheme; checks: `-s`, `-S`.

The schemes come from each extension's description, not from this list: the requirement below fails when any scheme
an extension registers lacks a row for one of the checks above in the `CurlSurface-Transfer*.md` matrices.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| CurlSurface:scheme-coverage-1 | MUST | Every scheme an extension's description registers, and the `curl-proto-<scheme>` helper scheme, has a row in the `CurlSurface-Transfer*.md` matrices for every check above (BL-4496). | TransferSurfaceSchemeCoverageTests.EveryRegisteredScheme_HasARowForEveryTransferSurfaceCheck |
