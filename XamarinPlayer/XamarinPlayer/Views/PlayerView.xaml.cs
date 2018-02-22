using System;
using JuvoPlayer.Common;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinPlayer.Services;

namespace XamarinPlayer.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PlayerView : ContentPage
    {
        private readonly int DefaultTimeout = 5000;
        private readonly TimeSpan UpdateInterval = new TimeSpan(0, 0, 0, 0, 100);
        private readonly TimeSpan DefaultSeekTime = new TimeSpan(0, 0, 0, 20, 0);

        private IPlayerService _playerService;
        private int _hideTime;
        private bool _isPageDisappeared = false;
        private bool _isShowing = false;

        public static readonly BindableProperty ContentSourceProperty = BindableProperty.Create("ContentSource", typeof(ClipDefinition), typeof(PlayerView), default(ClipDefinition));
        public ClipDefinition ContentSource
        {
            set { SetValue(ContentSourceProperty, value); }
            get { return (ClipDefinition)GetValue(ContentSourceProperty); }
        }

        public PlayerView()
        {
            InitializeComponent();

            NavigationPage.SetHasNavigationBar(this, false);

            _playerService = DependencyService.Get<IPlayerService>(DependencyFetchTarget.NewInstance);
            _playerService.StateChanged += OnPlayerStateChanged;
            _playerService.PlaybackCompleted += OnPlaybackCompleted;
            _playerService.ShowSubtitle += OnShowSubtitle;

            PlayButton.Clicked += (s, e) => { Play(); };

            BackButton.Clicked += (s, e) => { Rewind(); };

            ForwardButton.Clicked += (s, e) => { Forward(); };

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
            if (e.Contains("Back"))
            {
                if (_playerService.State != PlayerState.Playing ||
                    _playerService.State == PlayerState.Playing && !Controller.IsVisible)
                {
                    Navigation.RemovePage(this);
                }
                else
                {
                    Hide();
                }
            }
            else
            {
                Show();

                if (e.Contains("Play") || e.Contains("Pause"))
                {
                    Play();
                }
                else if (e.Contains("Stop"))
                {
                    Navigation.RemovePage(this);
                }
                else if (e.Contains("Next"))
                {
                    Forward();
                }
                else if (e.Contains("Rewind"))
                {
                    Rewind();
                }
            }
        }

        private void Forward()
        {
            if (_playerService.State != PlayerState.Playing)
                return;

            if (_playerService.Duration - _playerService.CurrentPosition < DefaultSeekTime)
                _playerService.SeekTo(_playerService.Duration);
            else
                _playerService.SeekTo(_playerService.CurrentPosition + DefaultSeekTime);
        }

        private void Rewind()
        {
            if (_playerService.State != PlayerState.Playing)
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
            if (!_isShowing)
            {
                PlayButton.Focus();
                _isShowing = true;
            }
            Controller.IsVisible = true;
            _hideTime = timeout;
        }

        public void Hide()
        {
            Controller.IsVisible = false;
            _isShowing = false;
        }

        private void PlayerViewPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("ContentSource"))
            {
                if (ContentSource == null)
                    return;

                _playerService.SetSource(ContentSource);
            }
        }

        private void OnPlaybackCompleted()
        {
            if (_playerService.State != PlayerState.Error)
            {
                // Schedule closing the page on the next event loop. Give application time to finish
                // playbackCompleted event handling
                Device.StartTimer(TimeSpan.FromMilliseconds(0), () =>
                {
                    Navigation.RemovePage(this);

                    return false;
                });
                
            }
            else
            {
                // TODO: display an error
                UpdatePlayTime();
                Show();
            }
        }

        private void OnShowSubtitle(Subtitle subtitle)
        {
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            PlayButton.Focus();
            Device.StartTimer(UpdateInterval, UpdatePlayerControl);
        }

        protected override void OnDisappearing()
        {
            Device.StartTimer(TimeSpan.FromMilliseconds(0), () =>
            {
                _playerService.Dispose();
                _playerService = null;

                return false;
            });
            MessagingCenter.Unsubscribe<IKeyEventSender, string>(this, "KeyDown");
            _isPageDisappeared = true;

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

        private void OnPlayerStateChanged(object sender, Services.PlayerStateChangedEventArgs e)
        {
            if (e.State == PlayerState.Error)
            {
                BackButton.IsEnabled = false;
                ForwardButton.IsEnabled = false;
                PlayButton.IsEnabled = false;
            }
            else if (e.State == PlayerState.Prepared)
            {
                BackButton.IsEnabled = true;
                ForwardButton.IsEnabled = true;
                PlayButton.IsEnabled = true;
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
        }

        private string GetFormattedTime(TimeSpan time)
        {
            if (time.TotalHours > 1)
                return time.ToString(@"hh\:mm\:ss");
            else
                return time.ToString(@"mm\:ss");
        }

        private bool UpdatePlayerControl()
        {
            if (_isPageDisappeared)
                return false;

            Device.BeginInvokeOnMainThread(() => {
                if (_playerService.State != PlayerState.Playing)
                {
                    return;
                }

                UpdatePlayTime();

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
    }
}
