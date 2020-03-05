using System;
using System.Threading.Tasks;

namespace XamarinPlayer.Tizen.TV.Services
{
    public interface IDialogService
    {
        Task ShowError(string message, string title, string buttonText, Action afterHideCallback);
    }
}