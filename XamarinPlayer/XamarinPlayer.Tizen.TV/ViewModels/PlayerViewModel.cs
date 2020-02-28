/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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
using System.Reactive.Disposables;
using JuvoLogger;
using JuvoPlayer;
using JuvoPlayer.Common;
using SkiaSharp;
using Xamarin.Forms;
using XamarinPlayer.Tizen.TV.Models;
using XamarinPlayer.Tizen.TV.Services;
using XamarinPlayer.Tizen.TV.Views;

namespace XamarinPlayer.Tizen.TV.ViewModels
{
    public class PlayerViewModel : INotifyPropertyChanged, ISeekLogicClient, ISuspendable, IDisposable, IEventSender
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private DetailContentData _contentData;
        private SeekLogic _seekLogic; // needs to be initialized in constructor!
        private StoryboardReader _storyboardReader;
        private readonly CompositeDisposable _subscriptions;
        private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(100);
        private bool _isPlayerDestroyed;
        private bool _hasFinished;
        private bool _isBuffering;
        private bool _isSeekingSupported;
        private bool _shallDisplaySeekPreview;
        private TimeSpan _currentTime = TimeSpan.Zero;
        private TimeSpan _totalTime = TimeSpan.Zero;
        private double _progress;
        private string _cueText;
        private bool _loading;
        private SubSkBitmap _previewFrame;
        private SettingsViewModel _audio = new SettingsViewModel {Type = StreamType.Audio};
        private SettingsViewModel _video = new SettingsViewModel {Type = StreamType.Video};
        private SettingsViewModel _subtitles = new SettingsViewModel {Type = StreamType.Subtitle};

        public event PropertyChangedEventHandler PropertyChanged;
        public ICommand PlayOrPauseCommand => new Command(PlayOrPause);
        public ICommand PauseCommand => new Command(Pause);
        public ICommand StartCommand => new Command(Start);
        public ICommand ForwardCommand => new Command(Forward);
        public ICommand RewindCommand => new Command(Rewind);
        public ICommand SuspendCommand => new Command(Suspend);
        public ICommand ResumeCommand => new Command(Resume);
        public ICommand DisposeCommand => new Command(Dispose);

        public PlayerViewModel(DetailContentData data)
        {
            _contentData = data;

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

            InitializeSeekPreview();
            SetSource(_contentData.Clip as ClipDefinition);
            
            Device.StartTimer(UpdateInterval, UpdatePlayerControl);
        }

        public string Source => _contentData.Source;
        public string Title => _contentData.Title;
        public string Description => _contentData.Description;
        public bool IsSeekingSupported => Player.IsSeekingSupported;

        public PlayerState PlayerState => Player.State;

        public SubSkBitmap PreviewFrame
        {
            get => _previewFrame;
            set
            {
                if (_previewFrame == value)
                    return;
                _previewFrame = value;
                OnPropertyChanged();
            }
        }

        public SKSize? PreviewFrameSize => _storyboardReader?.FrameSize;

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

        public SettingsViewModel Audio
        {
            get => _audio;
            set => _audio = value;
        }

        public SettingsViewModel Video
        {
            get => _video;
            set => _video = value;
        }

        public SettingsViewModel Subtitle
        {
            get => _subtitles;
            set => _subtitles = value;
        }

        public int AudioSelectedIndex
        {
            get => _audio.SelectedIndex;
            set
            {
                if (_audio.SelectedIndex != value && value != -1)
                {
                    _audio.SelectedIndex = value;
                    var stream = Audio.Source[_audio.SelectedIndex];

                    Player.ChangeActiveStream(stream);
                    OnPropertyChanged();
                }
            }
        }

        public int VideoSelectedIndex
        {
            get => Video.SelectedIndex;
            set
            {
                if (Video.SelectedIndex != value && value != -1)
                {
                    Video.SelectedIndex = value;
                    var stream = Video.Source[Video.SelectedIndex];

                    Player.ChangeActiveStream(stream);
                    OnPropertyChanged();
                }
            }
        }

        public int SubtitleSelectedIndex
        {
            get => Subtitle.SelectedIndex;
            set
            {
                if (Subtitle.SelectedIndex == value || value == -1)
                    return;

                Subtitle.SelectedIndex = value;

                if (Subtitle.SelectedIndex == 0)
                {
                    Player.DeactivateStream(StreamType.Subtitle);
                    OnPropertyChanged();
                    return;
                }

                var stream = Subtitle.Source[Subtitle.SelectedIndex];
                try
                {
                    Player.ChangeActiveStream(stream);
                    OnPropertyChanged();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    Subtitle.SelectedIndex = 0;
                }
            }
        }

        public IPlayerService Player { get; set; }

        private void PlayOrPause()
        {
            if (Player.State == PlayerState.Playing)
                Player.Pause();
            else
                Player.Start();
        }

        private void InitializeSeekPreview()
        {
            _seekLogic = new SeekLogic(this);
            var clipDefinition = _contentData.Clip as ClipDefinition;
            if (clipDefinition?.SeekPreviewPath != null)
            {
                InitializeSeekPreview(clipDefinition.SeekPreviewPath);
            }
        }

        private async void InitializeSeekPreview(string seekPreviewPath)
        {
            _storyboardReader?.Dispose();
            _storyboardReader =
                new StoryboardReader(seekPreviewPath);
            _seekLogic.StoryboardReader = _storyboardReader;
            try
            {
                await _storyboardReader.LoadTask;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private SubSkBitmap GetSeekPreviewFrame()
        {
            return _seekLogic.GetSeekPreviewFrame();
        }

        private void SetSource(ClipDefinition clip)
        {
            Player.SetSource(clip);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnPlayerStateChanged(PlayerState state)
        {
            OnPropertyChanged("PlayerState");
            Logger.Info($"Player State Changed: {state}");

            if (_isPlayerDestroyed)
            {
                Logger.Info("Player has been disposed.");
                return;
            }

            if (state == PlayerState.Prepared)
            {
                BindStreamSettings(Audio);
                BindStreamSettings(Video);
                BindSubtitleStreamSettings();

                Player.Start();
            }
        }

        private void OnPlayerCompleted()
        {
            Logger.Info($"Player State completed");
            if (_hasFinished)
                return;

            _hasFinished = true;
            
            MessagingCenter.Send<IEventSender, string>(this, "Pop", null);
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
                    MessagingCenter.Send<IEventSender, string>(this, "PlaybackError", null);

                MessagingCenter.Send<IEventSender, string>(this, "Pop", null);
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

        private void BindStreamSettings(SettingsViewModel settings)
        {
            var streams = Player.GetStreamsDescription(settings.Type);

            settings.Source = streams;
            settings.SelectedIndex = 0;

            SelectDefaultStream(settings);
            OnPropertyChanged($"{settings.Type}SelectedIndex");
        }

        private void SelectDefaultStream(SettingsViewModel settings)
        {
            for (var i = 0; i < settings.Source.Count; ++i)
            {
                if (settings.Source[i].Default)
                {
                    settings.SelectedIndex = i;
                    return;
                }
            }
        }

        private void BindSubtitleStreamSettings()
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

            streams.AddRange(Player.GetStreamsDescription(Subtitle.Type));

            Subtitle.Source = streams;
            Subtitle.SelectedIndex = 0;

            SelectDefaultStream(Subtitle);
            OnPropertyChanged($"{Subtitle.Type}SelectedIndex");
        }

        private void Forward()
        {
            _seekLogic.SeekForward();
        }

        private void Rewind()
        {
            _seekLogic.SeekBackward();
        }

        private bool UpdatePlayerControl()
        {
            if (_isPlayerDestroyed)
                return false;

            if (Player.State >= PlayerState.Playing)
            {
                UpdatePlayTime();
                UpdateCueText();
                UpdateLoadingState();
                UpdateSeekPreview();
            }

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
            CurrentTime = _seekLogic.CurrentPositionUI;
            TotalTime = _seekLogic.Duration;

            if (_seekLogic.Duration.TotalMilliseconds > 0)
                Progress = _seekLogic.CurrentPositionUI.TotalMilliseconds /
                           _seekLogic.Duration.TotalMilliseconds;
            else
                Progress = 0;
        }

        private void UpdateCueText()
        {
            var cueText = Player.CurrentCueText ?? string.Empty;

            CueText = cueText;
        }

        private void UpdateLoadingState()
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
            Player?.Suspend();
        }

        public void Resume()
        {
            Player?.Resume();
        }

        public void Dispose()
        {
            // _isPageDisappeared flag should be marked at the very beginning.
            // OnPlayerStateChanged event handler may receive events accessing
            // _playerService while _playerService is being disposed/nullified
            // Not something we want...
            // Reproducible with fast playback start/exit before start completes.
            //
            _isPlayerDestroyed = true;
            _storyboardReader?.Dispose();
            _storyboardReader = null;
            _subscriptions.Dispose();
            Player?.Dispose();
            Player = null;
        }
    }
}