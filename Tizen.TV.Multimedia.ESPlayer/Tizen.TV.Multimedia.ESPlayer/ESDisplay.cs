using System;
using System.Collections.Generic;
using System.Text;
using static Interop;

namespace Tizen.TV.Multimedia.ESPlayer
{
    public class EsDisplay
    {
        internal static readonly string LogTag = "Tizen.Multimedia.ESPlayer";
        private IDisplay display;
       
        public EsDisplay(Tizen.NUI.Window window)
        {
            display = new EcodeDisplay(window);
        }

        public EsDisplay(ElmSharp.Window window)
        {
            display = new EvasDisplay(window);
        }

        internal bool SetDisplay(IntPtr player)
        {
            return display.SetDisplay(player);
        }

        internal interface IDisplay
        {
            bool SetDisplay(IntPtr player);
        }

        internal class EcodeDisplay : IDisplay
        {
            private readonly Tizen.NUI.Window window;

            internal EcodeDisplay(Tizen.NUI.Window window)
            {
                this.window = window;
            }

            bool IDisplay.SetDisplay(IntPtr player)
            {
                Log.Info(LogTag, "start");
                return true;
                /*
                var window = this.window.GetNativeWindowHandle();
                return NativeESPlusPlayer.SetDisplay(player, DisplayType.Overlay, window, 0, 0, 1920, 1080);
                */
            }
        }

        internal class EvasDisplay : IDisplay
        {
            private readonly ElmSharp.Window window;

            internal EvasDisplay(ElmSharp.Window window)
            {
                this.window = window;
            }

            bool IDisplay.SetDisplay(IntPtr player)
            {
                Log.Info(LogTag, "start");
                return NativeESPlusPlayer.SetDisplay(player, DisplayType.Overlay, window);
            }
        }
    }
}
