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

using JuvoPlayer.Common;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JuvoPlayer.UI
{
    public delegate void ChangeRepresentation(int pid);
    public delegate void Pause();
    public delegate void Play();
    public delegate void Seek(double time);
    public delegate void SetExternalSubtitles(string path);
    public delegate void ShowClip(ClipDefinition clip);

    public class UIController
    {
        public event ChangeRepresentation ChangeRepresentation;
        public event Pause Pause;
        public event Play Play;
        public event Seek Seek;
        public event SetExternalSubtitles SetExternalSubtitles;
        public event ShowClip ShowClip;

        private View myView;
        private TextLabel myText;

        public void Initialize()
        {
            Window.Instance.BackgroundColor = Color.Transparent; // !!important

            //Create a View instance and add it to the stage
            myView = new View();
            myView.Size2D = new Size2D(300, 200);
            myView.BackgroundColor = Color.Red;
            myView.Position = new Position(810, 440, 0);
            //Subscribe Key Event
            myView.Focusable = true;
            myView.KeyEvent += KeyEvent;

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

        private bool KeyEvent(object source, View.KeyEventArgs e)
        {
            if (e.Key.State == Key.StateType.Down)
            {
                if (e.Key.KeyPressedName == "Right")
                {
                    myText.TextColor = Color.White;

                    ClipDefinition clip1 = new ClipDefinition()
                    {
                        Type = "RTP",
                        Url = "test"

                    };

                    ShowClip(clip1);
                }
                else if (e.Key.KeyPressedName == "Left")
                {
                    myText.TextColor = Color.Black;

                    ClipDefinition clip2 = new ClipDefinition()
                    {
                        Type = "RTP",
                        Url = "test"

                    };

                    ShowClip(clip2);
                }
            }

            return false;
        }

        public void OnBufferingCompleted()
        {

        }

        public void OnRenderSubtitle(Subtitle subtitle)
        {

        }

        public void OnSetClips(List<ClipDefinition> clips)
        {

        }

        public void OnTimeUpdated(double time)
        {

        }
    }
}
