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
using JuvoPlayer.Common.Utils.IReferenceCountableExtensions;
using Nito.AsyncEx;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinPlayer.Tizen.TV.Services;

namespace XamarinPlayer.Tizen.TV.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ContentItem
    {
        private static readonly SKColor FocusedColor = new SKColor(234, 234, 234);
        private static readonly SKColor UnfocusedColor = new SKColor(32, 32, 32);

        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private SKBitmapRefCounted _contentBitmap;
        private SubSkBitmap _previewBitmap;
        private readonly SKPaint _paint = new SKPaint {IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3};
        private double _height;
        private bool _isFocused;
        private CancellationTokenSource _animationCts;
        private StoryboardReader _storyboardReader;
        private readonly SKBitmapCache _skBitmapCache;

        public static readonly BindableProperty ContentImgProperty = BindableProperty.Create("ContentImg",
            typeof(string), typeof(ContentItem), default(ICollection<string>));

        public string ContentImg
        {
            set => SetValue(ContentImgProperty, value);
            get => (string) GetValue(ContentImgProperty);
        }

        public static readonly BindableProperty ContentTitleProperty =
            BindableProperty.Create("ContentTitle", typeof(string), typeof(ContentItem), default(string));

        public string ContentTitle
        {
            set => SetValue(ContentTitleProperty, value);
            get => (string) GetValue(ContentTitleProperty);
        }

        public static readonly BindableProperty ContentDescriptionProperty =
            BindableProperty.Create("ContentDescription", typeof(string), typeof(ContentItem), default(string));

        public string ContentDescription
        {
            set => SetValue(ContentDescriptionProperty, value);
            get => (string) GetValue(ContentDescriptionProperty);
        }

        public static readonly BindableProperty ContentTilePreviewPathProperty =
            BindableProperty.Create("ContentTilePreviewPath", typeof(string), typeof(ContentItem), default(string));

        private const string DefaultImagePath = "tiles/default_bg.png";

        public string ContentTilePreviewPath
        {
            set => SetValue(ContentTilePreviewPathProperty, value);
            get => (string) GetValue(ContentTilePreviewPathProperty);
        }

        public ContentItem()
        {
            InitializeComponent();
            var cacheService = DependencyService.Get<ISKBitmapCacheService>();
            _skBitmapCache = cacheService.GetCache();
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
                            StoryboardReader.PreloadingStrategy.PreloadOnlyRemoteSources, _skBitmapCache);

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
                if (_contentBitmap != null) return (_contentBitmap.Value, _contentBitmap.Value.Info.Rect);
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

        private Task<SKBitmapRefCounted> GetBitmap(string imagePath)
        {
            return _skBitmapCache.GetBitmap(imagePath);
        }

        private async void LoadSkBitmap()
        {
            SKBitmapRefCounted newBitmap = null;
            try
            {
                newBitmap = await GetBitmap(ContentImg);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                newBitmap = await GetBitmap(DefaultImagePath);
            }
            finally
            {
                _contentBitmap?.Release();
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

            if (propertyName != "ContentImg")
                return;
            LoadSkBitmap();
            InvalidateSurface();
        }
    }
}