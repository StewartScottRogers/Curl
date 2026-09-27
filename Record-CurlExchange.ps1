<#
.SYNOPSIS
    Records one real curl exchange against a canned loopback server, as test fixtures:
    the request bytes curl sent, its stdout, its stderr and its exit code.

.DESCRIPTION
    Expected bytes in Curl's HTTP tests come from real curl, measured first. This script
    is how they are measured. It binds 127.0.0.1:<Port> with
    System.Net.Sockets.TcpListener, runs curl with the given arguments, and for each of
    the first <Connections> connections curl opens it:

      1. reads the request until the header block ends (CRLF CRLF) and, when the
         headers carry Content-Length or Transfer-Encoding: chunked, until the body has
         arrived - each read waits at most one second, so a body that never comes
         ends the read instead of hanging it;
      2. records the raw request bytes;
      3. sends that connection's canned response and closes the connection.

    It then writes four files to OutDirectory:

      request.bin   the raw request bytes, every connection's in the order accepted
      stdout.bin    curl's standard output, byte for byte
      stderr.txt    curl's standard error, byte for byte
      exitcode.txt  curl's exit code, as decimal digits with no line ending

    A connection curl never opens is not waited for: once curl exits, the listener is
    stopped. The script exits 0 when the fixtures were written, whatever curl's own
    exit code was; recording a failing curl is as useful as recording a passing one.

.PARAMETER Port
    The loopback TCP port to listen on. The URL in CurlArgs must use the same port.

.PARAMETER Response
    The canned response, as a string with backslash escapes: \r \n \t \0 \\ \" \' and
    \xHH (two hex digits). After decoding, each character becomes one byte (Latin-1),
    so a character above U+00FF is an error. The response is sent exactly as given;
    the script adds nothing. Defaults to an empty 200 with Content-Length: 0.
    Give several, one per connection, to answer a followed redirect's hops
    differently: connection N gets the Nth, and every connection past the last gets
    the last.

.PARAMETER CurlArgs
    The arguments passed to curl, one per element. Each is quoted for the Windows
    command line as needed, so an argument with spaces or quotes reaches curl intact.
    Run the script from PowerShell (& or .\) to pass several; powershell -File hands a
    comma-separated list to the script as one string.

.PARAMETER OutDirectory
    Where the four fixture files are written. Created if missing; existing fixture
    files there are overwritten.

.PARAMETER Connections
    How many connections to serve. Default 1. Use more for a run that makes several
    requests, such as several URLs or a followed redirect.

.PARAMETER ResponseDelayMilliseconds
    How long to wait after reading each request before sending the response. Default 0.
    Use it to make a hop take a known time, as when measuring how -m counts across a
    followed redirect.

.PARAMETER Reset
    Instead of reading a request and answering it, reset each connection as soon as it
    is accepted: the socket closes with a zero linger time, so Windows sends a TCP RST
    rather than a FIN. Use it to measure what curl prints when the server resets the
    connection, as during a TLS handshake (BL-369). request.bin is then empty.

.PARAMETER RespondAfterBodyBytes
    Instead of reading the whole request, send the response as soon as the header block
    and this many body bytes have arrived, then go on reading (and recording) whatever
    curl still sends until it stops for two seconds or closes its end. Default -1: off.
    Each read then waits up to five seconds, so curl's one-second wait for
    100 Continue does not end the read early. Use it to measure a response that arrives
    while the body is being sent (BL-319, BL-395).

.PARAMETER StandardInput
    What curl reads from standard input, with the same backslash escapes as Response.
    It is written in full and then standard input is closed. Default empty: standard
    input is closed at once. Use it to measure an option that reads '-', such as
    -b - (BL-316).

.PARAMETER Ftp
    Serve one FTP session instead of HTTP responses: send a greeting, then read curl's
    commands one line at a time and answer each from a table of replies, opening a
    passive data connection for EPSV, PASV, RETR, LIST, STOR and APPE. request.bin then holds every
    command line curl sent on the control connection, and transcript.txt holds both
    directions, each line prefixed "> " (curl) or "< " (server). Response,
    Connections, ResponseDelayMilliseconds and Reset are ignored.

    The default replies are: greeting 220, USER 331, PASS 230, PWD 257 "/", EPSV 229
    with the data port, PASV 227 with 127.0.0.1 and the data port, TYPE 200, SIZE 213
    with FtpData's length, MDTM 213 20260927123456, CWD 250, REST 350 (remembering the
    offset), RETR 150 then FtpData from the last REST offset over the data connection
    then 226 (LIST the same), STOR 150 then every byte curl sends over the data
    connection until it closes it then 226 (APPE the same), QUIT 221 (and the session
    ends), and 502 for any other command. A data connection curl closes early (a range
    read) is not an error. The bytes received on STOR's and APPE's data connections are
    written to upload.bin, and each upload adds one "= <n> bytes received on the data
    connection: <bytes>" line to transcript.txt (BL-439).

.PARAMETER FtpReply
    Overrides for the FTP reply table, each 'VERB=reply' with the same backslash escapes
    as Response, e.g. 'PASS=430 Access denied'. The reply is sent as given with CRLF
    appended. VERB is a command name in capitals, GREETING for the greeting, or RETRDONE
    for the reply sent after RETR's or LIST's data, or STORDONE for the reply sent after
    STOR's or APPE's data. An overridden EPSV, PASV, RETR, LIST, STOR or APPE sends only
    the reply: no data connection is offered. The reply CLOSE closes the control
    connection instead of answering, e.g. 'PWD=CLOSE'.

.PARAMETER FtpData
    The file served by RETR, and the listing served by LIST, in -Ftp mode, with the
    same backslash escapes as Response.
    Default empty.

.PARAMETER Curl
    The curl executable to run. Defaults to the reference build ADR-0009 and ADR-0018
    name, curl 8.21.0 from Git for Windows' mingw64 directory, found beside git.exe.

.EXAMPLE
    .\Record-CurlExchange.ps1 -Port 18081 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello' -CurlArgs 'http://127.0.0.1:18081/a?b' -OutDirectory fixtures\default-get

    request.bin then holds
    GET /a?b HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n
    and stdout.bin holds hello.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [ValidateRange(1, 65535)] [int] $Port,
    [string[]] $Response = @('HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n'),
    [Parameter(Mandatory = $true)] [string[]] $CurlArgs,
    [Parameter(Mandatory = $true)] [string] $OutDirectory,
    [ValidateRange(1, 1000)] [int] $Connections = 1,
    [ValidateRange(0, 600000)] [int] $ResponseDelayMilliseconds = 0,
    [switch] $Reset,
    [ValidateRange(-1, [int]::MaxValue)] [int] $RespondAfterBodyBytes = -1,
    [string] $StandardInput = '',
    [switch] $Ftp,
    [string[]] $FtpReply = @(),
    [string] $FtpData = '',
    [string] $Curl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ReferenceCurlPath {
    $git = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($null -ne $git) {
        # git.exe lives in <Git>\cmd or <Git>\bin; the reference curl in <Git>\mingw64\bin.
        $gitRoot = Split-Path (Split-Path $git.Source -Parent) -Parent
        $candidate = Join-Path $gitRoot 'mingw64\bin\curl.exe'
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    $candidate = Join-Path $env:ProgramFiles 'Git\mingw64\bin\curl.exe'
    if (Test-Path -LiteralPath $candidate) { return $candidate }
    throw 'The reference curl (Git for Windows mingw64\bin\curl.exe) was not found; pass -Curl.'
}

function ConvertFrom-EscapedResponse {
    param([string] $Text)
    $bytes = New-Object System.Collections.Generic.List[byte]
    $index = 0
    while ($index -lt $Text.Length) {
        $character = $Text[$index]
        if ($character -ne '\') {
            if ([int] $character -gt 255) { throw "Response character U+$(([int] $character).ToString('X4')) is not Latin-1." }
            $bytes.Add([byte] [int] $character)
            $index++
            continue
        }
        if ($index + 1 -ge $Text.Length) { throw 'Response ends with a lone backslash.' }
        $escape = $Text[$index + 1]
        switch -CaseSensitive ([string] $escape) {
            'r' { $bytes.Add(13); $index += 2 }
            'n' { $bytes.Add(10); $index += 2 }
            't' { $bytes.Add(9); $index += 2 }
            '0' { $bytes.Add(0); $index += 2 }
            '\' { $bytes.Add(92); $index += 2 }
            '"' { $bytes.Add(34); $index += 2 }
            "'" { $bytes.Add(39); $index += 2 }
            'x' {
                $hex = if ($index + 4 -le $Text.Length) { $Text.Substring($index + 2, 2) } else { '' }
                if ($hex -notmatch '^[0-9A-Fa-f]{2}$') { throw "Response has a malformed \x escape at index $index." }
                $bytes.Add([Convert]::ToByte($hex, 16))
                $index += 4
            }
            default { throw "Response has an unknown escape \$escape at index $index." }
        }
    }
    return , $bytes.ToArray()
}

function ConvertTo-CommandLineArgument {
    # Quotes one argument so CommandLineToArgvW (and the C runtime) reads it back unchanged.
    param([string] $Argument)
    if ($Argument.Length -gt 0 -and $Argument -notmatch '[\s"]') { return $Argument }
    $builder = New-Object System.Text.StringBuilder
    [void] $builder.Append('"')
    $backslashes = 0
    foreach ($character in $Argument.ToCharArray()) {
        if ($character -eq '\') { $backslashes++; continue }
        if ($character -eq '"') {
            [void] $builder.Append('\', 2 * $backslashes + 1)
        } elseif ($backslashes -gt 0) {
            [void] $builder.Append('\', $backslashes)
        }
        $backslashes = 0
        [void] $builder.Append($character)
    }
    if ($backslashes -gt 0) { [void] $builder.Append('\', 2 * $backslashes) }
    [void] $builder.Append('"')
    return $builder.ToString()
}

# The server runs in its own runspace so curl can run in this one. It returns one
# byte array per connection served.
$serveConnections = {
    param($Listener, $ResponseBytes, [int] $ConnectionCount, [int] $DelayMilliseconds, [bool] $ResetConnections, [int] $EarlyResponseBodyBytes)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)

    function Test-RequestComplete {
        param([byte[]] $Received, [int] $Length)
        $text = $latin1.GetString($Received, 0, $Length)
        $headerEnd = $text.IndexOf("`r`n`r`n")
        if ($headerEnd -lt 0) { return $false }
        $bodyStart = $headerEnd + 4
        if ($EarlyResponseBodyBytes -ge 0) { return ($Length - $bodyStart) -ge $EarlyResponseBodyBytes }
        $headers = $text.Substring(0, $headerEnd)
        $contentLength = [regex]::Match($headers, '(?im)^Content-Length:[ \t]*(\d+)[ \t]*$')
        if ($contentLength.Success) {
            return ($Length - $bodyStart) -ge [long] $contentLength.Groups[1].Value
        }
        if ($headers -match '(?im)^Transfer-Encoding:.*\bchunked\b') {
            $body = $text.Substring($bodyStart)
            return $body.StartsWith("0`r`n`r`n") -or $body.EndsWith("`r`n0`r`n`r`n")
        }
        return $true
    }

    $requests = New-Object System.Collections.Generic.List[object]
    for ($served = 0; $served -lt $ConnectionCount; $served++) {
        try {
            $client = $Listener.AcceptTcpClient()
        } catch {
            break  # The listener was stopped: curl exited without opening this connection.
        }
        if ($ResetConnections) {
            # A zero linger time makes Close send RST instead of FIN.
            $client.LingerState = New-Object System.Net.Sockets.LingerOption($true, 0)
            $client.Close()
            $requests.Add([byte[]] @())
            continue
        }
        try {
            $stream = $client.GetStream()
            $stream.ReadTimeout = if ($EarlyResponseBodyBytes -ge 0) { 5000 } else { 1000 }
            $buffer = New-Object byte[] 65536
            $received = New-Object System.IO.MemoryStream
            while (-not (Test-RequestComplete -Received $received.GetBuffer() -Length ([int] $received.Length))) {
                try {
                    $count = $stream.Read($buffer, 0, $buffer.Length)
                } catch [System.IO.IOException] {
                    break  # One second without a byte: take what arrived.
                }
                if ($count -le 0) { break }
                $received.Write($buffer, 0, $count)
            }
            if ($DelayMilliseconds -gt 0) { [System.Threading.Thread]::Sleep($DelayMilliseconds) }
            [byte[]] $response = $ResponseBytes[[Math]::Min($served, $ResponseBytes.Count - 1)]
            try {
                $stream.Write($response, 0, $response.Length)
                $stream.Flush()
            } catch [System.IO.IOException] {
                # curl already closed its end; the request is still worth recording.
            }
            if ($EarlyResponseBodyBytes -ge 0) {
                # Answered mid-body: record whatever curl goes on sending until it stops.
                $stream.ReadTimeout = 2000
                while ($true) {
                    try {
                        $count = $stream.Read($buffer, 0, $buffer.Length)
                    } catch [System.IO.IOException] {
                        break
                    }
                    if ($count -le 0) { break }
                    $received.Write($buffer, 0, $count)
                }
            }
            $requests.Add($received.ToArray())
        } finally {
            $client.Close()
        }
    }
    return , $requests.ToArray()
}

# The -Ftp server: one control connection, answered a line at a time, with a passive
# data listener for RETR, LIST, STOR and APPE. It returns the control bytes curl sent, as
# one array, writes the two-way transcript into $Transcript, and the bytes uploaded on
# STOR and APPE data connections into $UploadedData.
$serveFtpSession = {
    param($Listener, [hashtable] $Overrides, [byte[]] $DataBytes, [System.Text.StringBuilder] $Transcript, [System.IO.MemoryStream] $UploadedData)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $received = New-Object System.IO.MemoryStream
    $dataListener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $dataListener.Start()
    $dataPort = ([System.Net.IPEndPoint] $dataListener.LocalEndpoint).Port
    $restOffset = [long] 0

    function Send-Reply {
        param($Stream, [string] $Reply)
        $bytes = $latin1.GetBytes($Reply + "`r`n")
        $Stream.Write($bytes, 0, $bytes.Length)
        $Stream.Flush()
        foreach ($line in ($Reply -split "`r`n")) { [void] $Transcript.Append("< $line`r`n") }
    }

    try {
        try {
            $client = $Listener.AcceptTcpClient()
        } catch {
            return , @(, $received.ToArray())
        }
        try {
            $stream = $client.GetStream()
            $stream.ReadTimeout = 5000
            $greeting = if ($Overrides.ContainsKey('GREETING')) { $Overrides['GREETING'] } else { '220 Recorder ready' }
            Send-Reply -Stream $stream -Reply $greeting
            $line = New-Object System.IO.MemoryStream
            while ($true) {
                try {
                    $next = $stream.ReadByte()
                } catch [System.IO.IOException] {
                    break  # Five seconds without a byte: curl is done with us.
                }
                if ($next -lt 0) { break }
                $received.WriteByte([byte] $next)
                $line.WriteByte([byte] $next)
                if ($next -ne 10) { continue }
                $command = $latin1.GetString($line.ToArray()).TrimEnd("`r", "`n")
                $line.SetLength(0)
                [void] $Transcript.Append("> $command`r`n")
                $verb = ($command -split ' ', 2)[0].ToUpperInvariant()
                if ($Overrides.ContainsKey($verb)) {
                    if ($Overrides[$verb] -ceq 'CLOSE') { break }  # Hang up instead of replying.
                    Send-Reply -Stream $stream -Reply $Overrides[$verb]
                    if ($verb -eq 'QUIT') { break }
                    continue
                }
                switch ($verb) {
                    'USER' { Send-Reply -Stream $stream -Reply '331 Password required' }
                    'PASS' { Send-Reply -Stream $stream -Reply '230 Logged in' }
                    'PWD' { Send-Reply -Stream $stream -Reply '257 "/" is current directory' }
                    'CWD' { Send-Reply -Stream $stream -Reply '250 OK' }
                    'TYPE' { Send-Reply -Stream $stream -Reply '200 Type set' }
                    'SIZE' { Send-Reply -Stream $stream -Reply "213 $($DataBytes.Length)" }
                    'MDTM' { Send-Reply -Stream $stream -Reply '213 20260927123456' }
                    'REST' {
                        $restOffset = [long] ($command -split ' ', 2)[1]
                        Send-Reply -Stream $stream -Reply "350 Restarting at $restOffset"
                    }
                    'EPSV' { Send-Reply -Stream $stream -Reply "229 Entering Extended Passive Mode (|||$dataPort|)" }
                    'PASV' { Send-Reply -Stream $stream -Reply "227 Entering Passive Mode (127,0,0,1,$([Math]::Floor($dataPort / 256)),$($dataPort % 256))" }
                    { $_ -eq 'RETR' -or $_ -eq 'LIST' } {
                        Send-Reply -Stream $stream -Reply '150 Opening BINARY mode data connection'
                        $accept = $dataListener.AcceptTcpClientAsync()
                        if (-not $accept.Wait(5000)) { throw "curl sent $verb but opened no data connection within five seconds." }
                        $dataClient = $accept.Result
                        try {
                            $dataStream = $dataClient.GetStream()
                            $start = [int] [Math]::Max(0, [Math]::Min($restOffset, $DataBytes.Length))
                            $dataStream.Write($DataBytes, $start, $DataBytes.Length - $start)
                            $dataStream.Flush()
                        } catch [System.IO.IOException] {
                            # curl closed the data connection early, as it does once a range is read.
                        } finally {
                            $dataClient.Close()
                        }
                        $done = if ($Overrides.ContainsKey('RETRDONE')) { $Overrides['RETRDONE'] } else { '226 Transfer complete' }
                        Send-Reply -Stream $stream -Reply $done
                    }
                    { $_ -eq 'STOR' -or $_ -eq 'APPE' } {
                        Send-Reply -Stream $stream -Reply '150 Opening BINARY mode data connection'
                        $accept = $dataListener.AcceptTcpClientAsync()
                        if (-not $accept.Wait(5000)) { throw "curl sent $verb but opened no data connection within five seconds." }
                        $dataClient = $accept.Result
                        $uploaded = New-Object System.IO.MemoryStream
                        try {
                            $dataStream = $dataClient.GetStream()
                            $dataStream.ReadTimeout = 5000
                            $dataStream.CopyTo($uploaded)
                        } catch [System.IO.IOException] {
                            # Five seconds without a byte, or a reset: keep what arrived.
                        } finally {
                            $dataClient.Close()
                        }
                        [byte[]] $uploadedBytes = $uploaded.ToArray()
                        $UploadedData.Write($uploadedBytes, 0, $uploadedBytes.Length)
                        [void] $Transcript.Append("= $($uploadedBytes.Length) bytes received on the data connection: $($latin1.GetString($uploadedBytes))`r`n")
                        $done = if ($Overrides.ContainsKey('STORDONE')) { $Overrides['STORDONE'] } else { '226 Transfer complete' }
                        Send-Reply -Stream $stream -Reply $done
                    }
                    'QUIT' { Send-Reply -Stream $stream -Reply '221 Bye'; break }
                    default { Send-Reply -Stream $stream -Reply '502 Command not implemented' }
                }
                if ($verb -eq 'QUIT') { break }
            }
        } catch [System.IO.IOException] {
            # curl closed the control connection mid-reply; what arrived is still recorded.
        } finally {
            $client.Close()
        }
    } finally {
        $dataListener.Stop()
    }
    return , @(, $received.ToArray())
}

if ([string]::IsNullOrEmpty($Curl)) { $Curl = Get-ReferenceCurlPath }
$responseBytes = New-Object System.Collections.Generic.List[byte[]]
foreach ($text in $Response) { $responseBytes.Add((ConvertFrom-EscapedResponse -Text $text)) }
$ftpOverrides = @{}
foreach ($entry in $FtpReply) {
    $separator = $entry.IndexOf('=')
    if ($separator -lt 1) { throw "FtpReply '$entry' is not VERB=reply." }
    $ftpOverrides[$entry.Substring(0, $separator).ToUpperInvariant()] = [System.Text.Encoding]::GetEncoding(28591).GetString((ConvertFrom-EscapedResponse -Text $entry.Substring($separator + 1)))
}
$transcript = New-Object System.Text.StringBuilder
$uploadedData = New-Object System.IO.MemoryStream
$OutDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine((Get-Location).ProviderPath, $OutDirectory))
New-Item -ItemType Directory -Path $OutDirectory -Force | Out-Null

$listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, $Port)
$listener.Start()
$server = [System.Management.Automation.PowerShell]::Create()
try {
    if ($Ftp) {
        [void] $server.AddScript($serveFtpSession).AddArgument($listener).AddArgument($ftpOverrides).AddArgument([byte[]] (ConvertFrom-EscapedResponse -Text $FtpData)).AddArgument($transcript).AddArgument($uploadedData)
    } else {
        [void] $server.AddScript($serveConnections).AddArgument($listener).AddArgument($responseBytes).AddArgument($Connections).AddArgument($ResponseDelayMilliseconds).AddArgument([bool] $Reset).AddArgument($RespondAfterBodyBytes)
    }
    $serverRun = $server.BeginInvoke()

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $Curl
    $startInfo.Arguments = (@($CurlArgs | ForEach-Object { ConvertTo-CommandLineArgument -Argument $_ }) -join ' ')
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    # The standard input writer takes the console's input encoding, whose UTF-8 byte order
    # mark would reach curl ahead of StandardInput; Latin-1 has none. Windows PowerShell 5.1
    # has no ProcessStartInfo.StandardInputEncoding to set instead.
    $consoleInputEncoding = [System.Console]::InputEncoding
    [System.Console]::InputEncoding = [System.Text.Encoding]::GetEncoding(28591)
    try {
        $curlProcess = [System.Diagnostics.Process]::Start($startInfo)
    } finally {
        [System.Console]::InputEncoding = $consoleInputEncoding
    }
    try {
        $standardInputBytes = ConvertFrom-EscapedResponse -Text $StandardInput
        $curlProcess.StandardInput.BaseStream.Write($standardInputBytes, 0, $standardInputBytes.Length)
        $curlProcess.StandardInput.Close()
        $stdout = New-Object System.IO.MemoryStream
        $stderr = New-Object System.IO.MemoryStream
        # Both pipes drain at once, so curl never blocks on a full one.
        $stdoutCopy = $curlProcess.StandardOutput.BaseStream.CopyToAsync($stdout)
        $stderrCopy = $curlProcess.StandardError.BaseStream.CopyToAsync($stderr)
        $curlProcess.WaitForExit()
        [System.Threading.Tasks.Task]::WaitAll(@($stdoutCopy, $stderrCopy))
        $exitCode = $curlProcess.ExitCode
    } finally {
        $curlProcess.Dispose()
    }

    # A connection curl did not open would block the server forever; stopping the
    # listener ends the wait. Give an accepted connection a moment to finish first.
    [void] $serverRun.AsyncWaitHandle.WaitOne(2000)
    $listener.Stop()
    $requests = $server.EndInvoke($serverRun)
    if ($server.Streams.Error.Count -gt 0) { throw $server.Streams.Error[0] }
} finally {
    $listener.Stop()
    $server.Dispose()
}

$requestBytes = New-Object System.IO.MemoryStream
foreach ($request in $requests) {
    foreach ($bytes in $request) { $requestBytes.Write($bytes, 0, $bytes.Length) }
}

[System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'request.bin'), $requestBytes.ToArray())
[System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'stdout.bin'), $stdout.ToArray())
[System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'stderr.txt'), $stderr.ToArray())
[System.IO.File]::WriteAllText((Join-Path $OutDirectory 'exitcode.txt'), [string] $exitCode, [System.Text.Encoding]::ASCII)
if ($Ftp) {
    [System.IO.File]::WriteAllText((Join-Path $OutDirectory 'transcript.txt'), $transcript.ToString(), [System.Text.Encoding]::GetEncoding(28591))
    [System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'upload.bin'), $uploadedData.ToArray())
}

Write-Host "curl exited $exitCode; fixtures written to $OutDirectory"
