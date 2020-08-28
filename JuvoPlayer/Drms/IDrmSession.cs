/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Utils.IReferenceCountable;

namespace JuvoPlayer.Drms
{
    /// <summary>Represents a single DRM Session.</summary>
    public interface IDrmSession : IReferenceCountable
    {
        /// <summary>Initializes a new instance of the <see cref="T:JuvoPlayer.Drms.IDRMSession"></see> class.</summary>
        /// <returns>A task, which will complete when Session initialization finishes.</returns>
        /// <exception cref="T:JuvoPlayer.Drms.DRMException">Session couldn't be initialized.</exception>
        Task Initialize();

        /// <summary>
        /// Awaitable call for initialization completion.
        /// </summary>
        /// <param name="token">Cancellation token</param>
        /// <returns>Task which completes when initialization is done</returns>
        /// <exception cref="OperationCancelledException"> is throw when initialization is terminated before
        /// completion or when passed <see cref="token"/> gets cancelled.</exception>
        /// <exception cref=""></exception>
        /// <exception cref="T:JuvoPlayer.Drms.DRMException"> is thrown when session could not be initialized</exception>
        /// <exception cref="InvalidOperationException"> is thrown if this API is called prior to calling Initialize</exception>
        Task<bool> WaitForInitialization(CancellationToken token);

        /// <summary>
        /// Sets session id
        /// </summary>
        /// <param name="sessionId">Session id</param>
        void SetSessionId(string sessionId);

        /// <summary>
        /// Sets session's licence installation status as installed
        /// </summary>
        void SetLicenceInstalled();

        /// <summary>
        /// Returns DRM initialization data
        /// </summary>
        /// <returns>DRM initialization data</returns>
        DrmInitData GetDrmInitData();

        /// <summary>
        /// Returns DRM description
        /// </summary>
        /// <returns>DRM description</returns>
        DrmDescription GetDrmDescription();

        /// <summary>
        /// Returns session id
        /// </summary>
        /// <returns>Session id</returns>
        string GetSessionId();

        /// <summary>
        /// Returns collection of keys
        /// </summary>
        /// <returns>Collection of keys</returns>
        IEnumerable<byte[]> GetKeys();

        /// <summary>
        /// Returns initialization state of session
        /// </summary>
        /// <returns>True - License installed. False - No license</returns>
        bool IsInitialized();

        /// <summary>
        /// Returns CDM Instance associated with this session
        /// </summary>
        /// <returns>Returns session's CDM Instance</returns>
        ICdmInstance CdmInstance { get; }
    }
}
