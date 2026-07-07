using WebcamViewer.Video;
using WebcamViewer.ViewModels;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace WebcamViewer.Views;

public partial class MainWindow : Window
{
    //I tend to like to declare/define private methods above the constructor, in C# classes
    private MainViewModel ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        //Doing this before InitializeComponent() - in case InitializeComponent()
        //  is the thing that sets the data context.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is IFolderPickBridge folderPick)
            {
                folderPick.PickFolderPathAsync = PickFolderPathAsync;
            }

            if (DataContext is ICanvasInvalidator invalidator)
            {
                //Frames arrive on a capture thread - marshal the repaint onto the UI thread
                invalidator.InvalidateCanvas = InvalidateVideoCanvas;
            }
        };

        InitializeComponent();

        VideoView.PaintSurface += (_, e) => VideoCanvasHelper.RenderFrame(e.Surface, e.Info, ViewModel);
    }

    private void InvalidateVideoCanvas()
    {
        if (VideoView.Dispatcher.CheckAccess())
        {
            VideoView.InvalidateVisual();
        }
        else
        {
            VideoView.Dispatcher.BeginInvoke(VideoView.InvalidateVisual);
        }
    }

    private Task<string> PickFolderPathAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose the folder for frame-photos",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };

        var chosen = dialog.ShowDialog(this) == true ? dialog.FolderName : null;
        return Task.FromResult(chosen);
    }
}
