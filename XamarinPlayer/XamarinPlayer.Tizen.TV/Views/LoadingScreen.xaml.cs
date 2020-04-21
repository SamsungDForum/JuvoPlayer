using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Tizen.Applications;
using Application = Tizen.Applications.Application;

namespace XamarinPlayer.Tizen.TV.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class LoadingScreen : ContentPage
    {
        public LoadingScreen()
        {
            InitializeComponent();
        }

        private void SKCanvasView_OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            SKBitmap bitmap;
            bitmap = SKBitmap.Decode(Path.Combine(Application.Current.DirectoryInfo.SharedResource,
                "JuvoPlayerXamarinTizenTV.png"));
            
            var info = e.Info;
            var rect = info.Rect;
            var surface = e.Surface;
            var canvas = surface.Canvas;
            
            canvas.DrawBitmap(bitmap, rect);
        }
    }
}