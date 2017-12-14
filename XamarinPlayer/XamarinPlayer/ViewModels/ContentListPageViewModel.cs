using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

using Xamarin.Forms;

using XamarinMediaPlayer.Models;
using XamarinMediaPlayer.Controls;

namespace XamarinMediaPlayer.ViewModels
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

            ContentList = new List<DetailContentData>();
            DetailContentData content;

            content = new DetailContentData();
            content.Title = "Big Buck Bunny";
            content.Description = "Big Buck Bunny is a comedy about a well-tempered rabbit Big Buck, who finds his day spoiled by the rude actions of the forest bullies, three rodents.In the typical 1950s cartoon tradition, BigBuck then prepares for the rodents in a comical revenge.";
            content.Image = "img_1_b.jpg";
            content.Bg = "img_1_a.jpg";
            content.Source = "https://goo.gl/ZVKLVF";
            content.ContentFocusedCommand = ContentFocusedCommand;
            ContentList.Add(content);

            content = new DetailContentData();
            content.Title = "Monkaa";
            content.Description = "Monkaa is a blue-furred, pink-faced monkey who consumes a crystallized meteorite, making him invincibly strong and too hot to handle. Exploring his new superpowers, Monkaa zooms through an entire universe.";
            content.Image = "img_3_b.jpg";
            content.Bg = "img_3_a.jpg";
            content.Source = "https://goo.gl/kJZq2i";
            content.ContentFocusedCommand = ContentFocusedCommand;
            ContentList.Add(content);

            content = new DetailContentData();
            content.Title = "Caminandes: Llamigos";
            content.Description = "In this episode of the Caminandes cartoon series we learn to know our hero Koro even better! It's winter in Patagonia, food is getting scarce. Koro the Llama engages with Oti the pesky penguin in an epic fight over that last tasty berry.";
            content.Image = "img_2_b.jpg";
            content.Bg = "img_2_a.jpg";
            content.Source = "https://goo.gl/uMXgLH";
            content.ContentFocusedCommand = ContentFocusedCommand;
            ContentList.Add(content);

            content = new DetailContentData();
            content.Title = "Caminandes: Gran Dillama";
            content.Description = "Some llamas have no luck. Inspired by the good old Chuck Jones cartoons, this series of shorts follows the misadventures of one particularly persistent llama - Koro - as he hunts for the good life on the other side of the street. Or fence.";
            content.Image = "img_7_b.jpg";
            content.Bg = "img_7_a.jpg";
            content.Source = "https://goo.gl/ndrjju";
            content.ContentFocusedCommand = ContentFocusedCommand;
            ContentList.Add(content);

            content = new DetailContentData();
            content.Title = "Sintel";
            content.Description = "The film follows a girl named Sintel who is searching for a baby dragon she calls Scales. A flashback reveals that Sintel found Scales with its wing injured and helped care for it, forming a close bond with it. By the time its wing recovered and it was able to fly, Scales was caught by an adult dragon.";
            content.Image = "img_4_b.jpg";
            content.Bg = "img_4_a.jpg";
            content.Source = "https://goo.gl/FnjkmB";
            content.ContentFocusedCommand = ContentFocusedCommand;
            ContentList.Add(content);
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
