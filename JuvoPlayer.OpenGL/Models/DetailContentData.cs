using System.Windows.Input;

namespace JuvoPlayer.OpenGL.Services
{
    class DetailContentData
    {
        public string Image { get; set; }
        public string Bg { get; set; }
        public string Source { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public ICommand ContentFocusedCommand { get; set; }
        public object Clip { get; set; }
    }
}