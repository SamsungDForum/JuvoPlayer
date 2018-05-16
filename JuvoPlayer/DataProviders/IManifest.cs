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



using System;
using System.Threading.Tasks;
using MpdParser;
/// <summary>
/// IManifest is a generic manifest interface for all non static manifest
/// files (Dynamic DASH, Live HLS, etc.).
/// Interface exposes a notification mechanism by which a manifest can inform
/// listers that its content has changed.
/// </summary>
namespace JuvoPlayer.DataProviders
{
    /// <summary>
    /// Manifest Change handler. Called each time a manifest information changes.
    /// New items will be present at end of the list.
    /// </summary>
    /// <param name="newDocument"></param>
    public delegate void ManifestChanged(Object newDocument);

    public interface IManifest
    {
        /// <summary>
        /// Notification when manifest document changes. 
        /// </summary>
        event ManifestChanged ManifestChanged;

        /// <summary>
        /// Forces IManifest implementing class to reload Manifest with specified delay.
        /// Calling ReloadManifest while there is an existing reload in progress, may stall
        /// caller. To avoid this, use IsReloadInProgress to get current running status.
        /// 
        /// </summary>
        /// <param name="reloadTime">DateTime specifying when new Playback document is to be 
        /// downloaded. Speficying DateTime.Now will schedule it immediately.</param>
        /// <returns>True - scheduled. False - not scheduled</returns>
        bool ReloadManifest(DateTime reloadTime);

        /// <summary>
        /// Returns currently running Manifest reload activity. May be null if called
        /// before calling ReloadManifest
        /// </summary>
        Task GetReloadManifestActivity { get; }

        /// <summary>
        /// Returns current reload status.
        /// true - reload in progress.
        /// false - no reloads in progress.
        /// </summary>
        bool IsReloadInProgress { get; }
    }
}

