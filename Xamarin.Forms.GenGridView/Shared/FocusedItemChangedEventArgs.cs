namespace Xamarin.Forms.GenGridView
{
    public class FocusedItemChangedEventArgs : SelectedItemChangedEventArgs
    {
        public FocusedItemChangedEventArgs(object selectedItem) : base(selectedItem, -1)
        {
        }

        public FocusedItemChangedEventArgs(object selectedItem, int selectedItemIndex) : base(selectedItem, selectedItemIndex)
        {
        }
    }
}