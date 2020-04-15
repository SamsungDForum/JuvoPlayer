using ElmSharp;
using Xamarin.Forms.Platform.Tizen;
using ESize = ElmSharp.Size;

namespace Xamarin.Forms.GenGridView.Tizen
{
    public class GenGridItemsLayoutManager : IGenGridLayoutManager
    {
        private readonly Xamarin.Forms.GenGridView.GenGridView _genGridView;
        private readonly GenGrid _genGrid;

        public GenGridItemsLayoutManager(GenGrid genGrid, Xamarin.Forms.GenGridView.GenGridView genGridView)
        {
            _genGridView = genGridView;
            _genGrid = genGrid;
        }

        private ESize AllocatedSize => _genGrid.Geometry.Size;
        private int ItemWidthConstraint => AllocatedSize.Width;
        private int ItemHeightConstraint => AllocatedSize.Height;

        private ESize MeasureItem(int widthConstraint, int heightConstraint)
        {
            if (_genGridView?.Items?.Count == 0 || _genGridView?.Items?[0] == null)
            {
                return new ESize(0, 0);
            }

            return _genGridView.Items[0].Measure(Forms.ConvertToScaledDP(widthConstraint),
                Forms.ConvertToScaledDP(heightConstraint), MeasureFlags.IncludeMargins).Request.ToPixel();
        }

        public void LayoutItems()
        {
            LayoutItems(ItemWidthConstraint, ItemHeightConstraint);
        }

        public void LayoutItems(int widthConstraint, int heightConstraint)
        {
            var size = MeasureItem(widthConstraint, heightConstraint);
            if (size.Width <= 0) size.Width = ItemWidthConstraint;
            if (size.Height <= 0) size.Height = ItemHeightConstraint;
            _genGrid.ItemWidth = size.Width;
            _genGrid.ItemHeight = size.Height;
        }
    }
}