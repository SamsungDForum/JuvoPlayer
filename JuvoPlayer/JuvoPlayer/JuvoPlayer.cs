// Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL 
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

using Tizen;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.TV.NUI;

namespace JuvoPlayer
{
    internal class JuvoPlayer : TVUIApplication
    {
        private View myView;
        private TextLabel myText;

        protected override void OnCreate()
        {
            base.OnCreate();

            //Create a View instance and add it to the stage
            myView = new View();
            myView.Size2D = new Size2D(300, 200);
            myView.BackgroundColor = Color.Red;
            myView.Position = new Position(810, 440, 0);
            //Subscribe Key Event
            myView.Focusable = true;
            myView.KeyEvent += MyView_KeyEvent;

            Window.Instance.GetDefaultLayer().Add(myView);

            //Create a child view and add it to the parent view.
            myText = new TextLabel("Hello World")
            {
                Position = new Position(40, 80, 0),
                TextColor = Color.Black,
                PointSize = 40
            };

            myView.Add(myText);

            FocusManager.Instance.SetCurrentFocusView(myView);
        }

        private bool MyView_KeyEvent(object source, View.KeyEventArgs e)
        {
            if (e.Key.State == Key.StateType.Down)
            {
                if (e.Key.KeyPressedName == "Right")
                {
                    myText.TextColor = Color.White;
                }
                else if (e.Key.KeyPressedName == "Left")
                {
                    myText.TextColor = Color.Black;
                }
            }

            return false;
        }

        protected override void OnPause()
        {
            //This function is called when the window's visibility is changed from visible to invisible.
            base.OnPause();
        }

        protected override void OnResume()
        {
            //This function is called when the window's visibility is changed from invisible to visible.
            base.OnResume();
        }

        protected override void OnTerminate()
        {
            //This function is called when the app exit normally.
            base.OnTerminate();
        }

        protected override void OnLowMemory(LowMemoryEventArgs e)
        {
            //This function is called when the system is low on memory.
            base.OnLowMemory(e);
        }

        protected override void OnLocaleChanged(LocaleChangedEventArgs e)
        {
            //This function is called when the language is changed.
            base.OnLocaleChanged(e);
        }

        private static void Main(string[] args)
        {
            //Create an Application
            JuvoPlayer juvoPlayer = new JuvoPlayer();
            juvoPlayer.Run(args);
        }
    }
}