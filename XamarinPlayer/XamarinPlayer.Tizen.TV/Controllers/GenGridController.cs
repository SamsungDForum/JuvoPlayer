using System.Collections.Generic;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.GenGridView;
using XamarinPlayer.Tizen.TV.Controls;
using XamarinPlayer.Tizen.TV.Models;

namespace XamarinPlayer.Tizen.TV.Controllers
{
    public class GenGridController : IGenGridController
    {
        private ContentItem _focusedItem;
        private readonly GenGridView _genGrid;

        public ContentItem FocusedItem => _focusedItem;

        public GenGridView GenGrid => _genGrid;

        public GenGridController(GenGridView genGrid)
        {
            _genGrid = genGrid;
            GenGrid.FocusedItemChanged += (sender, args) =>
            {
                _focusedItem?.ResetFocus();
                _focusedItem = args.SelectedItem as ContentItem;
                _focusedItem.SetFocus();
            };
            GenGrid.ItemsChanged += (sender, args) =>
            {
                if ((args.OldItems == null || args.OldItems.Count == 0) && args.NewItems != null &&
                    args.NewItems.Count > 0)
                {
                    _focusedItem = GenGrid.Items[0] as ContentItem;
                }
            };
        }
        
        public void SetItemsSource(List<DetailContentData> source)
        {
            GenGrid.ItemsSource = source;
        }

        private Task SwapFocusedContent(ContentItem newContent)
        {
            if (_focusedItem == newContent)
                return Task.CompletedTask;
            //_focusedItem?.ResetFocus();
            _focusedItem = newContent;
            ///_focusedItem.SetFocus();
            GenGrid.ScrollTo(_genGrid.Items.IndexOf(_focusedItem), ScrollToPosition.Center, true);
            return Task.CompletedTask;
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
            if (index <= 0) return false;
            var item = (ContentItem) GenGrid.Items[index - 1];
            GenGrid.ScrollTo(item);
            return true;
        }

        public Task SetFocusedContent(ContentItem contentItem)
        {
            return SwapFocusedContent(contentItem);
        }
    }
}