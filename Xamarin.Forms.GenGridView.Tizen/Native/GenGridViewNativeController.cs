using System;
using System.Collections;
using System.Collections.Generic;
using ElmSharp;
using Xamarin.Forms.Platform.Tizen;

namespace Xamarin.Forms.GenGridView.Tizen
{
    public class GenGridViewNativeController : IGenGridViewController
    {
        private readonly GenGrid _genGrid;
        protected readonly List<GenGridItem> Items;
        protected int Position;

        public EventHandler<GenGridItemEventArgs> ItemSelected { get; set; }

        public EventHandler<GenGridItemEventArgs> ItemFocused { get; set; }

        public GenGridViewNativeController(GenGrid grid)
        {
            _genGrid = grid;
            _genGrid.ItemFocused += OnItemFocused;
            _genGrid.ItemSelected += OnItemSelected;
            Items = new List<GenGridItem>();
        }

        private void OnItemSelected(object sender, GenGridItemEventArgs e)
        {
            ItemSelected?.Invoke(sender, e);
        }

        private void OnItemFocused(object sender, GenGridItemEventArgs e)
        {
            ItemFocused?.Invoke(sender, e);
        }

        public virtual void ScrollTo(View view, ScrollToPosition scrollToPosition, bool shouldAnimate)
        {
            Position = Items.FindIndex(x => x.Data == view);
            _genGrid.ScrollTo(Items[Position], scrollToPosition.ToNative(), shouldAnimate);
        }

        public virtual void ScrollTo(int index, ScrollToPosition scrollToPosition, bool shouldAnimate)
        {
            Position = index;
            _genGrid.ScrollTo(Items[Position], scrollToPosition.ToNative(), shouldAnimate);
        }

        private readonly GenItemClass _genItem = new GenItemClass("default")
        {
            GetContentHandler = (obj, part) =>
            {
                if (part == "elm.swallow.icon")
                {
                    return Platform.Tizen.Platform.GetOrCreateRenderer((View) obj).NativeView;
                }

                return null;
            }
        };

        public void Reset()
        {
            _genGrid.Clear();
            Items.Clear();
        }

        public void Add(IEnumerable templatedItems)
        {
            foreach (var item in templatedItems)
            {
                Items.Add(_genGrid.Append(_genItem, item));
            }
        }
        
        public void Remove(IEnumerable templatedItems)
        {
            foreach (var item in templatedItems)
            {
                var index = Items.FindIndex(x => x.Data == item);
                Items[index].Delete();
                Items.RemoveAt(index);
            }
        }

        public void Dispose()
        {
            Items.Clear();
            _genGrid.Clear();
            _genGrid.ItemFocused -= OnItemFocused;
            _genGrid.ItemSelected -= OnItemSelected;
        }
    }
}