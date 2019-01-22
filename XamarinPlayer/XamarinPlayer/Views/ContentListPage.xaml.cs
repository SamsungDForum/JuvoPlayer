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
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinPlayer.Controls;
using XamarinPlayer.Services;
using XamarinPlayer.ViewModels;

namespace XamarinPlayer.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]

    public partial class ContentListPage : ContentPage, IContentPayloadHandler
    {
        NavigationPage AppMainPage;

        public static readonly BindableProperty FocusedContentProperty = BindableProperty.Create("FocusedContent", typeof(ContentItem), typeof(ContentListPage), default(ContentItem));
        public ContentItem FocusedContent
        {
            get
            {
                return (ContentItem)GetValue(FocusedContentProperty);
            }
            set
            {
                SetValue(FocusedContentProperty, value);
            }
        }

        public ContentListPage(NavigationPage page)
        {
            InitializeComponent();

            AppMainPage = page;

            UpdateItem();

            NavigationPage.SetHasNavigationBar(this, false);

            PropertyChanged += ContentChanged;
        }

        private void ContentChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("FocusedContent"))
            {
                UpdateContentInfo();
            }
        }

        private void ContentSelected(ContentItem item)
        {
            ContentListView.FocusedContent = item;
            var playerView = new PlayerView
            {
                BindingContext = item.BindingContext
            };
            AppMainPage.PushAsync(playerView);
        }

        private void UpdateItem()
        {

            foreach (var content in ((ContentListPageViewModel)BindingContext).ContentList)
            {
                var item = new ContentItem
                {
                    BindingContext = content
                };
                item.OnContentSelect += ContentSelected;
                ContentListView.Add(item);
            }


        }

        protected async void UpdateContentInfo()
        {
            ContentTitle.Text = FocusedContent.ContentTitle;
            ContentDesc.Text = FocusedContent.ContentDescription;

            ContentImage.Source = ImageSource.FromFile(FocusedContent.ContentImg);
            ContentImage.Opacity = 0;
            await ContentImage.FadeTo(1, 1000);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            ContentListView.SetFocus();
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