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
            await Application.Current.MainPage.DisplayAlert(
                title,
                message,
                buttonText);
            if (afterHideCallback != null)
            {
                afterHideCallback();
            }
        }
    }
}