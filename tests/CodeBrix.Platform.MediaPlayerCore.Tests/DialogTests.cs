#nullable enable annotations
// Ported from LibVLCSharp.Tests 3.9.7 by Jeremy Ellis on 2026-04-18.

using System;
using System.Threading.Tasks;
using CodeBrix.Platform.MediaPlayerCore;
using Xunit;
using SilverAssertions;

namespace CodeBrix.Platform.MediaPlayerCore.Tests; //was previously: LibVLCSharp.Tests;

public class DialogTests : BaseSetup
{
    const string UrlRequireAuth = "http://httpbin.org/basic-auth/user/passwd";
    const string Username = "username";
    const string Password = "password";

    // Windows-only: libvlc on Linux handles HTTP basic-auth internally via the
    // access module and does NOT surface it through the managed dialog-handler
    // callback, so `await tcs.Task` hangs forever waiting for a callback that
    // never fires. The test works on Windows where the upstream LibVLCSharp CI
    // runs it.
    [Fact(Skip = "Windows-only: libvlc's HTTP basic-auth dialog is not raised to the managed dialog-handler callback on Linux",
          SkipUnless = nameof(BaseSetup.IsWindows), SkipType = typeof(BaseSetup))]
    public async Task PostLogin()
    {
        var tcs = new TaskCompletionSource<bool>();

        _libVLC.SetDialogHandlers((title, text) => Task.CompletedTask,
            (dialog, title, text, username, store, token) =>
            {
                // show UI dialog
                // On "OK" call PostLogin
                dialog.PostLogin(Username, Password, false);
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            },
            (dialog, title, text, type, cancelText, actionText, secondActionText, token) => Task.CompletedTask,
            (dialog, title, text, indeterminate, position, cancelText, token) => Task.CompletedTask,
            (dialog, position, text) => Task.CompletedTask);

        var mp = new MediaPlayer(_libVLC)
        {
            Media = new Media(_libVLC, UrlRequireAuth, FromType.FromLocation)
        };

        mp.Play();

        (await tcs.Task).Should().BeTrue();
    }

    [Fact(Skip = "Windows-only: libvlc's HTTP basic-auth dialog is not raised to the managed dialog-handler callback on Linux",
          SkipUnless = nameof(BaseSetup.IsWindows), SkipType = typeof(BaseSetup))]
    public async Task ShouldThrowIfPostLoginsTwice()
    {
        var tcs = new TaskCompletionSource<bool>();

        _libVLC.SetDialogHandlers((title, text) => Task.CompletedTask,
            (dialog, title, text, username, store, token) =>
            {
                dialog.PostLogin(Username, Password, false);
                ((Action)(() => dialog.PostLogin(Username, Password, false))).Should().Throw<VLCException>();
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            },
            (dialog, title, text, type, cancelText, actionText, secondActionText, token) => Task.CompletedTask,
            (dialog, title, text, indeterminate, position, cancelText, token) => Task.CompletedTask,
            (dialog, position, text) => Task.CompletedTask);

        var mp = new MediaPlayer(_libVLC)
        {
            Media = new Media(_libVLC, UrlRequireAuth, FromType.FromLocation)
        };

        mp.Play();

        (await tcs.Task).Should().BeTrue();
    }

    [Fact(Skip = "Windows-only: libvlc's HTTP basic-auth dialog is not raised to the managed dialog-handler callback on Linux",
          SkipUnless = nameof(BaseSetup.IsWindows), SkipType = typeof(BaseSetup))]
    public async Task ShouldNotThrowAndReturnFalseIfDimissingTwice()
    {
        var tcs = new TaskCompletionSource<bool>();

        _libVLC.SetDialogHandlers((title, text) => Task.CompletedTask,
            (dialog, title, text, username, store, token) =>
            {
                var result = dialog.Dismiss();
                (result).Should().BeTrue();
                result = dialog.Dismiss();
                (result).Should().BeFalse();
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            },
            (dialog, title, text, type, cancelText, actionText, secondActionText, token) => Task.CompletedTask,
            (dialog, title, text, indeterminate, position, cancelText, token) => Task.CompletedTask,
            (dialog, position, text) => Task.CompletedTask);

        var mp = new MediaPlayer(_libVLC)
        {
            Media = new Media(_libVLC, UrlRequireAuth, FromType.FromLocation)
        };

        mp.Play();

        (await tcs.Task).Should().BeTrue();
    }

    [Fact]
    public void ShouldUnsetDialogHandlersWhenInstanceDisposed()
    {
        _libVLC.SetDialogHandlers((title, text) => Task.CompletedTask,
            (dialog, title, text, username, store, token) => Task.CompletedTask,
            (dialog, title, text, type, cancelText, actionText, secondActionText, token) => Task.CompletedTask,
            (dialog, title, text, indeterminate, position, cancelText, token) => Task.CompletedTask,
            (dialog, position, text) => Task.CompletedTask);

        (_libVLC.DialogHandlersSet).Should().BeTrue();

        _libVLC.Dispose();

        (_libVLC.DialogHandlersSet).Should().BeFalse();
    }
}
