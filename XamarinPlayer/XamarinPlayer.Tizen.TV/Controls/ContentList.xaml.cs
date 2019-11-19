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

using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinPlayer.Tizen.TV.Controls;

namespace XamarinPlayer.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ContentList : ScrollView
    {
        public ContentItem FocusedContent { get; set; }

        public ContentList()
        {
            InitializeComponent();
        }

        public void Add(ContentItem item)
        {
            ContentLayout.Children.Add(item);
        }

        public ContentItem GetItem(int index)
        {
            return ContentLayout.Children.ElementAt(index) as ContentItem;
        }

        public void SetFocus()
        {
            if (FocusedContent == null)
                FocusedContent = (ContentItem) ContentLayout.Children.First();
            FocusedContent.SetFocus();
        }

        public async Task<bool> ScrollToNext()
        {
            var index = ContentLayout.Children.IndexOf(FocusedContent);
            var nextIndex = index + 1;
            if (nextIndex == ContentLayout.Children.Count)
                return false;

            await SwapFocusedContent(ContentLayout.Children[nextIndex] as ContentItem);
            return true;
        }

        public async Task<bool> ScrollToPrevious()
        {
            var index = ContentLayout.Children.IndexOf(FocusedContent);
            var prevIndex = index - 1;
            if (prevIndex == -1)
                return false;
            await SwapFocusedContent(ContentLayout.Children[prevIndex] as ContentItem);
            return true;
        }

        private Task SwapFocusedContent(ContentItem newContent)
        {
            FocusedContent.SetUnfocus();
            FocusedContent = newContent;
            FocusedContent.SetFocus();
            return ScrollToAsync(FocusedContent, ScrollToPosition.Center, true);
        }

        public void SetHeight(double height)
        {
            foreach (var view in ContentLayout.Children)
            {
                var child = (ContentItem) view;
                child.SetHeight(height);
            }
        }
    }
}