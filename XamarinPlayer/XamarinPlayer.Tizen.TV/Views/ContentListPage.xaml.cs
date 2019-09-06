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
using System.IO;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinPlayer.Controls;
using XamarinPlayer.Services;
using XamarinPlayer.ViewModels;
using Application = Tizen.Applications.Application;

namespace XamarinPlayer.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ContentListPage : ContentPage, IContentPayloadHandler
    {
        NavigationPage AppMainPage;

        public ContentListPage(NavigationPage page)
        {
            InitializeComponent();

            AppMainPage = page;

            UpdateItem();

            NavigationPage.SetHasNavigationBar(this, false);
        }

        private Task ContentSelected(ContentItem item)
        {
            var playerView = new PlayerView
            {
                BindingContext = item.BindingContext
            };
            return AppMainPage.PushAsync(playerView);
        }

        private void UpdateItem()
        {
            foreach (var content in ((ContentListPageViewModel) BindingContext).ContentList)
            {
                var item = new ContentItem
                {
                    BindingContext = content
                };
                ContentListView.Add(item);
            }
        }

        private async Task UpdateContentInfo()
        {
            var focusedContent = ContentListView.FocusedContent;
            ContentTitle.Text = focusedContent.ContentTitle;
            ContentDesc.Text = focusedContent.ContentDescription;
            ContentImage.Source = ImageSource.FromStream(() => File.OpenRead(focusedContent.ContentImg));
            ContentImage.Opacity = 0;
            await ContentImage.FadeTo(1, 1000);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            MessagingCenter.Subscribe<IKeyEventSender, string>(this, "KeyDown", (s, e) =>
            {
                HandleKeyEvent(e);
            });
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
            if (e.Contains("Next") || e.Contains("Right"))
                return KeyCode.Next;
            if (e.Contains("Rewind") || e.Contains("Left"))
                return KeyCode.Previous;
            if (e.Contains("Return"))
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
                if (keyCode == KeyCode.Next) return ContentListView.ScrollToNext();
                if (keyCode == KeyCode.Previous) return ContentListView.ScrollToPrevious();
                throw new ArgumentOutOfRangeException(nameof(keyCode), keyCode, null);
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
            var contentList = ((ContentListPageViewModel) BindingContext).ContentList;
            var index = contentList.FindIndex(content => content.Source.Equals(url));
            if (index == -1)
                return false;
            var item = ContentListView.GetItem(index);
            ContentSelected(item);
            return true;
        }
    }
}