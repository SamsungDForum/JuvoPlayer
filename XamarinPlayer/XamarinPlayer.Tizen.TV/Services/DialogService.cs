using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace XamarinPlayer.Tizen.TV.Services
{
    public class DialogService : IDialogService
    {
        public async Task ShowError(string message,
            string title,
            string buttonText,
            Action afterHideCallback)
        {
            Application.Current.MainPage.IsEnabled = false;
            await Application.Current.MainPage.DisplayAlert(
                title,
                message,
                buttonText);
            Application.Current.MainPage.IsEnabled = true;
            if (afterHideCallback != null)
            {
                afterHideCallback();
            }
        }
    }
}