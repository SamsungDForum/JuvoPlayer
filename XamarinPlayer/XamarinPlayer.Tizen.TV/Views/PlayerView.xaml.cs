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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer;
using JuvoPlayer.Common;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinPlayer.Models;
using XamarinPlayer.Services;
using Application = Tizen.Applications.Application;

namespace XamarinPlayer.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PlayerView : ContentPage, IContentPayloadHandler, ISuspendable, ISeekLogicClient
    {
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly int DefaultTimeout = 5000;
        private readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(100);

        private SeekLogic _seekLogic = null; // needs to be initialized in constructor!
        private StoryboardReader _storyboardReader;
        private int _hideTime;
        private bool _isPageDisappeared;
        private bool _isShowing;
        private bool _hasFinished;
        private readonly CompositeDisposable _subscriptions;
        public static readonly BindableProperty ContentSourceProperty =
            BindableProperty.Create("ContentSource", typeof(object), typeof(PlayerView));

        public object ContentSource
        {
            set { SetValue(ContentSourceProperty, value); }
            get { return GetValue(ContentSourceProperty); }
        }

        private bool _isBuffering;
        public IPlayerService Player { get; private set; }

        public PlayerView()
        {
            InitializeComponent();

            NavigationPage.SetHasNavigationBar(this, false);

            Player = DependencyService.Get<IPlayerService>(DependencyFetchTarget.NewInstance);

            _subscriptions = new CompositeDisposable
            {
                Player.StateChanged()
                    .ObserveOn(SynchronizationContext.Current)
                    .Subscribe(OnPlayerStateChanged, OnPlayerCompleted),

                Player.PlaybackError()
                    .ObserveOn(SynchronizationContext.Current)
                    .Subscribe(async message => await OnPlaybackError(message)),

                Player.BufferingProgress()
                    .ObserveOn(SynchronizationContext.Current)
                    .Subscribe(OnBufferingProgress)
            };

            PlayButton.Clicked += (s, e) => { Play(); };

            Progressbar.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == "Progress")
                    UpdateSeekPreviewFramePosition();
            };

            PropertyChanged += PlayerViewPropertyChanged;

            _seekLogic = new SeekLogic(this);
        }

        private void Play()
        {
            if (Player.State == PlayerState.Playing)
                Player.Pause();
            else
                Player.Start();
        }

        private async Task OnPlaybackError(string message)
        {
            // Prevent multiple popups from occuring, display them only
            // if it is a very first error event.
            if (_hasFinished == false)
            {
                _hasFinished = true;

                Hide();
                Player.Stop();
                if (!string.IsNullOrEmpty(message))
                    await DisplayAlert("Playback Error", message, "OK");

                Navigation.RemovePage(this);
            }
        }

        private void OnBufferingProgress(int progress)
        {
            _isBuffering = progress < 100;
        }

        private void KeyEventHandler(string e)
        {
            // Prevents key handling & focus change in Show().
            // Consider adding a call Focus(Focusable Object) where focus would be set in one place
            // and error status could be handled.

            if (_hasFinished)
            {
                return;
            }

            if (e.Contains("Back") && !e.Contains("XF86PlayBack"))
            {
                //If the 'return' button on standard or back arrow on the smart remote control was pressed do react depending on the playback state
                if (!_isShowing && !Settings.IsVisible)
                {
                    //return to the main menu showing all the video contents list
                    Navigation.RemovePage(this);
                }
                else
                {
                    if (Settings.IsVisible)
                    {
                        Settings.IsVisible = false;
                        PlayButton.IsEnabled = true;
                        PlayButton.Focus();
                    }
                    else
                    {
                        Hide();
                    }
                }
            }
            else
            {
                if (Settings.IsVisible)
                {
                    return;
                }

                if (_isShowing)
                {
                    if ((e.Contains("Play") || e.Contains("XF86PlayBack")) &&
                        Player.State == PlayerState.Paused)
                    {
                        Player.Start();
                    }
                    else if ((e.Contains("Pause") || e.Contains("XF86PlayBack")) &&
                             Player.State == PlayerState.Playing)
                    {
                        Player.Pause();
                    }

                    if ((e.Contains("Next") || e.Contains("Right")))
                    {
                        Forward();
                    }
                    else if ((e.Contains("Rewind") || e.Contains("Left")))
                    {
                        Rewind();
                    }
                    else if ((e.Contains("Up")))
                    {
                        HandleSettings();
                    }

                    //expand the time that playback control bar is on the screen
                    _hideTime = DefaultTimeout;
                }
                else
                {
                    if (e.Contains("Stop"))
                    {
                        Navigation.RemovePage(this);
                    }
                    else
                    {
                        //Make the playback control bar visible on the screen
                        Show();
                    }
                }
            }
        }

        private void HandleSettings()
        {
            Settings.IsVisible = !Settings.IsVisible;
            if (Settings.IsVisible)
            {
                if (AudioTrack.ItemsSource == null)
                    BindStreamPicker(AudioTrack, StreamType.Audio);
                if (VideoQuality.ItemsSource == null)
                    BindStreamPicker(VideoQuality, StreamType.Video);
                if (Subtitles.ItemsSource == null)
                    BindSubtitleStreamPicker();

                PlayButton.IsEnabled = false;

                AudioTrack.Focus();
            }
        }

        private void BindStreamPicker(Picker picker, StreamType streamType)
        {
            var streams = Player.GetStreamsDescription(streamType);

            InitializePicker(picker, streams);

            SelectDefaultStreamForPicker(picker, streams);

            RegisterSelectedIndexChangeEventForPicker(picker);
        }

        private void BindSubtitleStreamPicker()
        {
            var streams = new List<StreamDescription>
            {
                new StreamDescription
                {
                    Default = true,
                    Description = "off",
                    Id = 0,
                    StreamType = StreamType.Subtitle
                }
            };

            streams.AddRange(Player.GetStreamsDescription(StreamType.Subtitle));

            InitializePicker(Subtitles, streams);

            SelectDefaultStreamForPicker(Subtitles, streams);

            Subtitles.SelectedIndexChanged += (sender, args) =>
            {
                if (Subtitles.SelectedIndex == -1)
                    return;

                if (Subtitles.SelectedIndex == 0)
                {
                    Player.DeactivateStream(StreamType.Subtitle);
                    return;
                }

                var stream = (StreamDescription)Subtitles.ItemsSource[Subtitles.SelectedIndex];
                try
                {
                    Player.ChangeActiveStream(stream);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    Subtitles.SelectedIndex = 0;
                }
            };
        }

        private static void InitializePicker(Picker picker, List<StreamDescription> streams)
        {
            picker.ItemsSource = streams;
            picker.ItemDisplayBinding = new Binding("Description");
            picker.SelectedIndex = 0;
        }

        private static void SelectDefaultStreamForPicker(Picker picker, List<StreamDescription> streams)
        {
            for (var i = 0; i < streams.Count; ++i)
            {
                if (streams[i].Default)
                {
                    picker.SelectedIndex = i;
                    return;
                }
            }
        }

        private void RegisterSelectedIndexChangeEventForPicker(Picker picker)
        {
            picker.SelectedIndexChanged += (sender, args) =>
            {
                if (picker.SelectedIndex != -1)
                {
                    var stream = (StreamDescription)picker.ItemsSource[picker.SelectedIndex];

                    Player.ChangeActiveStream(stream);
                }
            };
        }

        public void Show()
        {
            Show(DefaultTimeout);
        }

        public void Show(int timeout)
        {
            // Do not show anything if error handling in progress.
            if (_hasFinished)
                return;

            if (!_isShowing)
            {
                PlayButton.Focus();
                _isShowing = true;
            }

            TopBar.IsVisible = true;
            BottomBar.IsVisible = true;
            _hideTime = timeout;
        }

        public void Hide()
        {
            TopBar.IsVisible = false;
            BottomBar.IsVisible = false;
            _isShowing = false;
        }

        private void PlayerViewPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("ContentSource"))
            {
                if (ContentSource == null)
                    return;

                var clipDefinition = ContentSource as ClipDefinition;
                if (clipDefinition?.SeekPreviewPath != null)
                {
                    InitializeSeekPreview(clipDefinition.SeekPreviewPath);
                }

                Player.SetSource(clipDefinition);
            }
        }

        private async void InitializeSeekPreview(string seekPreviewPath)
        {
            _storyboardReader?.Dispose();
            _storyboardReader = new StoryboardReader(seekPreviewPath);
            _seekLogic.StoryboardReader = _storyboardReader;

            try
            {
                await _storyboardReader.LoadTask;

                var size = _storyboardReader.FrameSize;
                SeekPreviewCanvas.WidthRequest = size.Width;
                SeekPreviewCanvas.HeightRequest = size.Height;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private void OnSeekPreviewCanvasOnPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            var frame = _seekLogic.GetSeekPreviewFrame();
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
                new Rectangle(offset, .0, SeekPreviewFrame.Width, SeekPreviewFrame.Height));
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _isPageDisappeared = false;
            MessagingCenter.Subscribe<IKeyEventSender, string>(this, "KeyDown", (s, e) => { KeyEventHandler(e); });

            PlayButton.Focus();
            Device.StartTimer(UpdateInterval, UpdatePlayerControl);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Moved marking _isPageDisappeared flag to very beginning.
            // OnPlayerStateChanged event handler may receive events accessing
            // _playerService while _playerService is being disposed/nullified
            // Not something we want...
            // Reproducible with fast playback start/exit before start completes.
            //
            _isPageDisappeared = true;

            Device.StartTimer(TimeSpan.FromMilliseconds(0), () =>
            {
                if (!_isPageDisappeared)
                    return false;
                _storyboardReader?.Dispose();
                _seekLogic.StoryboardReader = null;
                _storyboardReader = null;
                _subscriptions.Dispose();
                Player?.Dispose();
                Player = null;
                return false;
            });
            MessagingCenter.Unsubscribe<IKeyEventSender, string>(this, "KeyDown");
        }

        void OnTapGestureRecognizerControllerTapped(object sender, EventArgs args)
        {
            Hide();
        }

        void OnTapGestureRecognizerViewTapped(object sender, EventArgs args)
        {
            Show();
        }

        protected override bool OnBackButtonPressed()
        {
            return true;
        }

        private void OnPlayerStateChanged(PlayerState state)
        {
            Logger.Info($"Player State Changed: {state}");

            if (_isPageDisappeared)
            {
                Logger.Warn("Page scheduled for disappearing or already disappeared. Stale Event? Not Processed.");
                return;
            }

            switch (state)
            {
                case PlayerState.Prepared:
                    {
                        if (Player.IsSeekingSupported)
                        {
                            BackButton.IsEnabled = true;
                            ForwardButton.IsEnabled = true;
                        }

                        PlayButton.IsEnabled = true;
                        SettingsButton.IsEnabled = true;
                        PlayButton.Focus();

                        Player.Start();
                        Show();
                        break;
                    }
                case PlayerState.Playing:
                    PlayImage.Source = ImageSource.FromFile("btn_viewer_control_pause_normal.png");
                    break;
                case PlayerState.Paused:
                    PlayImage.Source = ImageSource.FromFile("btn_viewer_control_play_normal.png");
                    break;
            }
        }

        private void OnPlayerCompleted()
        {
            Logger.Info($"Player State completed");
            if (_hasFinished)
                return;

            _hasFinished = true;
            Navigation.RemovePage(this);
        }

        private string GetFormattedTime(TimeSpan time)
        {
            if (time.TotalHours > 1)
                return time.ToString(@"hh\:mm\:ss");
            return time.ToString(@"mm\:ss");
        }

        private bool UpdatePlayerControl()
        {
            if (_isPageDisappeared)
                return false;

            Device.BeginInvokeOnMainThread(() =>
            {
                if (Player.State < PlayerState.Paused)
                {
                    return;
                }

                UpdatePlayTime();
                UpdateCueTextLabel();
                UpdateLoadingIndicator();
                UpdateSeekPreview();

                if (Settings.IsVisible)
                    return;

                if (_hideTime > 0)
                {
                    _hideTime -= (int)UpdateInterval.TotalMilliseconds;
                    if (_hideTime <= 0)
                    {
                        Hide();
                    }
                }
            });

            return true;
        }

        private void UpdateSeekPreview()
        {
            if (_isShowing && _seekLogic.ShallDisplaySeekPreview())
            {
                if (!SeekPreviewContainer.IsVisible)
                    SeekPreviewContainer.IsVisible = true;
                SeekPreviewCanvas.InvalidateSurface();
            }
            else if (SeekPreviewContainer.IsVisible)
                SeekPreviewContainer.IsVisible = false;
        }

        private void UpdatePlayTime()
        {
            CurrentTime.Text = GetFormattedTime(_seekLogic.CurrentPositionUI);
            TotalTime.Text = GetFormattedTime(_seekLogic.Duration);

            if (_seekLogic.Duration.TotalMilliseconds > 0)
                Progressbar.Progress = _seekLogic.CurrentPositionUI.TotalMilliseconds /
                                       _seekLogic.Duration.TotalMilliseconds;
            else
                Progressbar.Progress = 0;
        }

        private void UpdateCueTextLabel()
        {
            var cueText = Player.CurrentCueText ?? string.Empty;
            if (string.IsNullOrEmpty(cueText))
            {
                CueTextLabel.IsVisible = false;
                return;
            }

            CueTextLabel.Text = cueText;
            CueTextLabel.IsVisible = true;
        }

        public bool HandleUrl(string url)
        {
            var currentClipUrl = (BindingContext as DetailContentData)?.Source;
            return currentClipUrl?.Equals(url) ?? false;
        }

        public void Suspend()
        {
            Player?.Suspend();
        }

        public void Resume()
        {
            Player?.Resume();
        }

        private void UpdateLoadingIndicator()
        {
            var isSeeking = _seekLogic.IsSeekInProgress || _seekLogic.IsSeekAccumulationInProgress;
            if ((isSeeking || _isBuffering) && (Player.State != PlayerState.Paused))
            {
                LoadingIndicator.IsRunning = true;
                LoadingIndicator.IsVisible = true;
            }
            else
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            }
        }

        private void Forward()
        {
            _seekLogic.SeekForward();
        }

        private void Rewind()
        {
            _seekLogic.SeekBackward();
        }
    }
}