namespace Curl.Api.Callbacks;

/// <summary>What an <see cref="IoctlCallback"/> returns: libcurl's <c>curlioerr</c>, with upstream curl 8.22.0's numbers.</summary>
public enum IoctlResult
{
    /// <summary><c>CURLIOE_OK</c> (0): the command was done.</summary>
    Ok = 0,

    /// <summary><c>CURLIOE_UNKNOWNCMD</c> (1): the command is not known.</summary>
    UnknownCommand = 1,

    /// <summary><c>CURLIOE_FAILRESTART</c> (2): the read stream could not be restarted.</summary>
    FailRestart = 2,
}
