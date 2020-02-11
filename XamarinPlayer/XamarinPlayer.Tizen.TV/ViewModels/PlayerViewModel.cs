using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System;
using System.Collections.Generic;
using JuvoLogger;
using JuvoPlayer;
using JuvoPlayer.Common;
using SkiaSharp;
using Xamarin.Forms;
using XamarinPlayer.Models;
using XamarinPlayer.Services;
using XamarinPlayer.Tizen.TV.ViewModels;
using XamarinPlayer.Views;

namespace XamarinPlayer.ViewModels
{
    public class PlayerViewModel: INotifyPropertyChanged,ISeekLogicClient,ISuspendable,IDisposable
    {
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        public DetailContentData ContentData;
        private SeekLogic _seekLogic = null; // needs to be initialized in constructor!
        private StoryboardReader _storyboardReader;
        private readonly int DefaultTimeout = 5000;
        private readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(100);
        private PlayerState? suspendedPlayerState;
        private int _hideTime;
        private bool _isPageDisappeared;
        private bool _hasFinished;
        private bool _isBuffering;
        private bool _isShowing;
        private bool _isSeekingSupported;
        private bool _topBarVisible;
        private bool _bottomBarVisible;
        private bool _settingsVisible;
        private bool _playButtonEnabled;
        private bool _settingsButtonEnabled;
        private bool _seekPreviewVisible;
        private string _currentTime = "00:00";
        private string _totalTime = "00:00";
        private double _progress = 0;
        private string _cueText;
        private bool _cueVisible;
        private bool _loadingVisible;
        private bool _loadingRunning;
        private ImageSource _playImageSource = ImageSource.FromFile("btn_viewer_control_play_normal.png");
        public PickerViewModel _audio = new PickerViewModel{Type = StreamType.Audio};
        public PickerViewModel _video = new PickerViewModel{Type = StreamType.Video};
        public PickerViewModel _subtitles = new PickerViewModel{Type = StreamType.Subtitle};
        
        public event PropertyChangedEventHandler PropertyChanged;

        public PlayerViewModel(DetailContentData data)
        {
            ContentData = data;
            
            Player = DependencyService.Get<IPlayerService>(DependencyFetchTarget.NewInstance);

            Player.StateChanged()
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(OnPlayerStateChanged, OnPlayerCompleted);

            Player.PlaybackError()
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(async message => await OnPlaybackError(message));

            Player.BufferingProgress()
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(OnBufferingProgress);
            
            _isPageDisappeared = false;
            
            Device.StartTimer(UpdateInterval, UpdatePlayerControl);
            
            _seekLogic = new SeekLogic(this);
        }

        public void Play()
        {
            if (Player.State == PlayerState.Playing)
                Player.Pause();
            else
                Player.Start();
        }
        
        public SKSize InitializeSeekPreview(string seekPreviewPath)
        {
            _storyboardReader?.Dispose();
            _storyboardReader =
                new StoryboardReader(seekPreviewPath);
            _seekLogic.StoryboardReader = _storyboardReader;

            var size = _storyboardReader.FrameSize;
            return size;
        }

        public SubSkBitmap GetSeekPreviewFrame()
        {
            return _seekLogic.GetSeekPreviewFrame();
        }

        public void SetSource(ClipDefinition clip)
        {
            Player.SetSource(clip);
        }
        
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        public string Image
        {
            get { return ContentData.Image;}
            set { ContentData.Image = value; }
        }
        public string Bg 
        {
            get { return ContentData.Bg;}
            set { ContentData.Bg = value; }
        }
        public string Source 
        {
            get { return ContentData.Source;}
            set { ContentData.Source = value; }
        }
        public string Title 
        {
            get { return ContentData.Title;}
            set { ContentData.Title = value; }
        }
        public string Description
        {
            get { return ContentData.Description;}
            set { ContentData.Description = value; }
        }
        public ICommand ContentFocusedCommand
        {
            get { return ContentData.ContentFocusedCommand;}
            set { ContentData.ContentFocusedCommand = value; }
        }
        public object Clip
        {
            get { return ContentData.Clip;}
            set { ContentData.Clip = value; }
        }
        public string TilePreviewPath
        {
            get { return ContentData.TilePreviewPath;}
            set { ContentData.TilePreviewPath = value; }
        }

        public bool IsSeekingSupported
        {
            get => _isSeekingSupported;
            set
            {
                if (value != Player.IsSeekingSupported)
                {
                    _isSeekingSupported = value;
                    OnPropertyChanged();
                }

            }
        }

        private bool _playButtonFocus;
        public bool PlayButtonFocus
        {
            get => _playButtonFocus;
            set
            {
                _playButtonFocus = value;
                OnPropertyChanged();
            }
        }
        
        private bool _seekPreview;
        public bool SeekPreview
        {
            get => _seekPreview;
            set
            {
                _seekPreview = value;
                OnPropertyChanged();
            }
        }

        public ImageSource PlayImageSource
        {
            get => _playImageSource;
            set
            {
                _playImageSource = value;
                OnPropertyChanged();
            }
        }
        
        public bool TopBarVisible
        {
            get => _topBarVisible;
            set
            {
                if (value != _topBarVisible)
                {
                    _topBarVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool BottomBarVisible
        {
            get => _bottomBarVisible;
            set
            {
                if (value != _bottomBarVisible)
                {
                    _bottomBarVisible = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool PlayButtonEnabled
        {
            get => _playButtonEnabled;
            set
            {
                _playButtonEnabled = value;
                OnPropertyChanged();
            }
        }
        public bool SettingsButtonEnabled
        {
            get => _settingsButtonEnabled;
            set
            {
                if (value != _settingsButtonEnabled)
                {
                    _settingsButtonEnabled = value;
                    OnPropertyChanged();
                }
            }
        }
        public bool SettingsVisible
        {
            get => _settingsVisible;
            set
            {
                if (value != _settingsVisible)
                {
                    _settingsVisible = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool IsShowing
        {
            get => _isShowing;
            set
            {
                if (value != _isShowing)
                {
                    _isShowing = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool SeekPreviewVisible
        {
            get => _seekPreviewVisible;
            set
            {
                if (value != _seekPreviewVisible)
                {
                    _seekPreviewVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CurrentTime
        {
            get => _currentTime;
            set
            {
                if (value != _currentTime)
                {
                    _currentTime = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public string TotalTime
        {
            get => _totalTime;
            set
            {
                if (value != _totalTime)
                {
                    _totalTime = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public double Progress
        {
            get => _progress;
            set
            {
                if (value != _progress)
                {
                    _progress = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public string CueText
        {
            get => _cueText;
            set
            {
                if (value != _cueText)
                {
                    _cueText = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool CueVisible
        {
            get => _cueVisible;
            set
            {
                if (value != _cueVisible)
                {
                    _cueVisible = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool LoadingVisible
        {
            get => _loadingVisible;
            set
            {
                if (value != _loadingVisible)
                {
                    _loadingVisible = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool LoadingRunning
        {
            get => _loadingRunning;
            set
            {
                _loadingRunning = value;
                OnPropertyChanged();
            }
        }

        public List<StreamDescription> AudioSource
        {
            get => _audio.Source;
            set
            {
                _audio.Source = value;
                OnPropertyChanged();
            }
        }

        public int AudioSelectedIndex
        {
            get => _audio.SelectedIndex;
            set
            {
                if (_audio.SelectedIndex != value&&value != -1)
                {
                    _audio.SelectedIndex = value;
                    var stream = (StreamDescription) AudioSource[_audio.SelectedIndex];

                    Player.ChangeActiveStream(stream);
                }
                OnPropertyChanged();
            }
        }
        
        public List<StreamDescription> VideoSource
        {
            get => _video.Source;
            set
            {
                _video.Source = value;
                OnPropertyChanged();
            }
        }

        public int VideoSelectedIndex
        {
            get => _video.SelectedIndex;
            set
            {
                if (_video.SelectedIndex != value&&value != -1)
                {
                    _video.SelectedIndex = value;
                    var stream = (StreamDescription) VideoSource[_video.SelectedIndex];
                    
                    Player.ChangeActiveStream(stream);
                }
                OnPropertyChanged();
            }
        }
        
        public List<StreamDescription> SubtitlesSource
        {
            get => _subtitles.Source;
            set
            {
                _subtitles.Source = value;
                OnPropertyChanged();
            }
        }

        public int SubtitlesSelectedIndex
        {
            get => _subtitles.SelectedIndex;
            set
            {
                if (value == -1||_subtitles.SelectedIndex==value)
                    return;
                
                _subtitles.SelectedIndex = value;

                if (_subtitles.SelectedIndex == 0)
                {
                    Player.DeactivateStream(StreamType.Subtitle);
                    OnPropertyChanged();
                    return;
                }

                var stream = (StreamDescription) SubtitlesSource[_subtitles.SelectedIndex];
                try
                {
                    Player.ChangeActiveStream(stream);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    _subtitles.SelectedIndex = 0;
                }
                OnPropertyChanged();
            }
        }

        public IPlayerService Player { get; private set; }
        
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
                        IsSeekingSupported = true;
                    }

                    PlayButtonEnabled = true;
                    SettingsButtonEnabled = true;
                    PlayButtonFocus = !PlayButtonFocus;
                    // BindStreams();
                    // Bind = true;

                    Player.Start();
                    Show();
                    break;
                }
                case PlayerState.Playing:
                {
                    PlayImageSource = ImageSource.FromFile("btn_viewer_control_pause_normal.png");
                    break;
                }
                case PlayerState.Paused:
                    PlayImageSource = ImageSource.FromFile("btn_viewer_control_play_normal.png");
                    break;
            }
        }
        private void OnPlayerCompleted()
        {
            Logger.Info($"Player State completed");
            if (_hasFinished)
                return;
            
            _hasFinished = true;
            Application.Current.MainPage.Navigation.PopAsync();
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
                    await Application.Current.MainPage.DisplayAlert("Playback Error", message, "OK");

                Application.Current.MainPage.Navigation.PopAsync();
            }
        }
        
        private void OnBufferingProgress(int progress)
        {
            _isBuffering = progress < 100;
        }
        
        public void Hide()
        {
            TopBarVisible = false;
            BottomBarVisible = false;
            IsShowing = false;
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
                PlayButtonFocus = !PlayButtonFocus;
                IsShowing = true;
            }

            TopBarVisible = true;
            BottomBarVisible = true;
            _hideTime = timeout;
        }

        public bool Back()
        {
            if (Player.State < PlayerState.Playing ||
                Player.State >= PlayerState.Playing && !_isShowing)
            {
                //return to the main menu showing all the video contents list
                return true;
            }
            else
            {
                if (SettingsVisible)
                {
                    SettingsVisible = false;
                    PlayButtonEnabled = true;
                    PlayButtonFocus = !PlayButtonFocus;
                }
                else
                    Hide();
            }

            return false;
        }

        public void Start()
        {
            if (_isShowing &&
                Player.State == PlayerState.Paused)
            {
                Player.Start();
            }
        }
        
        public void StartPause()
        {
            if (_isShowing)
            {
                if(Player.State == PlayerState.Playing)
                    Player.Pause();
                else if(Player.State == PlayerState.Paused)
                    Player.Start();
            }
        }
        
        public void Pause()
        {
            if (_isShowing &&
                Player.State == PlayerState.Playing)
            {
                Player.Pause();
            }
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
        
        public bool HandleSettings()
        {
            if (!SettingsVisible)
            {
                if (AudioSource == null)
                {
                    BindStreamPicker(_audio);
                    OnPropertyChanged("AudioSource");
                    OnPropertyChanged("AudioSelectedIndex");
                }
                if (VideoSource == null)
                {
                    BindStreamPicker(_video);
                    OnPropertyChanged("VideoSource");
                    OnPropertyChanged("VideoSelectedIndex");
                }
                if (SubtitlesSource == null)
                {
                    BindSubtitleStreamPicker();
                    OnPropertyChanged("SubtitlesSource");
                    OnPropertyChanged("SubtitlesSelectedIndex");
                }
                SettingsVisible = !SettingsVisible;
                PlayButtonEnabled = false;

                return true;
            }

            return false;
        }

        public void ExpandPlayback()
        {
            if(_isShowing)
                _hideTime = DefaultTimeout;
        }
        
        private void BindStreamPicker(PickerViewModel picker)
        {
            var streams = Player.GetStreamsDescription(picker.Type);
            
            picker.Source = streams;
            picker.SelectedIndex = 0;

            SelectDefaultStreamForPicker(picker);
        }
        
        private void SelectDefaultStreamForPicker(PickerViewModel picker)
        {
            for (var i = 0; i < picker.Source.Count; ++i)
            {
                if (picker.Source[i].Default)
                {
                    picker.SelectedIndex = i;
                    return;
                }
            }
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

            streams.AddRange(Player.GetStreamsDescription(_subtitles.Type));

            _subtitles.Source = streams;
            _subtitles.SelectedIndex = 0;

            SelectDefaultStreamForPicker(_subtitles);
        }
        
        public void Forward()
        {
            _seekLogic.SeekForward();
        }

        public void Rewind()
        {
            _seekLogic.SeekBackward();
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
                if (Player.State < PlayerState.Playing)
                {
                    return;
                }

                UpdatePlayTime();
                UpdateCueTextLabel();
                UpdateLoadingIndicator();
                UpdateSeekPreview();

                if (SettingsVisible)
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
        
        private void UpdateSeekPreview()
        {
            if (_isShowing && _seekLogic.ShallDisplaySeekPreview())
            {
                if (!SeekPreviewVisible)
                    SeekPreviewVisible = true;
                SeekPreview = !SeekPreview;
            }
            else if (SeekPreviewVisible)
                SeekPreviewVisible = false;
        }

        private void UpdatePlayTime()
        {
            CurrentTime = GetFormattedTime(_seekLogic.CurrentPositionUI);
            TotalTime = GetFormattedTime(_seekLogic.Duration);

            if (_seekLogic.Duration.TotalMilliseconds > 0)
                Progress = _seekLogic.CurrentPositionUI.TotalMilliseconds /
                                       _seekLogic.Duration.TotalMilliseconds;
            else
                Progress = 0;
        }

        private void UpdateCueTextLabel()
        {
            var cueText = Player.CurrentCueText ?? string.Empty;
            if (string.IsNullOrEmpty(cueText))
            {
                CueVisible = false;
                return;
            }

            CueText = cueText;
            CueVisible = true;
        }
        
        private void UpdateLoadingIndicator()
        {
            var isSeeking = _seekLogic.IsSeekInProgress || _seekLogic.IsSeekAccumulationInProgress;
            if (isSeeking || _isBuffering)
            {
                LoadingRunning = true;
                LoadingVisible = true;
            }
            else
            {
                LoadingRunning = false;
                LoadingVisible = false;
            }
        }
        
        public void Suspend()
        {
            suspendedPlayerState = Player?.State;
            Player?.Pause();
        }

        public void Resume()
        {
            if (suspendedPlayerState == PlayerState.Playing)
                Player?.Start();
        }

        public void Dispose()
        {
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
                _storyboardReader = null;
                Player?.Dispose();
                Player = null;

                return false;
            });
        }
    }
}