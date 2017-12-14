using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using Xamarin.Forms;
using XamarinMediaPlayer.Services;

namespace XamarinMediaPlayer.Controls
{
    public partial class ContentItem : AbsoluteLayout
    {
        public static readonly BindableProperty ContentImgProperty = BindableProperty.Create("ContentImg", typeof(string), typeof(ContentItem), default(ICollection<string>));
        public String ContentImg
        {
            set { SetValue(ContentImgProperty, value); }
            get { return (string)GetValue(ContentImgProperty); }
        }

        public static readonly BindableProperty ContentTitleProperty = BindableProperty.Create("ContentTitle", typeof(string), typeof(ContentItem), default(string));
        public string ContentTitle
        {
            set { SetValue(ContentTitleProperty, value); }
            get { return (string)GetValue(ContentTitleProperty); }
        }

        public static readonly BindableProperty ContentDescriptionProperty = BindableProperty.Create("ContentDescription", typeof(string), typeof(ContentItem), default(string));
        public string ContentDescription
        {
            set { SetValue(ContentDescriptionProperty, value); }
            get { return (string)GetValue(ContentDescriptionProperty); }
        }

        public static readonly BindableProperty ContentFocusedCommandProperty = BindableProperty.Create("ContentFocusedCommand", typeof(ICommand), typeof(ContentItem), default(ICommand));
        public ICommand ContentFocusedCommand
        {
            set { SetValue(ContentFocusedCommandProperty, value); }
            get { return (ICommand)GetValue(ContentFocusedCommandProperty); }
        }

        public ContentSelectHandler OnContentSelect;

        public enum ItemState
        {
            Unfocused,
            Focused,
            Selected
        }

        public ItemState State;
        public double _height;

        public ContentItem()
        {
            InitializeComponent();

            SetItemState(ItemState.Unfocused);
            _height = 0;

            PropertyChanged += ContentPropertyChanged;
        }

        public bool SetFocus()
        {
            ContentFocusedCommand?.Execute(this);
            return FocusArea.Focus();
        }

        public void SetUnfocus()
        {
            SetItemState(ItemState.Unfocused);
        }

        public void SetHeight(double height)
        {
            _height = height;
            HeightRequest = _height;
            WidthRequest = _height * 1.8;
        }

        private void SetItemState(ItemState st)
        {
            switch (st)
            {
                case ItemState.Focused:
                    ImageBorder.BackgroundColor = Color.FromRgb(234, 234, 234);
                    Dim.Color = Color.FromRgba(0, 0, 0, 0);
                    PlayImage.Opacity = 0;
                    break;
                case ItemState.Unfocused:
                    ImageBorder.BackgroundColor = Color.FromRgb(32, 32, 32);
                    Dim.Color = Color.FromRgba(0, 0, 0, 64);
                    PlayImage.Opacity = 0;
                    this.ScaleTo(1, 334);
                    break;
                case ItemState.Selected:
                    ImageBorder.BackgroundColor = Color.FromRgb(234, 234, 234);
                    Dim.Color = Color.FromRgba(0, 0, 0, 192);
                    PlayImage.Opacity = 1;
                    this.ScaleTo(0.9, 250);
                    break;
                default:
                    return;
            }
            State = st;
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (width == -1 || height == -1)
                return;

            if (_height == 0)
                WidthRequest = height * 1.8;
        }

        private void OnItemClicked(object sender, EventArgs e)
        {
            if (State == ItemState.Selected)
            {
                OnContentSelect(this);
            }
            else
            {
                ContentFocusedCommand?.Execute(this);

                SetItemState(ItemState.Selected);
            }
        }

        private void OnItemFocused(object sender, FocusEventArgs e)
        {
            SetItemState(ItemState.Focused);
        }

        private void OnItemUnfocused(object sender, FocusEventArgs e)
        {
            SetItemState(ItemState.Unfocused);
        }

        private void ContentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("ContentImg"))
            {
                ContentImage.Source = ContentImg;
            }
        }
    }
}