using System.Collections.Generic;
using System.Threading.Tasks;
using Xamarin.Forms.GenGridView;
using XamarinPlayer.Tizen.TV.Controls;
using XamarinPlayer.Tizen.TV.Models;

namespace XamarinPlayer.Tizen.TV.Controllers
{
    public interface IGenGridController
    {
        ContentItem FocusedItem { get; }
        GenGridView GenGrid { get; }
        void SetItemsSource(List<DetailContentData> source);
        Task<bool> ScrollToNext();
        Task<bool> ScrollToPrevious();
        Task SetFocusedContent(ContentItem contentItem);
    }
}