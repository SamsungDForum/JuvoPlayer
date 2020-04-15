using System;
using System.Collections;
using ElmSharp;

namespace Xamarin.Forms.GenGridView.Tizen
{
    public interface IGenGridViewController : IDisposable
    {
        void ScrollTo(View view, ScrollToPosition scrollToPosition, bool shouldAnimate);
        void ScrollTo(int index, ScrollToPosition scrollToPosition, bool shouldAnimate);
        void Remove(IEnumerable templatedItems);
        void Add(IEnumerable templatedItems);
        void Reset();
        EventHandler<GenGridItemEventArgs> ItemFocused { get; set; }
        EventHandler<GenGridItemEventArgs> ItemSelected { get; set; }
    }
}