using System.ComponentModel;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using XamarinMediaPlayer.ViewModels;
using XamarinMediaPlayer.Models;
using XamarinMediaPlayer.Controls;
using XamarinMediaPlayer.Services;

namespace XamarinMediaPlayer.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]

    public partial class ContentListPage : ContentPage
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
            var playerView = new PlayerView()
            {
                BindingContext = item.BindingContext
            };
            AppMainPage.PushAsync(playerView);
        }

        private void UpdateItem()
        {
            foreach (DetailContentData content in ((ContentListPageViewModel)BindingContext).ContentList)
            {
                ContentItem item = new ContentItem()
                {
                    BindingContext = content
                };
                item.OnContentSelect += new ContentSelectHandler(ContentSelected);
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
    }
}