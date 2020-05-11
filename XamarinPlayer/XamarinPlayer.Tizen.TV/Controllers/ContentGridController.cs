/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Xamarin.Forms.GenGridView;
using XamarinPlayer.Tizen.TV.Controls;
using XamarinPlayer.Tizen.TV.Models;

namespace XamarinPlayer.Tizen.TV.Controllers
{
    public class ContentGridController : IContentGridController
    {
        private ContentItem _focusedItem;
        private readonly GenGridView _genGrid;

        public ContentItem FocusedItem => _focusedItem;

        public GenGridView GenGrid => _genGrid;

        public ContentGridController(GenGridView genGrid)
        {
            _genGrid = genGrid;
            GenGrid.FocusedItemChanged += OnFocusedItemChanged;
            GenGrid.ItemsChanged += OnItemsChanged;
        }

        private void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if ((e.OldItems == null || e.OldItems.Count == 0) && e.NewItems != null &&
                e.NewItems.Count > 0)
            {
                _focusedItem = GenGrid.Items[0] as ContentItem;
            }
        }

        private void OnFocusedItemChanged(object sender, FocusedItemChangedEventArgs e)
        {
            _focusedItem?.ResetFocus();
            _focusedItem = e.SelectedItem as ContentItem;
            _focusedItem.SetFocus();
        }


        public void SetItemsSource(List<DetailContentData> source)
        {
            GenGrid.ItemsSource = source;
        }

        public Task SetFocusedContent(DetailContentData contentData)
        {
            var index = IndexOf(_genGrid.ItemsSource as IEnumerable<DetailContentData>, contentData);
            if (index == -1)
                return Task.CompletedTask;
            var newContent = (ContentItem) _genGrid.Items[index];
            SwapFocusedContent(newContent);
            return Task.CompletedTask;
        }

        public static int IndexOf<T>(IEnumerable<T> source, T value)
        {
            int index = 0;
            var comparer = EqualityComparer<T>.Default; // or pass in as a parameter
            foreach (T item in source)
            {
                if (comparer.Equals(item, value)) return index;
                index++;
            }

            return -1;
        }

        private void SwapFocusedContent(ContentItem newContent)
        {
            GenGrid.ScrollTo(_genGrid.Items.IndexOf(newContent));
        }

        ~ContentGridController()
        {
            GenGrid.FocusedItemChanged -= OnFocusedItemChanged;
            GenGrid.ItemsChanged -= OnItemsChanged;
        }
    }
}