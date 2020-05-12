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
            if (GenGrid.Items != null && GenGrid.Items.Count > 0)
            {
                _focusedItem = GenGrid.Items[0] as ContentItem;
            }
        }

        private void SwapFocusedContent(ContentItem newContent)
        {
            if (_focusedItem == newContent)
                return;
            _focusedItem = newContent;
            GenGrid.ScrollTo(_genGrid.Items.IndexOf(_focusedItem));
        }
        
        public bool ScrollToNext()
        {
            int index = _genGrid.Items.IndexOf(_focusedItem);
            if (index >= _genGrid.Items.Count - 1) return false;
            var item = (ContentItem) GenGrid.Items[index + 1];
            GenGrid.ScrollTo(item);
            return true;
        }

        public bool ScrollToPrevious()
        {
            int index = _genGrid.Items.IndexOf(_focusedItem);
            if (index <= 0) 
                return false;
            var item = (ContentItem) GenGrid.Items[index - 1];
            GenGrid.ScrollTo(item);
            return true;
        }

        public Task SetFocusedContent(ContentItem contentItem)   
        {
            SwapFocusedContent(contentItem);
            return Task.CompletedTask;
        }

        public void Subscribe()
        {
            GenGrid.FocusedItemChanged += OnFocusedItemChanged;
        }

        public void Unsubscribe()
        {
            GenGrid.FocusedItemChanged -= OnFocusedItemChanged;
        }
    }
}