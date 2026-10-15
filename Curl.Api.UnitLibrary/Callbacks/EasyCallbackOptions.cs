namespace Curl.Api.Callbacks;

/// <summary>
/// The callbacks of one easy handle: the <c>curl_easy_setopt</c> <c>CURLOPT_*FUNCTION</c> options
/// (ADR-0506), each <see langword="null"/> by default as libcurl's are unset. Each option's
/// <c>*DATA</c> user pointer is the delegate's closure.
/// </summary>
public sealed record EasyCallbackOptions
{
    /// <summary><c>CURLOPT_WRITEFUNCTION</c>: takes received body data. Default <see langword="null"/> (libcurl writes to stdout).</summary>
    public WriteCallback? Write { get; init; }

    /// <summary><c>CURLOPT_READFUNCTION</c>: supplies upload data. Default <see langword="null"/> (libcurl reads stdin).</summary>
    public ReadCallback? Read { get; init; }

    /// <summary><c>CURLOPT_HEADERFUNCTION</c>: takes received header lines. Default <see langword="null"/>.</summary>
    public HeaderCallback? Header { get; init; }

    /// <summary><c>CURLOPT_PROGRESSFUNCTION</c>: reports progress as floating-point counts. Default <see langword="null"/>.</summary>
    public ProgressCallback? Progress { get; init; }

    /// <summary><c>CURLOPT_XFERINFOFUNCTION</c>: reports progress as <c>curl_off_t</c> counts. Default <see langword="null"/>.</summary>
    public TransferInfoCallback? TransferInfo { get; init; }

    /// <summary><c>CURLOPT_DEBUGFUNCTION</c>: takes the verbose trace. Default <see langword="null"/> (libcurl writes it to stderr).</summary>
    public DebugCallback? Debug { get; init; }

    /// <summary><c>CURLOPT_SEEKFUNCTION</c>: rewinds the upload source. Default <see langword="null"/>.</summary>
    public SeekCallback? Seek { get; init; }

    /// <summary><c>CURLOPT_SOCKOPTFUNCTION</c>: sets options on a new socket. Default <see langword="null"/>.</summary>
    public SocketOptionCallback? SocketOption { get; init; }

    /// <summary><c>CURLOPT_OPENSOCKETFUNCTION</c>: creates sockets. Default <see langword="null"/> (libcurl calls <c>socket()</c>).</summary>
    public OpenSocketCallback? OpenSocket { get; init; }

    /// <summary><c>CURLOPT_CLOSESOCKETFUNCTION</c>: closes sockets. Default <see langword="null"/> (libcurl closes them).</summary>
    public CloseSocketCallback? CloseSocket { get; init; }

    /// <summary><c>CURLOPT_PREREQFUNCTION</c>: called before each request is sent. Default <see langword="null"/>.</summary>
    public PrerequestCallback? Prerequest { get; init; }

    /// <summary><c>CURLOPT_RESOLVER_START_FUNCTION</c>: called before each name resolution. Default <see langword="null"/>.</summary>
    public ResolverStartCallback? ResolverStart { get; init; }

    /// <summary><c>CURLOPT_TRAILERFUNCTION</c>: supplies chunked-upload trailers. Default <see langword="null"/>.</summary>
    public TrailerCallback? Trailer { get; init; }

    /// <summary><c>CURLOPT_CHUNK_BGN_FUNCTION</c>: called before each wildcard file. Default <see langword="null"/>.</summary>
    public ChunkBeginCallback? ChunkBegin { get; init; }

    /// <summary><c>CURLOPT_CHUNK_END_FUNCTION</c>: called after each wildcard file. Default <see langword="null"/>.</summary>
    public ChunkEndCallback? ChunkEnd { get; init; }

    /// <summary><c>CURLOPT_FNMATCH_FUNCTION</c>: matches wildcard patterns. Default <see langword="null"/> (libcurl's own matcher).</summary>
    public FileNameMatchCallback? FileNameMatch { get; init; }

    /// <summary><c>CURLOPT_INTERLEAVEFUNCTION</c>: takes interleaved RTP data. Default <see langword="null"/> (written as body data).</summary>
    public InterleaveCallback? Interleave { get; init; }

    /// <summary><c>CURLOPT_HSTSREADFUNCTION</c>: preloads the HSTS cache. Default <see langword="null"/>.</summary>
    public HstsReadCallback? HstsRead { get; init; }

    /// <summary><c>CURLOPT_HSTSWRITEFUNCTION</c>: takes the HSTS cache when it is saved. Default <see langword="null"/>.</summary>
    public HstsWriteCallback? HstsWrite { get; init; }

    /// <summary><c>CURLOPT_SSH_KEYFUNCTION</c>: decides on SSH host keys after the known-hosts check. Default <see langword="null"/>.</summary>
    public SshKeyCallback? SshKey { get; init; }

    /// <summary><c>CURLOPT_SSH_HOSTKEYFUNCTION</c>: checks SSH host keys in place of the known-hosts file. Default <see langword="null"/>.</summary>
    public SshHostKeyCallback? SshHostKey { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSL_CTX_FUNCTION</c>: adjusts the TLS context. Default <see langword="null"/>;
    /// setting it makes the engine answer <c>CURLE_NOT_BUILT_IN</c>, as curl's Schannel build does.
    /// </summary>
    public SslContextCallback? SslContext { get; init; }
}
