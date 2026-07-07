using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.Webcam.Internal.Windows;

/// <summary>
/// Runs work on a multithreaded-apartment (MTA) thread. Media Foundation and WASAPI
/// objects are not apartment-agile: an interface created on (or called from) an STA
/// thread — every desktop UI thread — fails cross-apartment with E_NOINTERFACE. All
/// COM-touching control operations route through here so the objects live, and are
/// always called, in the MTA; capture/encode worker threads are MTA already and call
/// straight through.
/// </summary>
internal static class MtaThread
{
    /// <summary>Runs the action inline when already on an MTA thread, else on the thread pool.</summary>
    internal static void Run(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA)
        {
            action();
            return;
        }
        Task.Run(action).GetAwaiter().GetResult();
    }

    /// <summary>Runs the function inline when already on an MTA thread, else on the thread pool.</summary>
    internal static T Run<T>(Func<T> function)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA)
        {
            return function();
        }
        return Task.Run(function).GetAwaiter().GetResult();
    }
}
