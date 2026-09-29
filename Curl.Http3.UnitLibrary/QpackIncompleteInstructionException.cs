namespace Curl.Http3;

/// <summary>
/// Thrown inside QPACK's readers when the input ends inside an instruction or a field
/// line. On the encoder and decoder streams that only means the rest has not arrived yet;
/// in a field section, which arrives whole, it is <see cref="QpackErrorCode.DecompressionFailed" />.
/// </summary>
internal sealed class QpackIncompleteInstructionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QpackIncompleteInstructionException" /> class.
    /// </summary>
    public QpackIncompleteInstructionException()
        : base("The QPACK input ends inside an instruction.")
    {
    }
}
