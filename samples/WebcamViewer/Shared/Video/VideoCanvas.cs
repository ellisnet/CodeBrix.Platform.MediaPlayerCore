using System;
using System.Runtime.InteropServices;
using SkiaSharp;
using WebcamViewer.ViewModels;

namespace WebcamViewer.Video;

/// <summary>
/// SkiaSharp-based video surface, abstracted so a single control name -
/// <c>&lt;video:VideoCanvas /&gt;</c> - can be used in the XAML of every head. This one
/// linked source file is compiled into each head's assembly and resolves to the correct
/// base control for that head via conditional compilation:
/// <list type="bullet">
///   <item>CodeBrix.Platform Skia heads (which should have HAS_CODEBRIXPLATFORM defined on
///   their shared assembly); and native WinUI 3 (which should have HAS_WINUI defined):
///   SkiaSharp.Views.Windows.SKXamlCanvas.</item>
///   <item>native WPF (neither symbol): SkiaSharp.Views.WPF.SKElement.</item>
/// </list>
/// It is a plain subclass that carries no extra behavior - the hosting page's code-behind
/// wires PaintSurface to <see cref="VideoCanvasHelper.RenderFrame"/>.
/// </summary>
#if (HAS_CODEBRIXPLATFORM || HAS_WINUI)
public class VideoCanvas : SkiaSharp.Views.Windows.SKXamlCanvas { }
#else
public class VideoCanvas : SkiaSharp.Views.WPF.SKElement { }
#endif

/// <summary>
/// Renders the view model's most recent webcam frame onto a Skia surface, aspect-fit and
/// centered on a black background. Called from the canvas PaintSurface handler (always on
/// the UI thread, so the cached buffers need no locking of their own).
/// </summary>
public static class VideoCanvasHelper
{
    private static byte[] _frameBuffer;
    private static SKBitmap _bitmap;

    public static void RenderFrame(SKSurface surface, SKImageInfo info, MainViewModel viewModel)
    {
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        if (viewModel == null
            || !viewModel.TryGetLatestFrame(ref _frameBuffer, out int width, out int height)
            || width <= 0 || height <= 0)
        {
            return;
        }

        if (_bitmap == null || _bitmap.Width != width || _bitmap.Height != height)
        {
            _bitmap?.Dispose();
            _bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        }
        Marshal.Copy(_frameBuffer, 0, _bitmap.GetPixels(), width * height * 4);

        float scale = Math.Min((float)info.Width / width, (float)info.Height / height);
        float destWidth = width * scale;
        float destHeight = height * scale;
        float destX = (info.Width - destWidth) / 2f;
        float destY = (info.Height - destHeight) / 2f;
        canvas.DrawBitmap(_bitmap, new SKRect(destX, destY, destX + destWidth, destY + destHeight),
            new SKSamplingOptions(SKFilterMode.Linear));
    }
}
