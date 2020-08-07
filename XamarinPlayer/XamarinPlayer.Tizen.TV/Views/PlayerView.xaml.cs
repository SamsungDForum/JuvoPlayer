/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinPlayer.Tizen.TV.Services;
using XamarinPlayer.Tizen.TV.ViewModels;
using XamarinPlayer.Tizen.TV.Views;

namespace XamarinPlayer.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PlayerView : ContentPage, IContentPayloadHandler, ISuspendable
    {
        public static readonly BindableProperty PlayerStateProperty =
            BindableProperty.Create(
                propertyName: "PlayerState",
                returnType: typeof(object),
                typeof(PlayerView),
                defaultValue: false,
                defaultBindingMode: BindingMode.OneWay,
                propertyChanged: (b, o, n) =>
                {
                    var playerView = ((PlayerView) b);
                    var state = (PlayerState) n;
                    switch (state)
                    {
                        case JuvoPlayer.Common.PlayerState.Prepared:
                        {
                            playerView.Show();
                            break;
                        }
                        case JuvoPlayer.Common.PlayerState.Playing:
                        {
                            playerView.PlayImage.Source = "btn_viewer_control_pause_normal.png";
                            break;
                        }
                        case JuvoPlayer.Common.PlayerState.Paused:
                            playerView.PlayImage.Source = "btn_viewer_control_play_normal.png";
                            break;
                    }
                });

        public static readonly BindableProperty SeekPreviewProperty =
            BindableProperty.Create(
                propertyName: "SeekPreview",
                returnType: typeof(object),
                typeof(PlayerView),
                defaultValue: false,
                defaultBindingMode: BindingMode.OneWay,
                propertyChanged: (b, o, n) =>
                {
                    if (n != null)
                        ((PlayerView) b).SeekPreviewCanvas.InvalidateSurface();
                });

        public static readonly BindableProperty PreviewFrameSizeProperty =
            BindableProperty.Create(
                propertyName: "PreviewFrameSize",
                returnType: typeof(object),
                typeof(SKSize),
                defaultValue: false,
                defaultBindingMode: BindingMode.OneWay,
                propertyChanged: (b, o, n) =>
                {
                    if (!(n is null))
                        ((PlayerView) b).SetSeekPreviewFrameSize((SKSize) n);
                });

        public object PreviewFrameSize
        {
            set { SetValue(PreviewFrameSizeProperty, value); }
            get { return GetValue(PreviewFrameSizeProperty); }
        }

        public object PlayerState
        {
            set { SetValue(PlayerStateProperty, value); }
            get { return GetValue(PlayerStateProperty); }
        }

        public object SeekPreview
        {
            set { SetValue(SeekPreviewProperty, value); }
            get { return GetValue(SeekPreviewProperty); }
        }

        private Subject<string> _keys;
        private IDisposable _keySubscription;

        private readonly int DefaultTimeout = 5000;

        public PlayerView()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            SetBinding(SeekPreviewProperty, new Binding(nameof(PlayerViewModel.PreviewFrame)));
            SetBinding(PlayerStateProperty, new Binding(nameof(PlayerViewModel.PlayerState)));
            SetBinding(PreviewFrameSizeProperty, new Binding(nameof(PlayerViewModel.PreviewFrameSize)));

            PlayButton.Clicked += (s, e) => { (BindingContext as PlayerViewModel)?.PlayOrPauseCommand.Execute(null); };

            Progressbar.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == "Progress")
                    UpdateSeekPreviewFramePosition();
            };
        }

        private void SetSeekPreviewFrameSize(SKSize size)
        {
            SeekPreviewFrame.IsVisible = true;
            SeekPreviewCanvas.WidthRequest = size.Width;
            SeekPreviewCanvas.HeightRequest = size.Height;
        }

        private void OnSeekPreviewCanvasOnPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            var frame = SeekPreview as SubSkBitmap;
            if (frame == null) return;

            var surface = args.Surface;
            var canvas = surface.Canvas;
            var targetRect = args.Info.Rect;

            canvas.DrawBitmap(frame.Bitmap, frame.SkRect, targetRect);
            canvas.Flush();
        }

        private void UpdateSeekPreviewFramePosition()
        {
            var progress = Progressbar.Progress;

            var offset = progress * SeekPreviewContainer.Width - SeekPreviewFrame.Width / 2;
            if (offset < 0)
                offset = 0;
            else if (offset + SeekPreviewFrame.Width > SeekPreviewContainer.Width)
                offset = SeekPreviewContainer.Width - SeekPreviewFrame.Width;

            AbsoluteLayout.SetLayoutBounds(SeekPreviewFrame,
                new Rectangle(offset, SeekPreviewContainer.Height - SeekPreviewFrame.Height, SeekPreviewFrame.Width,
                    SeekPreviewFrame.Height));
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            MessagingCenter.Subscribe<IKeyEventSender, string>(this, "KeyDown", (s, e) => { KeyEventHandler(e); });
            MessagingCenter.Subscribe<IEventSender, string>(this, "Pop",
                async (s, e) => { await Navigation.PopAsync(); });

            SettingsButton.IsEnabled = true;
            PlayButton.IsEnabled = true;
            PlayButton.Focus();
            SetupDebounce();
        }

        private void KeyEventHandler(string e)
        {
            // Prevents key handling & focus change in Show().
            // Consider adding a call Focus(Focusable Object) where focus would be set in one place
            // and error status could be handled.
            _keys.OnNext(e);
            
            if (e.Contains("Stop"))
            {
                Navigation.RemovePage(this);
                return;
            }

            if (e.Contains("Back") && !e.Contains("XF86PlayBack"))
            {
                //If the 'return' button on standard or back arrow on the smart remote control was pressed do react depending on the playback state
                if (!BottomBar.IsVisible)
                {
                    Hide();
                    Navigation.RemovePage(this);
                }
                else
                {
                    if (Settings.IsVisible)
                    {
                        Settings.IsVisible = false;
                        PlayButton.IsEnabled = true;
                        PlayButton.Focus();
                        SeekPreviewContainer.Opacity = 1;
                    }
                    else
                        Hide();
                }
            }
            else
            {
                if (Settings.IsVisible)
                {
                    return;
                }

                if (BottomBar.IsVisible)
                {
                    if (e.Contains("XF86PlayBack"))
                    {
                        (BindingContext as PlayerViewModel).PlayOrPauseCommand.Execute(null);
                    }
                    else if (e.Contains("Pause"))
                    {
                        (BindingContext as PlayerViewModel).PauseCommand.Execute(null);
                    }
                    else if (e.Contains("Play"))
                    {
                        (BindingContext as PlayerViewModel).StartCommand.Execute(null);
                    }

                    if ((e.Contains("Next") || e.Contains("Right")))
                    {
                        (BindingContext as PlayerViewModel).ForwardCommand.Execute(null);
                    }
                    else if ((e.Contains("Rewind") || e.Contains("Left")))
                    {
                        (BindingContext as PlayerViewModel).RewindCommand.Execute(null);
                    }
                    else if ((e.Contains("Up")))
                    {
                        Settings.IsVisible = true;
                        PlayButton.IsEnabled = false;
                        AudioTrack.Focus();
                        SeekPreviewContainer.Opacity = 0;
                    }

                    //expand the time that playback control bar is on the screen
                }
                else
                {
                    //Make the playback control bar visible on the screen
                    Show();
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            MessagingCenter.Unsubscribe<IKeyEventSender, string>(this, "KeyDown");
            MessagingCenter.Unsubscribe<IEventSender, string>(this, "Pop");
            _keySubscription?.Dispose();
            (BindingContext as PlayerViewModel)?.DisposeCommand.Execute(null);
        }

        void OnTapGestureRecognizerControllerTapped(object sender, EventArgs args)
        {
            Hide();
        }

        void OnTapGestureRecognizerViewTapped(object sender, EventArgs args)
        {
            Show();
        }

        private void Show()
        {
            // Do not show anything if error handling in progress.
            PlayButton.Focus();
            TopBar.IsVisible = true;
            BottomBar.IsVisible = true;
            SeekPreviewContainer.Opacity = 1;
        }

        private void Hide()
        {
            TopBar.IsVisible = false;
            BottomBar.IsVisible = false;
            SeekPreviewContainer.Opacity = 0;
        }

        protected override bool OnBackButtonPressed()
        {
            return true;
        }

        public Task<bool> HandleUrl(string url)
        {
            var currentClipUrl = (BindingContext as PlayerViewModel)?.Source;
            return Task.FromResult(currentClipUrl?.Equals(url) ?? false);
        }

        public void Suspend()
        {
            (BindingContext as PlayerViewModel)?.SuspendCommand.Execute(null);
        }

        public void Resume()
        {
            (BindingContext as PlayerViewModel)?.ResumeCommand.Execute(null);
            PlayButton.Focus();
        }

        private void SetupDebounce()
        {
            _keySubscription?.Dispose();
            _keys = new Subject<string>();
            var keysThrottled = _keys.Throttle(TimeSpan.FromMilliseconds(DefaultTimeout));
            _keySubscription = keysThrottled.Subscribe(i =>
            {
                if (!Settings.IsVisible) Hide();
            }, SynchronizationContext.Current);

            _keys.OnNext("first");
        }
    }
}