#nullable enable annotations
// Ported from LibVLCSharp 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;

namespace CodeBrix.Platform.MediaPlayerCore.MediaPlayerElement; //was previously: LibVLCSharp.Shared.MediaPlayerElement;

/// <summary>
/// Extensions methods for <see cref="TimeSpan"/>
/// </summary>
internal static class TimeSpanExtensions
{
    /// <summary>
    /// Converts the value of the current <see cref="TimeSpan"/> object to its equivalent short string representation
    /// </summary>
    /// <param name="span">time interval</param>
    /// <returns>the short string representation of the current <see cref="TimeSpan"/> value</returns>
    internal static string ToShortString(this TimeSpan span)
    {
        if (span.Days != 0)
        {
            return span.ToString(@"d\.hh\:mm\:ss");
        }
        if (span.Hours != 0)
        {
            return span.ToString(@"hh\:mm\:ss");
        }
        return span.ToString(@"mm\:ss");
    }
}
