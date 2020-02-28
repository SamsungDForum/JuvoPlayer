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
using System.Linq;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Utils.IReferenceCountableExtensions;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinPlayer.Tizen.TV.Controls;
using XamarinPlayer.Tizen.TV.Models;
using XamarinPlayer.Tizen.TV.Services;
using XamarinPlayer.Tizen.TV.ViewModels;
using XamarinPlayer.Views;

namespace XamarinPlayer.Tizen.TV.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ContentListPage : IContentPayloadHandler, ISuspendable
    {
        private readonly NavigationPage _appMainPage;

        private int _pendingUpdatesCount;
        private readonly SKBitmapCache _skBitmapCache;
        private SKBitmapRefCounted _backgroundBitmap;

        public ContentListPage(NavigationPage page)
        {
            InitializeComponent();

            _appMainPage = page;

            UpdateItem();

            var cacheService = DependencyService.Get<ISKBitmapCacheService>();
            _skBitmapCache = cacheService.GetCache();

            NavigationPage.SetHasNavigationBar(this, false);
        }

        private Task ContentSelected(BindableObject item)
        {
            var playerView = new PlayerView
            {
                BindingContext = new PlayerViewModel(item.BindingContext as DetailContentData)
            };
            return _appMainPage.PushAsync(playerView);
        }

        private void UpdateItem()
        {
            foreach (var item in ((ContentListPageViewModel) BindingContext).ContentList.Select(content => new ContentItem
            {
                BindingContext = content
            }))
            {
                ContentListView.Add(item);
            }
        }

        private async Task UpdateContentInfo()
        {
            var focusedContent = ContentListView.FocusedContent;
            ++_pendingUpdatesCount;
            await Task.Delay(TimeSpan.FromSeconds(1));
            --_pendingUpdatesCount;
            if (_pendingUpdatesCount > 0) return;

            ContentTitle.Text = focusedContent.ContentTitle;
            ContentDesc.Text = focusedContent.ContentDescription;

            _backgroundBitmap?.Release();
            _backgroundBitmap = null;
            _backgroundBitmap = await _skBitmapCache.GetBitmap(focusedContent.ContentImg);

            ContentImage.InvalidateSurface();
            ContentImage.Opacity = 0;
            ContentImage.AbortAnimation("FadeTo");
            await ContentImage.FadeTo(0.75);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            MessagingCenter.Subscribe<IKeyEventSender, string>(this, "KeyDown", (s, e) => { HandleKeyEvent(e); });
            ContentListView.SetFocus();
            await UpdateContentInfo();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            MessagingCenter.Unsubscribe<IKeyEventSender, string>(this, "KeyDown");
        }

        private enum KeyCode
        {
            Unknown,
            Enter,
            Next,
            Previous
        }

        private async void HandleKeyEvent(string e)
        {
            var keyCode = ConvertToKeyCode(e);
            if (IsScrollEvent(keyCode))
                await HandleScrollEvent(keyCode);
            else if (keyCode == KeyCode.Enter)
                await HandleEnterEvent();
        }

        private static KeyCode ConvertToKeyCode(string e)
        {
            if (e.Contains("Right"))
                return KeyCode.Next;
            if (e.Contains("Left"))
                return KeyCode.Previous;
            if (e.Contains("Return") || e.Contains("Play"))
                return KeyCode.Enter;
            return KeyCode.Unknown;
        }

        private static bool IsScrollEvent(KeyCode code)
        {
            return code == KeyCode.Next || code == KeyCode.Previous;
        }

        private async Task HandleScrollEvent(KeyCode keyCode)
        {
            Task<bool> ScrollTask()
            {
                switch (keyCode)
                {
                    case KeyCode.Next:
                        return ContentListView.ScrollToNext();
                    case KeyCode.Previous:
                        return ContentListView.ScrollToPrevious();
                    default:
                        throw new ArgumentOutOfRangeException(nameof(keyCode), keyCode, null);
                }
            }

            var listScrolled = await ScrollTask();
            if (listScrolled)
                await UpdateContentInfo();
        }

        private Task HandleEnterEvent()
        {
            return ContentSelected(ContentListView.FocusedContent);
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (width == -1 || height == -1)
                return;

            // FIXME: Workaround for Tizen
            // Sometimes height of list is calculated as wrong
            // Set the height explicitly for fixing this issue
            ContentListView.SetHeight(height * 0.21);
        }

        public bool HandleUrl(string url)
        {
            var contentListPageViewModel = (ContentListPageViewModel) BindingContext;
            var contentList = contentListPageViewModel.ContentList;
            var index = contentList.FindIndex(content => content.Source.Equals(url));
            if (index == -1)
                return false;
            contentListPageViewModel.IsBusy = true;
            var item = ContentListView.GetItem(index);
            ContentListView.SetFocusedContent(item).ContinueWith(async _ =>
            {
                await UpdateContentInfo();
                await ContentSelected(ContentListView.FocusedContent);
                contentListPageViewModel.IsBusy = false;
            }, TaskScheduler.FromCurrentSynchronizationContext());

            return true;
        }

        public void Suspend()
        {
            ContentListView.ResetFocus();
        }

        public void Resume()
        {
            ContentListView.SetFocus();
        }

        private void SKCanvasView_OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            if (_backgroundBitmap == null) return;

            var info = e.Info;
            var rect = info.Rect;
            var surface = e.Surface;
            var canvas = surface.Canvas;

            using (var paint = new SKPaint())
            {
                paint.Shader = SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.Top),
                    new SKPoint(rect.Left, rect.Bottom),
                    new[] {SKColors.Empty, SKColors.Black},
                    new[] {0.6F, 0.8F},
                    SKShaderTileMode.Repeat);
                canvas.DrawBitmap(_backgroundBitmap.Value, rect);
                canvas.DrawRect(rect, paint);
            }
        }
    }
}