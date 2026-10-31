namespace Curl.Extension.WebDavTools.Multistatus;

/// <summary>Why a <c>207 Multistatus</c> body was refused.</summary>
public enum WebDavMultistatusFailureKind
{
    /// <summary>The body is not well-formed XML.</summary>
    NotWellFormed,

    /// <summary>The body holds a document type declaration, which is never processed so no external entity is resolved.</summary>
    DocumentTypeDeclaration,

    /// <summary>The root element is not <c>multistatus</c> in the <c>DAV:</c> namespace.</summary>
    NotMultistatus,

    /// <summary>An element RFC 4918 requires is missing: an <c>href</c>, a <c>propstat</c>'s <c>prop</c> or <c>status</c>, or both a response's <c>status</c> and <c>propstat</c>.</summary>
    MissingElement,

    /// <summary>A <c>status</c> element is not an HTTP status line.</summary>
    InvalidStatus,
}
