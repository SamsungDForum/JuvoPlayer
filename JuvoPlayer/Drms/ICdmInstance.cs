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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.Drms
{
    public interface ICdmInstance
    {
        /// <summary>
        /// Awaitable call for packet decryption.
        /// </summary>
        /// <param name="packet">Encrypted packet to be decrypted</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Task which completes when initialization is done</returns>
        /// <exception cref="OperationCancelledException"> is throw when initialization is terminated before
        /// completion or when passed <see cref="token"/> gets cancelled.</exception>
        Task<Packet> DecryptPacket(EncryptedPacket packet, CancellationToken token);

        /// <summary>
        /// Gets existing or creates new matching session.
        /// </summary>
        /// <param name="data">Initialization data</param>
        /// <param name="keys">Collection of keys for the session</param>
        /// <param name="clipDrmConfigurations">List of drm descriptions</param>
        /// <returns>Matching (already existing or created) session.</returns>
        /// <exception cref="OperationCancelledException"> is throw when initialization is terminated before completion.</exception>
        Task<IDrmSession> GetDrmSession(DrmInitData data, IEnumerable<byte[]> keys, List<DrmDescription> clipDrmConfigurations);

        /// <summary>
        /// Awaitable call for closing session with given identifier.
        /// </summary>
        /// <param name="sessionId">Session identifier string</param>
        /// <exception cref="OperationCancelledException"> is throw when initialization is terminated before completion.</exception>
        void CloseSession(string sessionId);

        /// <summary>
        /// Awaitable call for completion of initialization of all existing sessions for the CDM.
        /// </summary>
        /// <param name="token">Cancellation token</param>
        /// <returns>Task which completes when initialization is done</returns>
        /// <exception cref="OperationCancelledException"> is throw when initialization is terminated before completion.</exception>
        Task WaitForAllSessionsInitializations(CancellationToken cancellationToken);
    }
}
