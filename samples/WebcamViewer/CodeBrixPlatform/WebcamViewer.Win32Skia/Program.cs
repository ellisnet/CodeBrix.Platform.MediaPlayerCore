using CodeBrix.Platform.UI.Hosting;
using System;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace
namespace WebcamViewer;

internal class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        App.InitializeLogging();

        var host = CodeBrixPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWindowsWin32()
            .Build();

        await host.RunAsync();
    }
}
