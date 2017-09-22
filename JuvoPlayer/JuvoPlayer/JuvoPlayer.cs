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
using JuvoPlayer.Player;
using JuvoPlayer.RTSP;
using JuvoPlayer.UI;
using Tizen;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.TV.NUI;

namespace JuvoPlayer
{
    internal class JuvoPlayer : TVUIApplication
    {
        private UIController uiController;
        private DataProviderFactoryManager dataProviders;

        private IDataProvider dataProvider;
        private IPlayerController playerController;

        private void Initialize()
        {
            dataProviders = new DataProviderFactoryManager();
            dataProviders.RegisterDataProviderFactory(new RTPDataProviderFactory());

            uiController = new UIController();
            uiController.ShowClip += OnShowClip;

            //TODO(p.galiszewsk)
            var playerAdapter = new MultimediaPlayerAdapter();
            playerController = new PlayerController(playerAdapter);
            uiController.ChangeRepresentation += playerController.ChangeRepresentation; //TODO(p.galiszewsk): is it in proper place
            uiController.Pause += playerController.OnPause;
            uiController.Play += playerController.OnPlay;
            uiController.Seek += playerController.OnSeek;
            uiController.SetExternalSubtitles += playerController.OnSetExternalSubtitles;

            uiController.Initialize();
        }

        private void OnShowClip(ClipDefinition clip)
        {
            dataProvider = dataProviders.CreateDataProvider(clip);
            dataProvider.DRMDataFound += playerController.OnDrmDataFound;
            dataProvider.StreamConfigReady += playerController.OnStreamConfigReady;
            dataProvider.StreamPacketReady += playerController.OnStreamPacketReady;
            dataProvider.StreamsFound += playerController.OnStreamsFound;
            dataProvider.Start();
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            Initialize();
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