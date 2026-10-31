namespace Curl.Extension.CompressionDictionary;

/// <summary>The outcome of checking the header at the start of a <c>dcb</c> or <c>dcz</c> stream.</summary>
public enum DictionaryStreamHeaderCheck
{
    /// <summary>The magic is right and the hash is the advertised dictionary's.</summary>
    Matches,

    /// <summary>Fewer bytes than the header holds have arrived.</summary>
    Incomplete,

    /// <summary>The stream does not start with the coding's magic number.</summary>
    WrongMagic,

    /// <summary>The magic is right but the hash names another dictionary.</summary>
    WrongDictionary,
}
