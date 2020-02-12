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

using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using JuvoLogger;
using JuvoPlayer;
using JuvoPlayer.Common;
using SkiaSharp;
using Xamarin.Forms;
using XamarinPlayer.Models;
using XamarinPlayer.Services;
using XamarinPlayer.Tizen.Services;
using XamarinPlayer.Tizen.TV.ViewModels;
using XamarinPlayer.Views;

namespace XamarinPlayer.ViewModels
{
    public class PlayerViewModel: INotifyPropertyChanged,ISeekLogicClient,ISuspendable,IDisposable
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private DetailContentData _contentData;
        private SeekLogic _seekLogic = null; // needs to be initialized in constructor!
        private StoryboardReader _storyboardReader;
        private readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(100);
        private PlayerState? suspendedPlayerState;
        private bool _isPlayerDestroyed;
        private bool _hasFinished;
        private bool _isBuffering;
        private bool _isSeekingSupported;
        private bool _shallDisplaySeekPreview;
        private TimeSpan _currentTime = TimeSpan.Zero;
        private TimeSpan _totalTime = TimeSpan.Zero;
        private double _progress = 0;
        private string _cueText;
        private bool _loading;
        private SubSkBitmap _previewFrame;
        private SettingsPickerViewModel _audio = new SettingsPickerViewModel{Type = StreamType.Audio};
        private SettingsPickerViewModel _video = new SettingsPickerViewModel{Type = StreamType.Video};
        private SettingsPickerViewModel _subtitles = new SettingsPickerViewModel{Type = StreamType.Subtitle};
        
        public event PropertyChangedEventHandler PropertyChanged;
        public ICommand PlayCommand => new Command(Play);
        public ICommand PauseCommand => new Command(Pause);
        public ICommand StartCommand => new Command(Start);
        public ICommand ForwardCommand => new Command(Forward);
        public ICommand RewindCommand => new Command(Rewind);
        public ICommand HandleSettingsCommand => new Command(HandleSettings);
        public ICommand SuspendCommand => new Command(Suspend);
        public ICommand ResumeCommand => new Command(Resume);
        public ICommand DisposeCommand => new Command(Dispose);
        public ICommand SetSourceCommand => new Command(param => SetSource((ClipDefinition)param) );
        public ICommand InitializeSeekPreviewCommand => new Command(param => InitializeSeekPreview((string)param) );

        public PlayerViewModel(DetailContentData data)
        {
            _contentData = data;
            
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
            
            Device.StartTimer(UpdateInterval, UpdatePlayerControl);
            
            _seekLogic = new SeekLogic(this);
        }

        public string Source 
        {
            get => _contentData.Source;
            set => _contentData.Source = value;
        }
        public string Title 
        {
            get => _contentData.Title;
            set => _contentData.Title = value;
        }
        public string Description
        {
            get => _contentData.Description;
            set => _contentData.Description = value;
        }
        public object Clip
        {
            get => _contentData.Clip;
            set => _contentData.Clip = value;
        }

        public bool IsSeekingSupported
        {
            get => _isSeekingSupported;
            set
            {
                if (value == Player.IsSeekingSupported) return;
                _isSeekingSupported = value;
                OnPropertyChanged();
            }
        }

        public PlayerState PlayerState => Player.State;

        public SubSkBitmap PreviewFrame
        {
            get => _previewFrame;
            set
            {
                if (_previewFrame != null && _previewFrame.Equals(value))
                    return;
                _previewFrame = value;
                OnPropertyChanged();
            }
        }

        public SKSize PreviewFrameSize => _storyboardReader.FrameSize;

        public bool ShallDisplaySeekPreview
        {
            get => _shallDisplaySeekPreview;
            set
            {
                if (value != _shallDisplaySeekPreview)
                {
                    _shallDisplaySeekPreview = value;
                    OnPropertyChanged();
                }
            }
        }

        public TimeSpan CurrentTime
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
        
        public TimeSpan TotalTime
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
        
        public bool Loading
        {
            get => _loading;
            set
            {
                if (value != _loading)
                {
                    _loading = value;
                    OnPropertyChanged();
                }
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
                    var stream = AudioSource[_audio.SelectedIndex];

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
                    var stream = VideoSource[_video.SelectedIndex];
                    
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

                var stream = SubtitlesSource[_subtitles.SelectedIndex];
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

        public IPlayerService Player { get; set; }
        
        private void Play()
        {
            if (Player.State == PlayerState.Playing)
                Player.Pause();
            else
                Player.Start();
        }
        
        private void InitializeSeekPreview(string seekPreviewPath)
        {
            _storyboardReader?.Dispose();
            _storyboardReader =
                new StoryboardReader(seekPreviewPath);
            _seekLogic.StoryboardReader = _storyboardReader;
        }

        private SubSkBitmap GetSeekPreviewFrame()
        {
            return _seekLogic.GetSeekPreviewFrame();
        }

        private void SetSource(ClipDefinition clip)
        {
            Player.SetSource(clip);
        }
        
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        private void OnPlayerStateChanged(PlayerState state)
        {
            OnPropertyChanged("PlayerState");
            Logger.Info($"Player State Changed: {state}");

            if (_isPlayerDestroyed)
            {
                Logger.Warn("Page scheduled for disappearing or already disappeared. Stale Event? Not Processed.");
                return;
            }
            if( state==PlayerState.Prepared)
            {
                if (Player.IsSeekingSupported)
                {
                    IsSeekingSupported = true;
                }

                Player.Start();
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

        private void Start()
        {
            if (Player.State == PlayerState.Paused)
            {
                Player.Start();
            }
        }
        
        private void Pause()
        {
            if (Player.State == PlayerState.Playing)
            {
                Player.Pause();
            }
        }
        
        private void HandleSettings()
        {
            if (AudioSource == null)
            {
                BindStreamPicker(_audio);
            }
            if (VideoSource == null)
            {
                BindStreamPicker(_video);
            }
            if (SubtitlesSource == null)
            {
                BindSubtitleStreamPicker();
            }
        }
        
        private void BindStreamPicker(SettingsPickerViewModel settingsPicker)
        {
            var streams = Player.GetStreamsDescription(settingsPicker.Type);
            
            settingsPicker.Source = streams;
            settingsPicker.SelectedIndex = 0;

            SelectDefaultStreamForPicker(settingsPicker);
            OnPropertyChanged($"{settingsPicker.Type}Source");
            OnPropertyChanged($"{settingsPicker.Type}SelectedIndex");
            
        }
        
        private void SelectDefaultStreamForPicker(SettingsPickerViewModel settingsPicker)
        {
            for (var i = 0; i < settingsPicker.Source.Count; ++i)
            {
                if (settingsPicker.Source[i].Default)
                {
                    settingsPicker.SelectedIndex = i;
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
            OnPropertyChanged($"{_subtitles.Type}Source");
            OnPropertyChanged($"{_subtitles.Type}SelectedIndex");
        }
        
        private void Forward()
        {
            _seekLogic.SeekForward();
        }

        private void Rewind()
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
            
            if (_isPlayerDestroyed)
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
            });

            return true;
        }
        
        private void UpdateSeekPreview()
        {
            if (_seekLogic.ShallDisplaySeekPreview())
            {
                if (!ShallDisplaySeekPreview)
                    ShallDisplaySeekPreview = true;
                PreviewFrame = GetSeekPreviewFrame();
            }
            else if (ShallDisplaySeekPreview)
                ShallDisplaySeekPreview = false;
        }

        private void UpdatePlayTime()
        {
            CurrentTime = TimeSpan.ParseExact(GetFormattedTime(_seekLogic.CurrentPositionUI),@"mm\:ss", CultureInfo.CurrentCulture, TimeSpanStyles.AssumeNegative);
            TotalTime = TimeSpan.ParseExact(GetFormattedTime(_seekLogic.Duration),@"mm\:ss", CultureInfo.CurrentCulture, TimeSpanStyles.AssumeNegative);

            if (_seekLogic.Duration.TotalMilliseconds > 0)
                Progress = _seekLogic.CurrentPositionUI.TotalMilliseconds /
                                       _seekLogic.Duration.TotalMilliseconds;
            else
                Progress = 0;
        }

        private void UpdateCueTextLabel()
        {
            var cueText = Player.CurrentCueText ?? string.Empty;

            CueText = cueText;
        }
        
        private void UpdateLoadingIndicator()
        {
            var isSeeking = _seekLogic.IsSeekInProgress || _seekLogic.IsSeekAccumulationInProgress;
            if (isSeeking || _isBuffering)
            {
                Loading = true;
            }
            else
            {
                Loading = false;
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
            _isPlayerDestroyed = true;

            Device.StartTimer(TimeSpan.FromMilliseconds(0), () =>
            {
                if (!_isPlayerDestroyed)
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