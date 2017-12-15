using JuvoPlayer.Common;
using System.Collections.Generic;
using System.Windows.Input;

namespace XamarinPlayer.Models
{
    class DetailContentData
    {
        private ClipDefinition clip;
        public string Image
        {
            get { return clip.Poster; }
            set { clip.Poster = value; }
        }

        public string Bg
        {
            get { return clip.Poster; }
            set { clip.Poster = value; }
        }
        public string Source
        {
            get { return clip.Url; }
            set { clip.Url = value; }
        }
        public string Title
        {
            get { return clip.Title; }
            set { clip.Title = value; }
        }
        public string Description
        {
            get { return clip.Description; }
            set { clip.Description = value; }
        }

        public ICommand ContentFocusedCommand { get; set; }

        public DetailContentData(ClipDefinition clip, ICommand focusedCommand)
        {
            this.clip = clip;
            this.ContentFocusedCommand = focusedCommand;
        }
    }
}
