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
﻿using System.ComponentModel;
using System.Linq;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace XamarinPlayer.Controls
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

        public ContentItem GetItem(int index)
        {
            ContentItem item = ContentLayout.Children.ElementAt(index) as ContentItem;
            return item;
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