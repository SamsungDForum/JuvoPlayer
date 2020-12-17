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

using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.Drms
{
    public interface IDrmManager
    {
        /// <summary>
        /// Sets new DrmDescription
        /// </summary>
        /// <param name="drmDescription">New DrmDescription</param>
        Task UpdateDrmConfiguration(DrmDescription drmDescription);

        /// <summary>
        /// Clears and closes all created CDM Instances
        /// </summary>
        void Clear();

        /// <summary>
        /// Returns Task&lt;IDrmSession&gt; for given DRM initialization data
        /// </summary>
        /// <returns>Task&lt;IDrmSession&gt; for given DRM initialization data</returns>
        /// <param name="data">DrmInitData identifying session</param>
        Task<IDrmSession> GetDrmSession(DrmInitData data);

        /// <summary>
        /// Returns ICdmInstance for given DRM initialization data
        /// </summary>
        /// <returns>ICdmInstance for given DRM initialization data</returns>
        /// <param name="data">DrmInitData identifying session</param>
        ICdmInstance GetCdmInstance(DrmInitData data);
    }
}
