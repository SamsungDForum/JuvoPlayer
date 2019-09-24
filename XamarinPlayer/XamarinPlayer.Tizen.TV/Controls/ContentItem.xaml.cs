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
using System.ComponentModel;
using Xamarin.Forms;
using XamarinPlayer.Services;

namespace XamarinPlayer.Controls
{
    public partial class ContentItem : AbsoluteLayout
    {
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

        public ContentSelectHandler OnContentSelect;

        private enum ItemState
        {
            Unfocused,
            Focused,
            Selected
        }

        private double _height;

        public ContentItem()
        {
            InitializeComponent();

            SetItemState(ItemState.Unfocused);

            _height = 0;

            PropertyChanged += ContentPropertyChanged;
        }

        public void SetFocus()
        {
            SetItemState(ItemState.Focused);
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
                    this.ScaleTo(0.9);
                    break;
                default:
                    return;
            }
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

        private void ContentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("ContentImg"))
            {
                ContentImage.Source = ContentImg;
            }
        }
    }
}