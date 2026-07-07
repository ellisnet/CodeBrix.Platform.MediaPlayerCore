using System;

namespace CodeBrix.Webcam;

/// <summary>
/// The exception thrown for webcam-specific failures: a device that cannot be opened,
/// a capture pipeline that cannot be built, an overlay that does not match the video
/// dimensions, and similar conditions.
/// </summary>
public class WebcamException : Exception
{
    /// <summary>Creates the exception with a message describing the failure.</summary>
    /// <param name="message">A description of the failure.</param>
    public WebcamException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the underlying cause.</summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="innerException">The underlying exception.</param>
    public WebcamException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
