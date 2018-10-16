using ElmSharp;

namespace JuvoPlayer.Utils
{
    public class WindowUtils
    {
        public static readonly int DefaultWindowWidth = 1920;
        public static readonly int DefaultWindowHeight = 1080;

        public static Window CreateElmSharpWindow()
        {
            return CreateElmSharpWindow(DefaultWindowWidth, DefaultWindowHeight);
        }

        public static Window CreateElmSharpWindow(int width, int height)
        {
            var window = new Window("JuvoPlayer")
            {
                Geometry = new Rect(0, 0, width, height)
            };

            // Sample code calls following API:
            // skipping geometry settings
            //
            // window.Resize(width, height);
            // window.Realize(null);
            // window.Active();
            // window.Show();
            //
            // Does not seem to be necessary in case of Juvo/Xamarin
            //

            return window;
        }

        public static void DestroyElmSharpWindow(Window window)
        {
            window.Hide();
            window.Unrealize();
        }
    }
}