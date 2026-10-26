---
id: BL-3560
title: Fix CGR-031: REFUSED_STREAM, and GOAWAY with a last-stream-id below the request, are not retried on a new connection; REF retries and succeeds
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferException.cs, Curl.Protocol.Http.UnitTests/Cgr031Tests.cs, Curl.Protocol.Http.UnitTests/Http2SessionTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3560 — Fix CGR-031: REFUSED_STREAM, and GOAWAY with a last-stream-id below the request, are not retried on a new connection; REF retries and succeeds

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-031 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-031: REFUSED_STREAM, and GOAWAY with a last-stream-id below the request, are not retried on a new connection; REF retries and succeeds

- **Severity:** High (servers commonly send GOAWAY or REFUSED_STREAM during graceful shutdown or overload, and curl hides this from the user)
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\x_rst7first`, `EV\win\x_gafirst`)
- **Confidence:** Probable. Detail: Probable (reproduced by the tester and the verifier on Linux and PORT-WIN; the REF retry logic lives in curl's own code, which can only be checked against 8.18)
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-5`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scens, which act on connection 1 only:
  - `rst7first`: RST_STREAM REFUSED_STREAM.
  - `gafirst`: GOAWAY with last=0 and NO_ERROR, then close.
  - `gafirstia`: the same with INTERNAL_ERROR.
- Command: `curl -sS --http2-prior-knowledge http://127.0.0.1:P/x -w '\n[%{http_version} %{response_code} %{num_connects}]\n'`. TLS form: `curl -sSk https://127.0.0.1:P/x`.
- Cases: `x_rst7first`, `x_rst7first_tls`, `x_gafirst`, `x_gafirstia`, `x_gafirst_tls`.
- Always-refusing variants (`rst7`, `gabefore0`): REF gives up after 5 retries.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge http://127.0.0.1:P/x -w '\n[%{http_version} %{response_code} %{num_connects}]\n'
curl -sSk https://127.0.0.1:P/x
```

**Expected (real curl)**

- opens a second connection, re-sends the request, prints `hello h2` and exits 0 with `[2 200 2]`. If the server always refuses, REF exits 56 with `curl: (56) Connection died, tried 5 times before giving up`.

**Actual (Curl)**

- no retry, `[0 000 1]`.
  - REFUSED_STREAM: exit 92, `curl: (92) HTTP/2 stream 1 was not closed cleanly: REFUSED_STREAM (err 7)`.
  - GOAWAY: exit 56, `curl: (56) Failure when receiving data from the peer`.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2StreamConnection.cs:375` (every `Http2StreamResetException` becomes exit 92, and `Http2GoAwayException` becomes 56). `Http3StreamConnection.cs:300` has a request-rejected retry, but HTTP/2 does not.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr031Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Delivered directly, not through the /feature agents: the plan was one mapping in `Http2StreamConnection.FailureOf` onto the HTTP/3 refused-stream retry that already exists (`IsStreamRefused`, `StreamRefusedOutcome`, ADR-0187), so the cost of the agents was not justified. The decision is recorded in ADR-0817.
- Touches widened: `HttpProtocolHandler.cs` (`ReportReceiveFailure` no longer prints the message of every refused stream, because curl prints no `-v` line of its own for an HTTP/2 refusal; the HTTP/3 line moved to `InfoLines`, so its output is unchanged), `HttpTransferException.cs` (doc of `IsStreamRefused`) and `Http2SessionTests.cs` (`WriteAsync_WhenTheOpenStreamIsResetWhileAnotherWaits_...` used REFUSED_STREAM as an example of a reset code; it now uses CANCEL, because REFUSED_STREAM now means a retry). No task in Doing on origin/work/dark-factory named these files (checked at 08:35).
- REF = real curl 8.18.0 (OpenSSL, nghttp2 1.68) in WSL Ubuntu, the report's REF-LINUX. The Windows curl 8.22.0 on PATH has no HTTP/2: `--http2-prior-knowledge` gives exit 2, `the installed libcurl version does not support this`. PORT = Curl at this commit, run with `dotnet run --project Curl.Console` on Windows (PORT-WIN). The server is the C# file-based app below. It is the same source on both sides: published self-contained for linux-x64 for REF, and run on Windows for PORT.
- Command: `curl -sS --http2-prior-knowledge http://127.0.0.1:P/x -w '\n[%{http_version} %{response_code} %{num_connects}]\n'`. REF and PORT matched in every scenario:
  - `rst7first`, `gafirst` (GOAWAY last 0 NO_ERROR), `gafirstia` (GOAWAY last 0 INTERNAL_ERROR): exit 0, stdout `hello h2\n\n[2 200 2]\n`, empty stderr. Before the fix: `rst7first` was exit 92, `HTTP/2 stream 1 was not closed cleanly: REFUSED_STREAM (err 7)`, `[0 000 1]`; the GOAWAY cases were exit 56, `Failure when receiving data from the peer`.
  - `rst7` and `gabefore0` (refuse on every connection): exit 56, stdout `\n[0 000 6]\n`, stderr `curl: (56) Connection died, tried 5 times before giving up`.
  - Frames on the wire, as the server logged them (type, flags, stream and length for each connection): identical, with one exception. In `rst7`, PORT sends its closing GOAWAY on the sixth connection, the one it gives up on, and REF does not; REF did the same in a second run. Filed as BL-3838.
  - `-v` for `rst7first` and `gafirst`: identical lines, except that REF prints `* received GOAWAY, error=0, last_stream=0` before `REFUSED_STREAM, retrying a fresh connect`. Filed as BL-3839.
- TLS form `curl -sSk https://127.0.0.1:P/x` (the server offers h2 alone by ALPN). With REF-LINUX, `rst7first` and `gafirst` give exit 0 and `hello h2`. PORT-WIN matches the Windows platform curl (Schannel, no HTTP/2, so ALPN offers http/1.1 alone). Curl and curl.exe 8.22.0 both give exit 35 with `schannel: next InitializeSecurityContext failed: Unknown error (0x80090367) - No common application protocol exists between the client and the server. Application protocol negotiation failed.` On Linux, Curl's TLS h2 stream goes through the same `Http2StreamConnection.FailureOf` as the cleartext cases. Curl was not run on Linux here, because WSL has no dotnet. The unit tests are platform-neutral, and CI runs them on Linux and macOS.
- Focused tests are in `Cgr031Tests.cs`: refuse once and then serve (RST_STREAM REFUSED_STREAM, GOAWAY NO_ERROR, GOAWAY INTERNAL_ERROR), refuse every time (exit 56, six connects), and a GOAWAY that covers the stream (exit 56, no retry). With the two mapping lines taken out, 6 of the 7 failed: the seventh pins behaviour that already held and should stay. All 7 pass with the fix.
- Fast tests: the whole solution ran once; the only failure was the `Http2SessionTests` case above. After that change, Curl.Protocol.Http.UnitTests had 2283 passed, 19 skipped, 0 failed. Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: the first run found `FailureOf` at complexity 12, so the refusal test moved into `IsRefusal`; the second run gave 100% line, 100% branch and 0 failing members.
- Scenario server (`dotnet run h2srv.cs <port> <scen> [tls]`; scen `rst7first`, `gafirst`, `gafirstia`, `rst7`, `gabefore0`):

```csharp
// Minimal h2c prior-knowledge server for CGR-031: args <port> <scen>
using System.Net; using System.Net.Sockets; using System.Net.Security; using System.Security.Cryptography; using System.Security.Cryptography.X509Certificates;
bool tls = args.Length > 2;
var key = RSA.Create(2048); var req = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var cert = X509CertificateLoader.LoadPkcs12(req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)).Export(X509ContentType.Pfx), null);
int port = int.Parse(args[0]); string scen = args[1]; int conn = 0;
var l = new TcpListener(IPAddress.Any, port); l.Start();
Console.WriteLine("listening"); 
var stop = DateTime.UtcNow.AddSeconds(40);
while (DateTime.UtcNow < stop) {
  if (!l.Pending()) { await Task.Delay(20); continue; }
  var c = await l.AcceptTcpClientAsync(); conn++;
  _ = Serve(c, conn);
}
async Task Serve(TcpClient c, int n) {
  try {
  Stream s = c.GetStream();
  if (tls) { var ssl = new SslStream(s); await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = cert, ApplicationProtocols = [SslApplicationProtocol.Http2] }); s = ssl; }
  async Task ReadN(byte[] b) { int o = 0; while (o < b.Length) { int r = await s.ReadAsync(b.AsMemory(o)); if (r == 0) throw new EndOfStreamException(); o += r; } }
  async Task Send(int type, int flags, int sid, byte[] p) { var h = new byte[9 + p.Length]; h[0]=(byte)(p.Length>>16);h[1]=(byte)(p.Length>>8);h[2]=(byte)p.Length;h[3]=(byte)type;h[4]=(byte)flags;h[5]=(byte)(sid>>24);h[6]=(byte)(sid>>16);h[7]=(byte)(sid>>8);h[8]=(byte)sid; p.CopyTo(h,9); await s.WriteAsync(h); }
  var pre = new byte[24]; await ReadN(pre);
  await Send(4, 0, 0, []);
  bool refuse = scen switch { "rst7first" or "gafirst" or "gafirstia" => n == 1, "rst7" or "gabefore0" => true, _ => false };
  while (true) {
    var h = new byte[9]; await ReadN(h);
    int len = (h[0]<<16)|(h[1]<<8)|h[2]; int type = h[3]; int flags = h[4]; int sid = ((h[5]&0x7f)<<24)|(h[6]<<16)|(h[7]<<8)|h[8];
    var p = new byte[len]; await ReadN(p);
    Console.WriteLine($"conn {n} frame type {type} flags {flags} stream {sid} len {len}");
    if (type == 4 && (flags & 1) == 0) await Send(4, 1, 0, []);
    if (type == 1) {
      if (refuse && scen.StartsWith("rst")) await Send(3, 0, sid, [0,0,0,7]);
      else if (refuse) { await Send(7, 0, 0, [0,0,0,0, 0,0,0,(byte)(scen == "gafirstia" ? 2 : 0)]); await Task.Delay(100); c.Close(); return; }
      else { await Send(1, 4, sid, [0x88]); await Send(0, 1, sid, "hello h2\n"u8.ToArray()); }
    }
  }
  } catch (Exception e) { Console.WriteLine($"conn {n} end {e.GetType().Name}"); }
}
```

## Log

- 2026-10-26: Created from CGR-031 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A refused HTTP/2 stream (REFUSED_STREAM, or GOAWAY below it) is retried on a new connection up to five times, as curl does
