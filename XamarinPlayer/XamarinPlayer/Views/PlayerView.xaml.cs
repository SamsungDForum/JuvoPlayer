using System;
using System.Collections.Generic;
using System.ComponentModel;
using JuvoLogger;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinPlayer.Services;

namespace XamarinPlayer.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PlayerView : ContentPage
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
        private string _errorMessage;

        public static readonly BindableProperty ContentSourceProperty = BindableProperty.Create("ContentSource", typeof(object), typeof(PlayerView), null);

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
            _playerService.StateChanged += OnPlayerStateChanged;

            PlayButton.Clicked += (s, e) => { Play(); };

            PropertyChanged += PlayerViewPropertyChanged;

            MessagingCenter.Subscribe<IKeyEventSender, string>(this, "KeyDown", (s, e) => { KeyEventHandler(e); });            

        }



        private void Play()
        {
            if (_playerService.State == PlayerState.Playing)
                _playerService.Pause();
            else
                _playerService.Start();
        }
               

        private void KeyEventHandler(string e)
        {
            // TODO: This is a workaround for alertbox & lost focus
            // Prevents key handling & fous change in Show().
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
                    if ((e.Contains("Play") || e.Contains("XF86PlayBack")) && _playerService.State == PlayerState.Paused)
                    {
                        _playerService.Start();
                    }
                    else if ((e.Contains("Pause") || e.Contains("XF86PlayBack")) && _playerService.State == PlayerState.Playing)
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

                var stream = (StreamDescription)Subtitles.ItemsSource[Subtitles.SelectedIndex];
                try
                {
                    _playerService.ChangeActiveStream(stream);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message);
                    Logger.Error(ex.StackTrace);
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

            PlayButton.Focus();
            Device.StartTimer(UpdateInterval, UpdatePlayerControl);
        }

        protected override void OnDisappearing()
        {
            // Moved marking _isPageDisappeared flag to very beginning.
            // OnPlayerStateChanged event handler may recieve events accessing
            // _playerService while _playerService is being disposed/nullified
            // Not something we want...
            // Reproducable with fast playback start/exit before start completes.
            //
            _isPageDisappeared = true;

            Device.StartTimer(TimeSpan.FromMilliseconds(0), () =>
            {
                _playerService?.Dispose();
                _playerService = null;

                return false;
            });
            MessagingCenter.Unsubscribe<IKeyEventSender, string>(this, "KeyDown");


            base.OnDisappearing();
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

        void OnPlaybackFinished()
        {
            // DisplayAlert seems emotionally unstable
            // if not called from main thread.
            Device.BeginInvokeOnMainThread(async () =>
            {
                if (!string.IsNullOrEmpty(_errorMessage))
                    await DisplayAlert("Playback Error", _errorMessage, "OK");

                Navigation.RemovePage(this);
            });
        }

        /// <summary>
        /// This re-router through main thread has been added in order
        /// to accomodate AsyncPrepare for ESPlayer.
        /// AsyncPrepare cannot be awiaited in any form (none found).
        /// If awiated - does not complete. Issuing prepared event from thread
        /// other then UI causes load file exceptions.
        /// If this redirector will cause issues, more selective approach may need
        /// to be applied - i.e. prepare event only
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPlayerStateChanged(object sender, PlayerStateChangedEventArgs e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                HandleStateChanged(sender, e);
            });
        }
        private void HandleStateChanged(object sender, PlayerStateChangedEventArgs e)
        {
            Logger.Info($"Player State Changed: {e.State}");

            if (_isPageDisappeared)
            {
                Logger.Warn($"Page scheduled for disappearing or already disappeared. Stale Event? Not Processed.");
                return;
            }

            if (e.State == PlayerState.Stopped)
            {
                if (_hasFinished)
                    OnPlaybackFinished();
            }
            if (e.State == PlayerState.Completed)
            {
                // Do not remove any screens in case of error. Will be done as part
                // of error handling during Stopped event.
                if (_hasFinished == false)
                {
                    _hasFinished = true;
                    OnPlaybackFinished();
                }
            }
            else if (e.State == PlayerState.Error)
            {
                // Prevent multiple popups from occouring, display them only
                // if it is a very first error event.
                if (_hasFinished == false)
                {
                    _hasFinished = true;
                    _errorMessage = (e as PlayerStateChangedStreamError)?.Message ?? "Unknown Error";

                    // Terminate player to prevent any futher error events.
                    // This will issue a player.stopped event during which
                    // error message will be displayed.(if error flag is set).
                    // Hide controls. If not hidden, a timeouts take away focus rendering alert
                    // unclosable.
                    //
                    Device.StartTimer(TimeSpan.FromMilliseconds(0), () =>
                    {
                        Hide();
                        _playerService.Stop();
                        return false;
                    });
                }
            }
            else if (e.State == PlayerState.Prepared)
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
            }
            else if (e.State == PlayerState.Playing)
            {
                PlayImage.Source = ImageSource.FromFile("btn_viewer_control_pause_normal.png");
            }
            else if (e.State == PlayerState.Paused)
            {
                PlayImage.Source = ImageSource.FromFile("btn_viewer_control_play_normal.png");
            }
            else if (e.State == PlayerState.Stopped)
            {
                PlayImage.Source = ImageSource.FromFile("btn_viewer_control_play_normal.png");
            }

            if (e.State == PlayerState.Buffering)
            {
                InfoTextLabel.Text = "Buffering...";
                InfoTextLabel.IsVisible = true;
            }
            else
                InfoTextLabel.IsVisible = false;
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
                    _hideTime -= (int)UpdateInterval.TotalMilliseconds;
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
                Progressbar.Progress = _playerService.CurrentPosition.TotalMilliseconds / _playerService.Duration.TotalMilliseconds;
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
    }
}
