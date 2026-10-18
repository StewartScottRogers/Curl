namespace Curl.Api.Callbacks;

/// <summary>What an <see cref="IoctlCallback"/> is asked to do: libcurl's <c>curliocmd</c>, with upstream curl 8.22.0's numbers.</summary>
public enum IoctlCommand
{
    /// <summary><c>CURLIOCMD_NOP</c> (0): nothing.</summary>
    Nop = 0,

    /// <summary><c>CURLIOCMD_RESTARTREAD</c> (1): restart the read stream from the start.</summary>
    RestartRead = 1,
}
