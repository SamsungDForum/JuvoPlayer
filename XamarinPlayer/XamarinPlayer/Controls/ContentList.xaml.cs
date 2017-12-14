using System.Linq;
using System.ComponentModel;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace XamarinMediaPlayer.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ContentList : ScrollView
    {
        public static readonly BindableProperty FocusedContentProperty = BindableProperty.Create("FocusedContent", typeof(ContentItem), typeof(ContentList), default(ContentItem));
        public ContentItem FocusedContent
        {
            get { return (ContentItem)GetValue(FocusedContentProperty); }
            set { SetValue(FocusedContentProperty, value); }
        }

        public ContentList()
        {
            InitializeComponent();

            PropertyChanged += ContentFocusedChanged;
        }

        public void Add(ContentItem item)
        {
            ContentLayout.Children.Add(item);
        }

        public bool SetFocus()
        {
            ContentItem item = ContentLayout.Children.First() as ContentItem;

            foreach (ContentItem child in ContentLayout.Children)
            {
                if (child == FocusedContent)
                {
                    item = child;
                    break;
                }
            }

            return item.SetFocus();
        }

        public void SetHeight(double height)
        {
            ContentItem item = ContentLayout.Children.First() as ContentItem;

            foreach (ContentItem child in ContentLayout.Children)
            {
                child.SetHeight(height);
            }
        }

        private void ContentFocusedChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("FocusedContent"))
            {
                UpdateItemState();
            }
        }

        private void UpdateItemState()
        {
            foreach (ContentItem child in ContentLayout.Children)
            {
                if (child != FocusedContent)
                {
                    child.SetUnfocus();
                }
            }
        }
    }
}