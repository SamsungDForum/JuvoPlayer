/// @file ESDisplay.cs 
/// <published> N </published>
/// <privlevel> Non-privilege </privlevel>
/// <privilege> None </privilege>
/// <privacy> N </privacy>
/// <product> TV </product>
/// <version> 5.0.0 </version>
/// <SDK_Support> N </SDK_Support>
/// Copyright (c) 2018 Samsung Electronics Co., Ltd All Rights Reserved  
/// PROPRIETARY/CONFIDENTIAL  
/// This software is the confidential and proprietary  
/// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall  
/// not disclose such Confidential Information and shall use it only in  
/// accordance with the terms of the license agreement you entered into with  
/// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the  
/// suitability of the software, either express or implied, including but not  
/// limited to the implied warranties of merchantability, fitness for a  
/// particular purpose, or non-infringement. SAMSUNG shall not be liable for any  
/// damages suffered by licensee as a result of using, modifying or distributing  
/// this software or its derivatives.

using System;
using static Interop;

namespace Tizen.TV.Multimedia
{
    internal class ESDisplay
    {
        internal static readonly string LogTag = "Tizen.Multimedia.ESPlayer";
        private IDisplay display;

        internal ESDisplay(Tizen.NUI.Window window)
        {
            display = new EcodeDisplay(window);
        }

        internal ESDisplay(ElmSharp.Window window)
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
