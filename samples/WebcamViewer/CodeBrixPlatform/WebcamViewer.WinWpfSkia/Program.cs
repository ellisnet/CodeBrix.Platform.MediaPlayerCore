using CodeBrix.Platform.UI.Hosting;
using CodeBrix.Platform.UI.Runtime.Skia.Wpf;
using System;

// ReSharper disable CheckNamespace
namespace WebcamViewer;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.InitializeLogging();

        var host = CodeBrixPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWindowsWpf()
            .Build();

        //The WPF host's default OpenGL renderer draws via raw opengl32 onto WPF's own
        //  DirectX-composited HWND, which causes "airspace" conflicts on many systems.
        //  Software rendering blits the Skia frame into WPF and composites correctly.
        if (host is WpfHost wpfHost)
        {
            wpfHost.RenderSurfaceType = RenderSurfaceType.Software;
        }

        host.Run();
    }
}
