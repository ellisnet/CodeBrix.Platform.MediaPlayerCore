using CodeBrix.Platform.Simple;
using WebcamViewer.Helpers;
using System.Windows;

namespace WebcamViewer;

public partial class App : Application
{
    public App()
    {
        SimpleServiceResolver.CreateInstance(HostHelper.GetHost(), services =>
        {
            //No custom services needed - the drawing session lives in the view model
        });
        SimpleViewModel.SetIsDesignMode(false);
    }
}
