#nullable enable annotations
// Ported from LibVLCSharp 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.Runtime.InteropServices;

namespace CodeBrix.Platform.MediaPlayerCore.Structures; //was previously: LibVLCSharp.Shared.Structures;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct AudioOutputDescriptionStructure
{
    internal readonly IntPtr Name;
    internal readonly IntPtr Description;
    internal readonly IntPtr Next;
}

/// <summary>
/// Description for audio output.
/// </summary>
public readonly struct AudioOutputDescription
{
    /// <summary>
    /// AudioOutputDescription default constructor
    /// </summary>
    /// <param name="name">Audio output name</param>
    /// <param name="description">Audio output description</param>
    internal AudioOutputDescription(string name, string description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>
    /// Audio output name
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// Audio output description
    /// </summary>
    public readonly string Description;
}
