using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml.Controls;
using WebcamViewer.Video;
using WebcamViewer.ViewModels;
using System; //Required: the IAsyncOperation GetAwaiter extension (awaiting the FolderPicker) lives here
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

// ReSharper disable once CheckNamespace
namespace WebcamViewer.Views;

public sealed partial class MainPage : Page
{
    //I tend to like to declare/define private methods above the constructor, in C# classes
    private MainViewModel ViewModel => DataContext as MainViewModel;

    public MainPage()
    {
        //Doing this before InitializeComponent() - in case InitializeComponent()
        //  is the thing that sets the data context.
        DataContextChanged += (_, _) =>
        {
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            if (DataContext is IFolderPickBridge folderPick)
            {
                folderPick.PickFolderPathAsync = PickFolderPathAsync;
            }

            if (DataContext is ICanvasInvalidator invalidator)
            {
                //Frames arrive on a capture thread - marshal the repaint onto the UI thread
                invalidator.InvalidateCanvas = () => DispatcherQueue?.TryEnqueue(() => VideoView?.Invalidate());
            }
        };

        InitializeComponent();

        VideoView.PaintSurface += (_, e) => VideoCanvasHelper.RenderFrame(e.Surface, e.Info, ViewModel);
        VideoView.SizeChanged += (_, _) => VideoView.Invalidate();
    }

    private static async Task<string> PickFolderPathAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        picker.FileTypeFilter.Add("*");

        StorageFolder folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
