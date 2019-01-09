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
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinPlayer.Models;
using XamarinPlayer.Services;

namespace XamarinPlayer.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PlayerView : ContentPage, IContentPayloadHandler, ISuspendable
    {
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly int DefaultTimeout = 5000;
        private readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(100);
        private readonly TimeSpan DefaultSeekTime = TimeSpan.FromSeconds(20);

        private IPlayerService _playerService;
        private int _hideTime;
        private bool _isPageDisappeared;
        private bool _isShowing;
        private bool _hasFinished;

        public static readonly BindableProperty ContentSourceProperty = BindableProperty.Create("ContentSource", typeof(object), typeof(PlayerView));
        private PlayerState? suspendedPlayerState;

        public object ContentSource
        {
            set { SetValue(ContentSourceProperty, value); }
            get { return GetValue(ContentSourceProperty); }
        }

        public PlayerView()
        {
            InitializeComponent();

            NavigationPage.SetHasNavigationBar(this, false);

            _playerService = DependencyService.Get<IPlayerService>(DependencyFetchTarget.NewInstance);

            _playerService.StateChanged()
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(OnPlayerStateChanged, OnPlayerCompleted);

            _playerService.PlaybackError()
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(async message => await OnPlaybackError(message));

            _playerService.BufferingProgress()
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(OnBufferingProgress);

            PlayButton.Clicked += (s, e) => { Play(); };

            PropertyChanged += PlayerViewPropertyChanged;
        }

        private void Play()
        {
            if (_playerService.State == PlayerState.Playing)
                _playerService.Pause();
            else
                _playerService.Start();
        }

        private async Task OnPlaybackError(string message)
        {
            // Prevent multiple popups from occuring, display them only
            // if it is a very first error event.
            if (_hasFinished == false)
            {
                _hasFinished = true;

                Hide();
                _playerService.Stop();
                if (!string.IsNullOrEmpty(message))
                    await DisplayAlert("Playback Error", message, "OK");

                Navigation.RemovePage(this);
            }
        }

        private void OnBufferingProgress(int progress)
        {
            if (progress >= 100)
                InfoTextLabel.IsVisible = false;
            else
            {
                InfoTextLabel.Text = "Buffering...";
                InfoTextLabel.IsVisible = true;
            }
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
                if (_playerService.State < PlayerState.Playing ||
                    _playerService.State >= PlayerState.Playing && !_isShowing)
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
                        Hide();
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
                        _playerService.State == PlayerState.Paused)
                    {
                        _playerService.Start();
                    }
                    else if ((e.Contains("Pause") || e.Contains("XF86PlayBack")) &&
                             _playerService.State == PlayerState.Playing)
                    {
                        _playerService.Pause();
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
                    BindStreamPicker(AudioTrack, StreamDescription.StreamType.Audio);
                if (VideoQuality.ItemsSource == null)
                    BindStreamPicker(VideoQuality, StreamDescription.StreamType.Video);
                if (Subtitles.ItemsSource == null)
                    BindSubtitleStreamPicker();

                PlayButton.IsEnabled = false;

                AudioTrack.Focus();
            }
        }

        private void BindStreamPicker(Picker picker, StreamDescription.StreamType streamType)
        {
            var streams = _playerService.GetStreamsDescription(streamType);

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
                    Type = StreamDescription.StreamType.Subtitle
                }
            };

            streams.AddRange(_playerService.GetStreamsDescription(StreamDescription.StreamType.Subtitle));

            InitializePicker(Subtitles, streams);

            SelectDefaultStreamForPicker(Subtitles, streams);

            Subtitles.SelectedIndexChanged += (sender, args) =>
            {
                if (Subtitles.SelectedIndex == -1)
                    return;

                if (Subtitles.SelectedIndex == 0)
                {
                    _playerService.DeactivateStream(StreamDescription.StreamType.Subtitle);
                    return;
                }

                var stream = (StreamDescription) Subtitles.ItemsSource[Subtitles.SelectedIndex];
                try
                {
                    _playerService.ChangeActiveStream(stream);
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
                    var stream = (StreamDescription) picker.ItemsSource[picker.SelectedIndex];

                    _playerService.ChangeActiveStream(stream);
                }
            };
        }

        private void Forward()
        {
            if (!_playerService.IsSeekingSupported || _playerService.State < PlayerState.Playing)
                return;

            if (_playerService.Duration - _playerService.CurrentPosition < DefaultSeekTime)
                _playerService.SeekTo(_playerService.Duration);
            else
                _playerService.SeekTo(_playerService.CurrentPosition + DefaultSeekTime);
        }

        private void Rewind()
        {
            if (!_playerService.IsSeekingSupported || _playerService.State < PlayerState.Playing)
                return;

            if (_playerService.CurrentPosition < DefaultSeekTime)
                _playerService.SeekTo(TimeSpan.Zero);
            else
                _playerService.SeekTo(_playerService.CurrentPosition - DefaultSeekTime);
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

                _playerService.SetSource(ContentSource);
            }
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
                _playerService?.Dispose();
                _playerService = null;

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
                    if (_playerService.IsSeekingSupported)
                    {
                        BackButton.IsEnabled = true;
                        ForwardButton.IsEnabled = true;
                    }

                    PlayButton.IsEnabled = true;
                    SettingsButton.IsEnabled = true;
                    PlayButton.Focus();

                    _playerService.Start();
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
                if (_playerService.State < PlayerState.Playing)
                {
                    return;
                }

                UpdatePlayTime();
                UpdateCueTextLabel();

                if (Settings.IsVisible)
                    return;

                if (_hideTime > 0)
                {
                    _hideTime -= (int) UpdateInterval.TotalMilliseconds;
                    if (_hideTime <= 0)
                    {
                        Hide();
                    }
                }
            });

            return true;
        }

        private void UpdatePlayTime()
        {
            CurrentTime.Text = GetFormattedTime(_playerService.CurrentPosition);
            TotalTime.Text = GetFormattedTime(_playerService.Duration);

            if (_playerService.Duration.TotalMilliseconds > 0)
                Progressbar.Progress = _playerService.CurrentPosition.TotalMilliseconds /
                                       _playerService.Duration.TotalMilliseconds;
            else
                Progressbar.Progress = 0;
        }

        private void UpdateCueTextLabel()
        {
            var cueText = _playerService.CurrentCueText ?? string.Empty;
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
            suspendedPlayerState = _playerService?.State;
            _playerService?.Pause();
        }

        public void Resume()
        {
            if (suspendedPlayerState == PlayerState.Playing)
                _playerService?.Start();
        }
    }
}