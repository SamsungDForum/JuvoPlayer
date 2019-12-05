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
using System.IO;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Application = Tizen.Applications.Application;

namespace XamarinPlayer.Tizen.TV.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ContentItem
    {
        private static SKColor FocusedColor = new SKColor(234, 234, 234);
        private static SKColor UnfocusedColor = new SKColor(32, 32, 32);

        private SKBitmap _contentBitmap;
        private SubSkBitmap _previewBitmap;
        private double _height;
        private bool _isFocused;

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
            _isFocused = true;
#pragma warning disable 4014
            this.ScaleTo(0.9);
#pragma warning restore 4014
            InvalidateSurface();

            await Task.Delay(TimeSpan.FromMilliseconds(500));
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
            animation.Commit(this, "Animation", 1000 / 30, (uint) (tilePreviewDuration.TotalMilliseconds / 6), repeat: () => true);
        }

        public void SetUnfocus()
        {
            _isFocused = false;
            this.AbortAnimation("Animation");
            this.ScaleTo(1, 334);
            _storyboardReader?.Dispose();
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

            canvas.Clear();

            var (bitmap, srcRect) = GetCurrentBitmap();
            if (bitmap == null)
                return;

            var borderColor = _isFocused ? FocusedColor : UnfocusedColor;

            using (var path = new SKPath())
            using (var roundRect = new SKRoundRect(info.Rect, 30, 30))
            using (var paint = new SKPaint
                {Color = borderColor, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3})
            {
                path.AddRoundRect(roundRect);
                canvas.ClipPath(path, antialias: true);
                canvas.DrawBitmap(bitmap, srcRect, info.Rect);
                canvas.DrawRoundRect(roundRect, paint);
            }
        }

        private async void LoadSkBitmap()
        {
            var path = ContentImg;
            var newBitmap = await Task.Run(() =>
            {
                using (var stream = new SKFileStream(path))
                {
                    return SKBitmap.Decode(stream);
                }
            });

            _contentBitmap?.Dispose();
            _contentBitmap = newBitmap;
            InvalidateSurface();
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
            else if (propertyName == "ContentTilePreviewPath")
            {
                _storyboardReader?.Dispose();
                _storyboardReader = new StoryboardReader(Path.Combine(Application.Current.DirectoryInfo.Resource,
                    ContentTilePreviewPath));
            }
        }
    }
}