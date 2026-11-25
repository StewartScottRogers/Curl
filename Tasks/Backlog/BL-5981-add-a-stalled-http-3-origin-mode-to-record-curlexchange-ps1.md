---
id: BL-5981
title: Add a stalled HTTP/3 origin mode to Record-CurlExchange.ps1
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5973]
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-11-25
completed:
---
# BL-5981 — Add a stalled HTTP/3 origin mode to Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -Http3StalledOrigin` serves a stalled HTTP/3 stream and records the QUIC events, so the measurement in ADR-1410 can be repeated.

## Context

BL-5978 and ADR-1410. BL-5978 wrote this mode and measured with it, but did not commit it because BL-5973 held the script. The patch is under Notes.

## Acceptance criteria

- [ ] `-Http3StalledOrigin` records an HTTP/3 curl's `-m` run: `transcript.txt` shows the STOP_SENDING error code and the decoder stream bytes.
- [ ] The switch has a `.PARAMETER` help entry and is refused with the other server modes.

## Notes

The block below is BL-5978's `git diff` of the script. Save it to a file and `git apply` it, resolving by hand where BL-5973's edits moved the lines.

````diff
diff --git a/Record-CurlExchange.ps1 b/Record-CurlExchange.ps1
index 7587629f52..dcf5ebb0b6 100644
--- a/Record-CurlExchange.ps1
+++ b/Record-CurlExchange.ps1
@@ -564,6 +564,18 @@
     frame's content is text, with \r and \n as CR and LF.
     Example: pwsh -File .\Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H :status: 200|X-Upper: v','D! hello h2\n' -Port 18080 -Response 'HTTP/1.1 200 OK\r\n\r\n' -Curl <nghttp2 curl> -CurlArgs '--http2-prior-knowledge','-sS','-i','http://127.0.0.1:18080/x' -OutDirectory fixtures\h2-upper
 
+.PARAMETER Http3StalledOrigin
+    Serve HTTP/3 on UDP ListenAddress and Port over System.Net.Quic (msquic) with the
+    throwaway certificate of -Tls, so curl needs -k and an HTTP/3 build (BL-5978). Every
+    request is answered with a 200 HEADERS frame and one DATA frame carrying the body of the
+    first Response (at most 16383 bytes), and its stream is never ended, so -m ends the
+    transfer. transcript.txt holds one line per QUIC event with the milliseconds since the
+    server was made: connections accepted, streams opened, bytes received, FINs, the
+    client's stream resets and stop-sendings and its connection close, each with its
+    application error code. request.bin holds the request streams' bytes. Needs pwsh;
+    refused with every other server mode.
+    Example: pwsh -File .\Record-CurlExchange.ps1 -Http3StalledOrigin -Port 18443 -Response 'HTTP/1.1 200 OK\r\n\r\nhello' -Curl <HTTP/3 curl> -CurlArgs '--http3-only','-k','-v','-m','2','https://127.0.0.1:18443/x' -OutDirectory fixtures\h3-stall
+
 .PARAMETER ProxyAlpn
     With -Http2, the one protocol the proxy accepts by ALPN: h2 (the default) or http/1.1,
     to measure curl falling back to an HTTP/1.1 CONNECT when the proxy will not speak
@@ -753,6 +765,7 @@ param(
     [int] $Http2ResetCode = -1,
     [ValidatePattern('^(|push|nostatus|status:.*)$')] [string] $Http2Answer = '',
     [switch] $Http2NoSettings,
+    [switch] $Http3StalledOrigin,
     [string[]] $Http2HeaderField = @(),
     [string[]] $Http2Frames = @(),
     [string] $Curl,
@@ -790,6 +803,9 @@ if ($Http2 -and -not $Tls -and -not $Http2Origin) { throw '-Http2 is an HTTPS pr
 if ($Http2 -and ($Ftp -or $Smtp -or $Imap -or $Pop3 -or $Script -or $NoServer -or $UnixSocket -or $Tftp)) { throw '-Http2 serves one HTTPS proxy connection, so it cannot be combined with -Ftp, -Smtp, -Imap, -Pop3, -Script, -NoServer, -UnixSocket or -Tftp.' }
 if ($Http2 -and $PSVersionTable.PSVersion.Major -lt 7) { throw '-Http2 needs PowerShell 7 (pwsh): Windows PowerShell 5.1''s .NET Framework cannot pick a protocol by ALPN as a TLS server.' }
 
+if ($Http3StalledOrigin -and ($Http2 -or $Ftp -or $Smtp -or $Imap -or $Pop3 -or $Script -or $Tls -or $NoServer -or $UnixSocket -or $Tftp -or $UdpSink)) { throw '-Http3StalledOrigin serves HTTP/3 on UDP -Port, so it cannot be combined with -Http2, -Http2Origin, -Ftp, -Smtp, -Imap, -Pop3, -Script, -Tls, -NoServer, -UnixSocket, -Tftp or -UdpSink.' }
+if ($Http3StalledOrigin -and $PSVersionTable.PSVersion.Major -lt 7) { throw '-Http3StalledOrigin needs PowerShell 7 (pwsh): Windows PowerShell 5.1''s .NET Framework has no System.Net.Quic.' }
+
 function Get-ReferenceCurlPath {
     # Git for Windows ships its curl in <Git>\ucrt64\bin from 2.56.0 (curl 8.22.0) and in
     # <Git>\mingw64\bin before it; git.exe lives in <Git>\cmd or <Git>\bin.
@@ -2200,7 +2216,7 @@ $uploadedData = New-Object System.IO.MemoryStream
 $OutDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine((Get-Location).ProviderPath, $OutDirectory))
 New-Item -ItemType Directory -Path $OutDirectory -Force | Out-Null
 
-$tlsCertificate = if ($Tls -or $Ftp -or $Smtp -or $Imap -or $Pop3 -or $DohPort.Count -gt 0) { New-ThrowawayTlsCertificate -RootCertificateFile $TlsRootCertificateFile } else { $null }
+$tlsCertificate = if ($Tls -or $Http3StalledOrigin -or $Ftp -or $Smtp -or $Imap -or $Pop3 -or $DohPort.Count -gt 0) { New-ThrowawayTlsCertificate -RootCertificateFile $TlsRootCertificateFile } else { $null }
 if ($null -ne $tlsCertificate) {
     $publicKeyInfo = Get-SubjectPublicKeyInfo -Certificate $tlsCertificate
     $publicKeySha256 = [System.Convert]::ToBase64String([System.Security.Cryptography.SHA256]::Create().ComputeHash($publicKeyInfo))
@@ -2263,7 +2279,7 @@ public sealed class RecorderUnixSocketListener
     if (Test-Path -LiteralPath $UnixSocket) { Remove-Item -LiteralPath $UnixSocket -Force }
     $listener = New-Object RecorderUnixSocketListener($UnixSocket)
     $server = [System.Management.Automation.PowerShell]::Create()
-} elseif (-not $NoServer -and -not $Tftp) {
+} elseif (-not $NoServer -and -not $Tftp -and -not $Http3StalledOrigin) {
     $listener = New-Object System.Net.Sockets.TcpListener($ListenAddress, $Port)
     $listener.Start()
     $server = [System.Management.Automation.PowerShell]::Create()
@@ -3410,6 +3426,182 @@ public sealed class RecorderTftpResponder
     $tftpReplies = [string[]] @($tftpSteps | ForEach-Object { $tftpOverrides[$_][0] })
     $tftpResponder = New-Object RecorderTftpResponder($ListenAddress, $Port, [byte[]] (ConvertFrom-EscapedResponse -Text $TftpData), [bool] $TftpNoOack, $TftpIdleMilliseconds, $tftpSteps, $tftpReplies)
 }
+$http3Origin = $null
+if ($Http3StalledOrigin) {
+    # System.Net.Quic (msquic) is the server; the HTTP/3 frames are the few bytes written here.
+    # The QPACK field section is two zero prefix bytes and static index 25, ':status: 200'.
+    Add-Type -TypeDefinition @'
+using System;
+using System.Collections.Generic;
+using System.Diagnostics;
+using System.IO;
+using System.Net;
+using System.Net.Quic;
+using System.Net.Security;
+using System.Security.Cryptography.X509Certificates;
+using System.Text;
+using System.Threading;
+using System.Threading.Tasks;
+
+public sealed class RecorderHttp3StalledOrigin
+{
+    private readonly StringBuilder _log = new StringBuilder();
+    private readonly MemoryStream _received = new MemoryStream();
+    private readonly Stopwatch _clock = Stopwatch.StartNew();
+    private readonly CancellationTokenSource _stop = new CancellationTokenSource();
+    private readonly List<object> _kept = new List<object>();
+    private readonly IPEndPoint _endPoint;
+    private readonly X509Certificate2 _certificate;
+    private readonly byte[] _body;
+    private QuicListener _listener;
+    private int _connections;
+
+    public RecorderHttp3StalledOrigin(IPAddress address, int port, X509Certificate2 certificate, byte[] body)
+    {
+        if (body.Length > 16383) { throw new ArgumentException("The HTTP/3 body must be at most 16383 bytes."); }
+        _endPoint = new IPEndPoint(address, port);
+        _certificate = certificate;
+        _body = body;
+    }
+
+    public string Log { get { lock (_log) { return _log.ToString(); } } }
+
+    public byte[] Received { get { lock (_received) { return _received.ToArray(); } } }
+
+    public void Start()
+    {
+        var protocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 };
+        _listener = QuicListener.ListenAsync(new QuicListenerOptions
+        {
+            ListenEndPoint = _endPoint,
+            ApplicationProtocols = protocols,
+            ConnectionOptionsCallback = (connection, hello, token) => new ValueTask<QuicServerConnectionOptions>(new QuicServerConnectionOptions
+            {
+                DefaultStreamErrorCode = 0x10c,
+                DefaultCloseErrorCode = 0x100,
+                IdleTimeout = TimeSpan.FromMinutes(5),
+                MaxInboundBidirectionalStreams = 100,
+                MaxInboundUnidirectionalStreams = 10,
+                ServerAuthenticationOptions = new SslServerAuthenticationOptions { ServerCertificate = _certificate, ApplicationProtocols = protocols },
+            }),
+        }).AsTask().GetAwaiter().GetResult();
+        Task.Run(AcceptConnectionsAsync);
+    }
+
+    public void Finish()
+    {
+        // Give the client's last frames a moment to arrive before the endpoint goes.
+        Thread.Sleep(1000);
+        _stop.Cancel();
+        _listener.DisposeAsync().AsTask().Wait(2000);
+    }
+
+    private async Task AcceptConnectionsAsync()
+    {
+        while (!_stop.IsCancellationRequested)
+        {
+            try
+            {
+                QuicConnection connection = await _listener.AcceptConnectionAsync(_stop.Token);
+                int number = ++_connections;
+                Record("connection " + number + " accepted");
+                Task serve = Task.Run(() => ServeConnectionAsync(connection, number));
+            }
+            catch (Exception error)
+            {
+                if (!_stop.IsCancellationRequested) { Record("accept failed: " + error.Message); }
+                return;
+            }
+        }
+    }
+
+    private async Task ServeConnectionAsync(QuicConnection connection, int number)
+    {
+        try
+        {
+            QuicStream control = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, _stop.Token);
+            lock (_kept) { _kept.Add(connection); _kept.Add(control); }
+            // Stream type 0 (control), then an empty SETTINGS frame.
+            await control.WriteAsync(new byte[] { 0x00, 0x04, 0x00 }, _stop.Token);
+            while (true)
+            {
+                QuicStream stream = await connection.AcceptInboundStreamAsync(_stop.Token);
+                lock (_kept) { _kept.Add(stream); }
+                string name = "connection " + number + " stream " + stream.Id;
+                Record(name + " opened (" + stream.Type + ")");
+                Task read = Task.Run(() => ReadStreamAsync(stream, name));
+                if (stream.Type == QuicStreamType.Bidirectional)
+                {
+                    Task watch = Task.Run(() => WatchWritesAsync(stream, name));
+                }
+            }
+        }
+        catch (QuicException error)
+        {
+            Record("connection " + number + " ended: " + Describe(error));
+        }
+        catch (OperationCanceledException) { }
+    }
+
+    private async Task ReadStreamAsync(QuicStream stream, string name)
+    {
+        var buffer = new byte[16384];
+        bool isAnswered = false;
+        try
+        {
+            while (true)
+            {
+                int count = await stream.ReadAsync(buffer, _stop.Token);
+                if (count == 0) { Record(name + " received FIN"); return; }
+                Record(name + " received " + BitConverter.ToString(buffer, 0, Math.Min(count, 64)).Replace("-", string.Empty).ToLowerInvariant() + " (" + count + " bytes)");
+                if (stream.Type != QuicStreamType.Bidirectional) { continue; }
+                lock (_received) { _received.Write(buffer, 0, count); }
+                if (isAnswered) { continue; }
+                isAnswered = true;
+                await AnswerAsync(stream);
+                Record(name + " sent HEADERS :status 200 and DATA of " + _body.Length + " bytes, stream left open");
+            }
+        }
+        catch (QuicException error) { Record(name + " read ended: " + Describe(error)); }
+        catch (OperationCanceledException) { }
+    }
+
+    private async Task AnswerAsync(QuicStream stream)
+    {
+        var answer = new MemoryStream();
+        answer.Write(new byte[] { 0x01, 0x03, 0x00, 0x00, 0xd9 }, 0, 5);
+        if (_body.Length > 0)
+        {
+            // DATA, its length as a two-byte variable-length integer.
+            answer.Write(new byte[] { 0x00, (byte)(0x40 | (_body.Length >> 8)), (byte)_body.Length }, 0, 3);
+            answer.Write(_body, 0, _body.Length);
+        }
+        await stream.WriteAsync(answer.ToArray(), _stop.Token);
+    }
+
+    private async Task WatchWritesAsync(QuicStream stream, string name)
+    {
+        try { await stream.WritesClosed; }
+        catch (QuicException error) { Record(name + " writes closed by the client: " + Describe(error)); }
+        catch (OperationCanceledException) { }
+    }
+
+    private static string Describe(QuicException error)
+    {
+        return error.QuicError + (error.ApplicationErrorCode.HasValue ? " 0x" + error.ApplicationErrorCode.Value.ToString("x") : string.Empty);
+    }
+
+    private void Record(string line)
+    {
+        lock (_log) { _log.Append(_clock.ElapsedMilliseconds).Append(" ms ").Append(line).Append('\n'); }
+    }
+}
+'@
+    $body = [System.Text.Encoding]::GetEncoding(28591).GetString($responseBytes[0])
+    $bodyStart = $body.IndexOf("`r`n`r`n")
+    $body = if ($bodyStart -ge 0) { $body.Substring($bodyStart + 4) } else { '' }
+    $http3Origin = New-Object RecorderHttp3StalledOrigin($ListenAddress, $Port, $tlsCertificate, [System.Text.Encoding]::GetEncoding(28591).GetBytes($body))
+}
 # The sink never answers; the datagrams wait in its receive buffer until curl exits.
 $udpSinkClient = $null
 if ($UdpSink) {
@@ -3419,6 +3611,9 @@ if ($UdpSink) {
 try {
     if ($NoServer -or $Tftp) {
         $serverRun = $null
+    } elseif ($Http3StalledOrigin) {
+        $serverRun = $null
+        $http3Origin.Start()
     } elseif ($Http2) {
         $serverRun = $null
         $http2Proxy.Start()
@@ -3506,6 +3701,11 @@ try {
     }
 
     if ($null -ne $tftpResponder) { $tftpResponder.Stop() }
+    if ($null -ne $http3Origin) {
+        $http3Origin.Finish()
+        $requests = @(, @(, $http3Origin.Received))
+        [void] $transcript.Append($http3Origin.Log)
+    }
 
     $udpDatagrams = New-Object System.Text.StringBuilder
     if ($null -ne $udpSinkClient) {
@@ -3549,7 +3749,7 @@ if ($Tftp) {
 if ($null -ne $dnsResponder) {
     [System.IO.File]::WriteAllText((Join-Path $OutDirectory 'dns.txt'), $dnsResponder.Log, [System.Text.Encoding]::ASCII)
 }
-if ($Smtp -or $Imap -or $Pop3 -or $Script -or $Http2) {
+if ($Smtp -or $Imap -or $Pop3 -or $Script -or $Http2 -or $Http3StalledOrigin) {
     [System.IO.File]::WriteAllText((Join-Path $OutDirectory 'transcript.txt'), $transcript.ToString(), [System.Text.Encoding]::GetEncoding(28591))
 }
 $curlExecutable = $Curl
````

## Log

- 2026-11-25: Created.
