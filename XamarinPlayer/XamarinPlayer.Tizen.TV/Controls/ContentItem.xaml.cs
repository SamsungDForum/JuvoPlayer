/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2019, Samsung Electronics Co., Ltd
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
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.ResourceLoaders;
using Nito.AsyncEx;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace XamarinPlayer.Tizen.TV.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ContentItem
    {
        private static SKColor FocusedColor = new SKColor(234, 234, 234);
        private static SKColor UnfocusedColor = new SKColor(32, 32, 32);

        private ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private SKBitmap _contentBitmap;
        private SubSkBitmap _previewBitmap;
        private SKPaint _paint = new SKPaint {IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3};
        private double _height;
        private bool _isFocused;
        private CancellationTokenSource _animationCts;
        private StoryboardReader _storyboardReader;

        public static readonly BindableProperty ContentImgProperty = BindableProperty.Create("ContentImg",
            typeof(string), typeof(ContentItem), default(ICollection<string>));

        public string ContentImg
        {
            set { SetValue(ContentImgProperty, value); }
            get { return (string) GetValue(ContentImgProperty); }
        }

        public static readonly BindableProperty ContentTitleProperty =
            BindableProperty.Create("ContentTitle", typeof(string), typeof(ContentItem), default(string));

        public string ContentTitle
        {
            set { SetValue(ContentTitleProperty, value); }
            get { return (string) GetValue(ContentTitleProperty); }
        }

        public static readonly BindableProperty ContentDescriptionProperty =
            BindableProperty.Create("ContentDescription", typeof(string), typeof(ContentItem), default(string));

        public string ContentDescription
        {
            set { SetValue(ContentDescriptionProperty, value); }
            get { return (string) GetValue(ContentDescriptionProperty); }
        }

        public static readonly BindableProperty ContentTilePreviewPathProperty =
            BindableProperty.Create("ContentTilePreviewPath", typeof(string), typeof(ContentItem), default(string));

        public string ContentTilePreviewPath
        {
            set { SetValue(ContentTilePreviewPathProperty, value); }
            get { return (string) GetValue(ContentTilePreviewPathProperty); }
        }

        public ContentItem()
        {
            InitializeComponent();
        }

        public async void SetFocus()
        {
            using (_animationCts = new CancellationTokenSource())
            {
                var token = _animationCts.Token;
                try
                {
                    _isFocused = true;
                    this.AbortAnimation("ScaleTo");
                    await this.ScaleTo(0.9);
                    if (!_isFocused)
                        return;

                    InvalidateSurface();

                    if (ContentTilePreviewPath == null) return;

                    if (_storyboardReader == null)
                        _storyboardReader = new StoryboardReader(ContentTilePreviewPath,
                            StoryboardReader.PreloadingStrategy.PreloadOnlyRemoteSources);

                    await Task.WhenAll(Task.Delay(500), _storyboardReader.LoadTask).WaitAsync(token);
                    if (_storyboardReader == null || !_isFocused) return;

                    var tilePreviewDuration = _storyboardReader.Duration();
                    var animation = new Animation
                    {
                        {
                            0, 1, new Animation(t =>
                                {
                                    var position = TimeSpan.FromMilliseconds(t);
                                    var previewBitmap = _storyboardReader.GetFrame(position);
                                    if (previewBitmap == null) return;
                                    _previewBitmap = previewBitmap;
                                    InvalidateSurface();
                                }, 0,
                                tilePreviewDuration.TotalMilliseconds)
                        }
                    };
                    animation.Commit(this, "Animation", 1000 / 5, (uint) (tilePreviewDuration.TotalMilliseconds / 6),
                        repeat: () => true);
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
            }
        }

        public void ResetFocus()
        {
            try
            {
                _animationCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _isFocused = false;
            this.AbortAnimation("ScaleTo");
            this.AbortAnimation("Animation");
            this.ScaleTo(1);
            _storyboardReader?.Dispose();
            _storyboardReader = null;
            _previewBitmap = null;
            InvalidateSurface();
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            (SKBitmap, SKRect) GetCurrentBitmap()
            {
                if (_previewBitmap != null) return (_previewBitmap.Bitmap, _previewBitmap.SkRect);
                if (_contentBitmap != null) return (_contentBitmap, _contentBitmap.Info.Rect);
                return (null, SKRect.Empty);
            }

            var info = e.Info;
            var surface = e.Surface;
            var canvas = surface.Canvas;

            var (bitmap, srcRect) = GetCurrentBitmap();
            if (bitmap == null)
                return;

            var dstRect = info.Rect;
            var borderColor = _isFocused ? FocusedColor : UnfocusedColor;
            _paint.Color = borderColor;

            using (var path = new SKPath())
            using (var roundRect = new SKRoundRect(dstRect, 30, 30))
            {
                canvas.Clear();
                path.AddRoundRect(roundRect);
                canvas.ClipPath(path, antialias: true);
                canvas.DrawBitmap(bitmap, srcRect, dstRect);
                canvas.DrawRoundRect(roundRect, _paint);
            }
        }

        private async void LoadSkBitmap()
        {
            using (var resource = ResourceFactory.Create(ContentImg))
            {
                var newBitmap = await Task.Run(async () =>
                {
                    using (var stream = await resource.ReadAsStreamAsync())
                    {
                        return SKBitmap.Decode(stream);
                    }
                });
                _contentBitmap?.Dispose();
                _contentBitmap = newBitmap;
                InvalidateSurface();
            }
        }

        public void SetHeight(double height)
        {
            _height = height;
            HeightRequest = _height;
            WidthRequest = _height * 1.8;
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            const double tolerance = 0.001;
            if (Math.Abs(width - -1) < tolerance || Math.Abs(height - -1) < tolerance)
                return;

            if (Math.Abs(_height) < tolerance)
                WidthRequest = height * 1.8;
        }

        protected override void OnPropertyChanged(string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            if (propertyName == "ContentImg")
            {
                LoadSkBitmap();
                InvalidateSurface();
            }
        }
    }
}