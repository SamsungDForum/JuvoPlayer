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
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Utils.IReferenceCountableExtensions;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinPlayer.Tizen.TV.Models;
using XamarinPlayer.Tizen.TV.Services;
using XamarinPlayer.Tizen.TV.ViewModels;
using XamarinPlayer.Views;
using XamarinPlayer.Tizen.TV.Controllers;

namespace XamarinPlayer.Tizen.TV.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ContentListPage : IContentPayloadHandler, ISuspendable
    {
        public static readonly BindableProperty ContentDataListProperty =
            BindableProperty.Create(
                propertyName: "ContentDataList",
                returnType: typeof(object),
                typeof(ContentListPage),
                defaultValue: false,
                defaultBindingMode: BindingMode.OneWay,
                propertyChanged: (bindable, oldValue, newValue) =>
                {
                    var page = ((ContentListPage) bindable);
                    if (newValue != null)
                    {
                        page._contentGridController.SetItemsSource((List<DetailContentData>) newValue);
                        page.UpdateContentInfo();
                        page._contentListLoaded.SetResult(true);
                    }
                });

        public object ContentDataList
        {
            set { SetValue(ContentDataListProperty, value); }
            get { return GetValue(ContentDataListProperty); }
        }

        public static readonly BindableProperty FocusedContentProperty =
            BindableProperty.Create(
                propertyName: "ContentList",
                returnType: typeof(object),
                typeof(ContentListPage),
                defaultValue: false,
                defaultBindingMode: BindingMode.OneWay,
                propertyChanged: (bindable, oldValue, newValue) =>
                {
                    var page = ((ContentListPage) bindable);
                    if (newValue != null)
                        page._contentGridController.SetFocusedContent((DetailContentData) newValue);
                });

        public object FocusedContent
        {
            set { SetValue(FocusedContentProperty, value); }
            get { return GetValue(FocusedContentProperty); }
        }

        private readonly NavigationPage _appMainPage;

        private int _pendingUpdatesCount;
        private readonly SKBitmapCache _skBitmapCache;
        private SKBitmapRefCounted _backgroundBitmap;
        private IContentGridController _contentGridController;
        private TaskCompletionSource<bool> _contentListLoaded = new TaskCompletionSource<bool>();

#if DEBUG
        void OnHotReloaded()
        {
            _contentGridController?.Unsubscribe();
            _contentGridController = new ContentGridController(ContentGrid);
            _contentGridController.SetItemsSource(ContentDataList as List<DetailContentData>);
            _contentGridController.Subscribe();
            _contentGridController.SetFocusedContent(FocusedContent as DetailContentData);
            UpdateContentInfo();
        }
#endif

        public ContentListPage(NavigationPage page)
        {
            InitializeComponent();

            _appMainPage = page;
            _contentGridController = new ContentGridController(ContentGrid);
            SetBinding(ContentDataListProperty, new Binding(nameof(ContentListPageViewModel.ContentList)));
            SetBinding(FocusedContentProperty, new Binding(nameof(ContentListPageViewModel.CurrentContent)));

            var cacheService = DependencyService.Get<ISKBitmapCacheService>();
            _skBitmapCache = cacheService.GetCache();

            NavigationPage.SetHasNavigationBar(this, false);
        }

        private Task ContentSelected(DetailContentData data)
        {
            var playerView = new PlayerView
            {
                BindingContext = new PlayerViewModel(data, new DialogService())
            };
            return _appMainPage.PushAsync(playerView);
        }

        private async void UpdateContentInfo()
        {
            ++_pendingUpdatesCount;
            await Task.Delay(TimeSpan.FromSeconds(1));
            --_pendingUpdatesCount;
            if (_pendingUpdatesCount > 0 || FocusedContent == null) return;

            ContentTitle.Text = (FocusedContent as DetailContentData)?.Title;
            ContentDesc.Text = (FocusedContent as DetailContentData)?.Description;

            _backgroundBitmap?.Release();
            _backgroundBitmap = null;
            if (_contentGridController.FocusedItem != null)
                _backgroundBitmap = await _skBitmapCache.GetBitmap(_contentGridController.FocusedItem.ContentImg);

            ContentImage.InvalidateSurface();
            ContentImage.Opacity = 0;
            ContentImage.AbortAnimation("FadeTo");
            await ContentImage.FadeTo(0.75);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _contentGridController.Subscribe();
            MessagingCenter.Subscribe<IKeyEventSender, string>(this, "KeyDown", (s, e) => { HandleKeyEvent(e); });
            MessagingCenter.Subscribe<IEventSender, string>(this, "Pop",
                async (s, e) =>
                {
                    await _appMainPage.PopAsync();
                    Application.Current.Quit();
                });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            MessagingCenter.Unsubscribe<IKeyEventSender, string>(this, "KeyDown");
            MessagingCenter.Unsubscribe<IEventSender, string>(this, "Pop");
            _contentGridController.Unsubscribe();
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
            if (ContentDataList == null || !Application.Current.MainPage.IsEnabled) return;
            var keyCode = ConvertToKeyCode(e);
            if (IsScrollEvent(keyCode))
                HandleScrollEvent(keyCode);
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

        private void HandleScrollEvent(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.Next:
                    (BindingContext as ContentListPageViewModel)?.NextCommand.Execute(null);
                    break;
                case KeyCode.Previous:
                    (BindingContext as ContentListPageViewModel)?.PreviousCommand.Execute(null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(keyCode), keyCode, null);
            }

            UpdateContentInfo();
        }

        private Task HandleEnterEvent()
        {
            return ContentSelected(FocusedContent as DetailContentData);
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (width == -1 || height == -1)
                return;

            // FIXME: Workaround for Tizen
            // Sometimes height of list is calculated as wrong
            // Set the height explicitly for fixing this issue
            ContentGrid.HeightRequest = (height * 0.21);
        }

        public async Task<bool> HandleUrl(string url)
        {
            await _contentListLoaded.Task;
            var contentList = (List<DetailContentData>) ContentDataList;
            var data = contentList?.Find(content => content.Source.Equals(url));
            if (data == null)
                return false;

            (BindingContext as ContentListPageViewModel)?.DeactivateCommand.Execute(null);
            _contentGridController.SetFocusedContent(data);
            SelectContent(data);
            return true;
        }

        private async void SelectContent(DetailContentData data)
        {
            UpdateContentInfo();
            await ContentSelected(data);
            (BindingContext as ContentListPageViewModel)?.ActivateCommand.Execute(null);
        }

        public void Suspend()
        {
            _contentGridController.FocusedItem?.ResetFocus();
        }

        public void Resume()
        {
            _contentGridController.FocusedItem?.SetFocus();
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