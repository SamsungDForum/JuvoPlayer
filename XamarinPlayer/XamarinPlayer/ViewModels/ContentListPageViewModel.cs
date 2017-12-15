using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

using Xamarin.Forms;

using XamarinPlayer.Models;
using XamarinPlayer.Controls;
using JuvoPlayer.Common;
using System.Linq;
using System.IO;
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
            ICommand ContentFocusedCommand = CreateFocusedCommand();

            var applicationPath = DependencyService.Get<IPathService>(DependencyFetchTarget.NewInstance).ApplicationPath;
            var clipsPath = Path.Combine(applicationPath, "shared", "res", "videoclips.json");
            ContentList = JSONFileReader.DeserializeJsonFile<List<ClipDefinition>>(clipsPath).Select(o => new DetailContentData(o , CreateFocusedCommand())).ToList();
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
