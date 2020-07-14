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
using System.Linq;
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
            _genGrid.ItemsSource = source;
            if (_genGrid.Items != null && _genGrid.Items.Count > 0)
            {
                _focusedItem = _genGrid.Items[0] as ContentItem;
            }
        }

        public void SetFocusedContent(DetailContentData contentData)
        {
            var index = (_genGrid.ItemsSource as IEnumerable<DetailContentData>).ToList().IndexOf(contentData);
            if (index == -1)
                return;
            var newContent = (ContentItem) _genGrid.Items[index];
            SwapFocusedContent(newContent);
        }

        private void SwapFocusedContent(ContentItem newContent)
        {
            _genGrid.ScrollTo(_genGrid.Items.IndexOf(newContent));
        }

        public void Subscribe()
        {
            _genGrid.FocusedItemChanged += OnFocusedItemChanged;
        }

        public void Unsubscribe()
        {
            _genGrid.FocusedItemChanged -= OnFocusedItemChanged;
        }
    }
}