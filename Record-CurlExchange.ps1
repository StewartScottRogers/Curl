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
      3. sends the canned response and closes the connection.

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
    [string] $Response = 'HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n',
    [Parameter(Mandatory = $true)] [string[]] $CurlArgs,
    [Parameter(Mandatory = $true)] [string] $OutDirectory,
    [ValidateRange(1, 1000)] [int] $Connections = 1,
    [ValidateRange(0, 600000)] [int] $ResponseDelayMilliseconds = 0,
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
    param($Listener, [byte[]] $ResponseBytes, [int] $ConnectionCount, [int] $DelayMilliseconds)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)

    function Test-RequestComplete {
        param([byte[]] $Received, [int] $Length)
        $text = $latin1.GetString($Received, 0, $Length)
        $headerEnd = $text.IndexOf("`r`n`r`n")
        if ($headerEnd -lt 0) { return $false }
        $bodyStart = $headerEnd + 4
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
        try {
            $stream = $client.GetStream()
            $stream.ReadTimeout = 1000
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
            $requests.Add($received.ToArray())
            if ($DelayMilliseconds -gt 0) { [System.Threading.Thread]::Sleep($DelayMilliseconds) }
            try {
                $stream.Write($ResponseBytes, 0, $ResponseBytes.Length)
                $stream.Flush()
            } catch [System.IO.IOException] {
                # curl already closed its end; the request is still worth recording.
            }
        } finally {
            $client.Close()
        }
    }
    return , $requests.ToArray()
}

if ([string]::IsNullOrEmpty($Curl)) { $Curl = Get-ReferenceCurlPath }
$responseBytes = ConvertFrom-EscapedResponse -Text $Response
$OutDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine((Get-Location).ProviderPath, $OutDirectory))
New-Item -ItemType Directory -Path $OutDirectory -Force | Out-Null

$listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, $Port)
$listener.Start()
$server = [System.Management.Automation.PowerShell]::Create()
try {
    [void] $server.AddScript($serveConnections).AddArgument($listener).AddArgument($responseBytes).AddArgument($Connections).AddArgument($ResponseDelayMilliseconds)
    $serverRun = $server.BeginInvoke()

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $Curl
    $startInfo.Arguments = (@($CurlArgs | ForEach-Object { ConvertTo-CommandLineArgument -Argument $_ }) -join ' ')
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $curlProcess = [System.Diagnostics.Process]::Start($startInfo)
    try {
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

Write-Host "curl exited $exitCode; fixtures written to $OutDirectory"
