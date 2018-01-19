using JuvoPlayer.Common;
using System.Collections.Generic;
using System.Windows.Input;

namespace XamarinPlayer.Models
{
    class DetailContentData
    {
        public string Image
        {
            get { return Clip.Poster; }
            set { Clip.Poster = value; }
        }

        public string Bg
        {
            get { return Clip.Poster; }
            set { Clip.Poster = value; }
        }
        public string Source
        {
            get { return Clip.Url; }
            set { Clip.Url = value; }
        }
        public string Title
        {
            get { return Clip.Title; }
            set { Clip.Title = value; }
        }
        public string Description
        {
            get { return Clip.Description; }
            set { Clip.Description = value; }
        }

        public ICommand ContentFocusedCommand { get; set; }
        public ClipDefinition Clip { get; set; }

        public DetailContentData(ClipDefinition clip, ICommand focusedCommand)
        {
            this.Clip = clip;
            this.ContentFocusedCommand = focusedCommand;
        }
    }
}
