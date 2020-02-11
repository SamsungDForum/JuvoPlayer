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
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Resolvers;
using Configuration;
using JuvoLogger;
using JuvoPlayer;
using JuvoPlayer.Common;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinPlayer.Models;
using XamarinPlayer.Services;
using XamarinPlayer.ViewModels;
using Application = Tizen.Applications.Application;

namespace XamarinPlayer.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PlayerView : ContentPage, IContentPayloadHandler, ISuspendable
    {
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        public static readonly BindableProperty ContentSourceProperty =
            BindableProperty.Create("ContentSource", typeof(object), typeof(PlayerView));
        
        public static readonly BindableProperty PlayButtonFocusProperty =
            BindableProperty.Create(
                propertyName: "PlayButtonFocus",
                returnType: typeof(bool),
                typeof(PlayerView),
                defaultValue: false,
                defaultBindingMode: BindingMode.OneWay,
                propertyChanged: (b, o, n) =>
                {
                    ((PlayerView)b).PlayButton.Focus();
                });
        
        public static readonly BindableProperty SeekPreviewProperty =
            BindableProperty.Create(
                propertyName: "SeekPreview",
                returnType: typeof(bool),
                typeof(PlayerView),
                defaultValue: false,
                defaultBindingMode: BindingMode.OneWay,
                propertyChanged: (b, o, n) =>
                {
                    ((PlayerView)b).SeekPreviewCanvas.InvalidateSurface();
                });

        public object ContentSource
        {
            set { SetValue(ContentSourceProperty, value); }
            get { return GetValue(ContentSourceProperty); }
        }

        public PlayerView()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            
            SetBinding(PlayButtonFocusProperty, new Binding(nameof(PlayerViewModel.PlayButtonFocus)));
            SetBinding(SeekPreviewProperty, new Binding(nameof(PlayerViewModel.SeekPreview)));

            PlayButton.Clicked += (s, e) => { (BindingContext as PlayerViewModel)?.PlayCommand.Execute(null); };

            Progressbar.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == "Progress")
                    UpdateSeekPreviewFramePosition();
            };
            PropertyChanged += PlayerViewPropertyChanged;
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

                (BindingContext as PlayerViewModel)?.SetSourceCommand.Execute(clipDefinition);
            }
        }

        private void InitializeSeekPreview(string seekPreviewPath)
        {
            (BindingContext as PlayerViewModel).InitializeSeekPreviewCommand.Execute(Path.Combine(Application.Current.DirectoryInfo.Resource, seekPreviewPath));
            var size = (BindingContext as PlayerViewModel).PreviewFrameSize;
            SeekPreviewCanvas.WidthRequest = size.Width;
            SeekPreviewCanvas.HeightRequest = size.Height;
        }

        private void OnSeekPreviewCanvasOnPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            var frame = (BindingContext as PlayerViewModel)?.PreviewFrame;
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
            MessagingCenter.Subscribe<IKeyEventSender, string>(this, "KeyDown", (s, e) => { KeyEventHandler(e); });

            SettingsButton.IsEnabled = true;
            PlayButton.IsEnabled = true;
            PlayButton.Focus();
        }
        
        private void KeyEventHandler(string e)
        {
            // Prevents key handling & focus change in Show().
            // Consider adding a call Focus(Focusable Object) where focus would be set in one place
            // and error status could be handled.
            
            if (e.Contains("Back") && !e.Contains("XF86PlayBack"))
            {
                //If the 'return' button on standard or back arrow on the smart remote control was pressed do react depending on the playback state
                (BindingContext as PlayerViewModel).BackCommand.Execute(null);
                if (!(BindingContext as PlayerViewModel).Playing)
                {
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
                        (BindingContext as PlayerViewModel).HideCommand.Execute(null);
                }
            }
            else
            {
                if (Settings.IsVisible)
                {
                    return;
                }
        
                if ((BindingContext as PlayerViewModel).Overlay)
                {
                    if(e.Contains("XF86PlayBack"))
                    {
                        (BindingContext as PlayerViewModel).PlayCommand.Execute(null);
                        // (BindingContext as PlayerViewModel).StartPause();
                    }
                    else if (e.Contains("Pause"))
                    {
                        (BindingContext as PlayerViewModel).PauseCommand.Execute(null);
                        // (BindingContext as PlayerViewModel).Pause();
                    }
                    else if (e.Contains("Play"))
                    {
                        (BindingContext as PlayerViewModel).StartCommand.Execute(null);
                        // (BindingContext as PlayerViewModel).Start();
                    }
        
                    if ((e.Contains("Next") || e.Contains("Right")))
                    {
                        (BindingContext as PlayerViewModel).ForwardCommand.Execute(null);
                        // (BindingContext as PlayerViewModel).Forward();
                    }
                    else if ((e.Contains("Rewind") || e.Contains("Left")))
                    {
                        (BindingContext as PlayerViewModel).RewindCommand.Execute(null);
                        // (BindingContext as PlayerViewModel).Rewind();
                    }
                    else if ((e.Contains("Up")))
                    {
                        (BindingContext as PlayerViewModel).HandleSettingsCommand.Execute(null);
                        Settings.IsVisible = true;
                        PlayButton.IsEnabled = false;
                        AudioTrack.Focus();
                    }

                    //expand the time that playback control bar is on the screen
                    (BindingContext as PlayerViewModel).ExpandPlaybackCommand.Execute(null);
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
                        (BindingContext as PlayerViewModel).ShowCommand.Execute(null);
                    }
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            (BindingContext as PlayerViewModel)?.DisposeCommand.Execute(null);
            MessagingCenter.Unsubscribe<IKeyEventSender, string>(this, "KeyDown");
        }

        void OnTapGestureRecognizerControllerTapped(object sender, EventArgs args)
        {
            (BindingContext as PlayerViewModel).HideCommand.Execute(null);
        }

        void OnTapGestureRecognizerViewTapped(object sender, EventArgs args)
        {
            (BindingContext as PlayerViewModel).ShowCommand.Execute(null);
        }

        protected override bool OnBackButtonPressed()
        {
            return true;
        }

        public bool HandleUrl(string url)
        {
            var currentClipUrl = (BindingContext as PlayerViewModel)?.Source;
            return currentClipUrl?.Equals(url) ?? false;
        }

        public void Suspend()
        {
            (BindingContext as PlayerViewModel)?.SuspendCommand.Execute(null);
        }

        public void Resume()
        {
            (BindingContext as PlayerViewModel)?.ResumeCommand.Execute(null);
        }
    }
}