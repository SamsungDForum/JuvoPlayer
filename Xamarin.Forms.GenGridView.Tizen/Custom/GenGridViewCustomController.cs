using ElmSharp;

namespace Xamarin.Forms.GenGridView.Tizen
{
    public class GenGridViewCustomController : GenGridViewNativeController
    {
        public GenGridViewCustomController(GenGrid grid) : base(grid)
        {
        }

        public override void ScrollTo(int index, ScrollToPosition scrollToPosition, bool shouldAnimate)
        {
            base.ScrollTo(index, scrollToPosition, shouldAnimate);
            ItemFocused?.Invoke(this, new GenGridItemEventArgs {Item = Items[Position]});
        }

        public override void ScrollTo(View view, ScrollToPosition scrollToPosition, bool shouldAnimate)
        {
            base.ScrollTo(view, scrollToPosition, shouldAnimate);
            ItemFocused?.Invoke(this, new GenGridItemEventArgs {Item = Items[Position]});
        }
    }
}