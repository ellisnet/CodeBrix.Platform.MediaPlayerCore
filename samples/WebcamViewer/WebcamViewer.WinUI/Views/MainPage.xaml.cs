using Microsoft.UI.Xaml.Controls;
using WebcamViewer.Video;
using WebcamViewer.ViewModels;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace WebcamViewer.WinUI.Views;

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

        //Native WinUI 3 pickers must be seeded with the owning window's handle
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        StorageFolder folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
