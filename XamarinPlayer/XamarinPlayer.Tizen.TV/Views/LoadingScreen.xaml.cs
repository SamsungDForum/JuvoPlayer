using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            LoadingImage.Source = Path.Combine(Application.Current.DirectoryInfo.SharedResource, "JuvoPlayerXamarinTizenTV.png");
        }
    }
}