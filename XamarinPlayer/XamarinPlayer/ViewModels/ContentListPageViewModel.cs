using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Xamarin.Forms;
using XamarinPlayer.Controls;
using XamarinPlayer.Models;
using XamarinPlayer.Services;

namespace XamarinPlayer.ViewModels
{
    class ContentListPageViewModel : INotifyPropertyChanged
    {
        public List<DetailContentData> ContentList { get; set; }
        public ContentItem FocusedContent { get; set; }
        public ICommand ContentFocusedCommand
        {
            protected set;
            get;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ContentListPageViewModel()
        {
            var applicationPath = DependencyService.Get<IPathService>(DependencyFetchTarget.NewInstance).ApplicationPath;
            var clipsPath = Path.Combine(applicationPath, "shared", "res", "videoclips.json");
            var clips = DependencyService.Get<IClipReaderService>(DependencyFetchTarget.NewInstance).ReadClips(clipsPath);

            ContentList = clips.Select(o => new DetailContentData()
            {
                Bg = o.Image,
                Clip = o.ClipDetailsHandle,
                ContentFocusedCommand = CreateFocusedCommand(),
                Description = o.Description,
                Image = o.Image,
                Source = o.Source,
                Title = o.Title,
            }).ToList();
        }

        protected ICommand CreateFocusedCommand()
        {
            ICommand command = new Command<ContentItem>((item) =>
            {
                FocusedContent = item;
                OnPropertyChanged("FocusedContent");
            });

            return command;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
