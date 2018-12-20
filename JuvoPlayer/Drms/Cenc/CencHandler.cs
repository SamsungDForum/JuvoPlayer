/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
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

using System.Linq;
using System.Text;
using JuvoPlayer.Common;
using JuvoLogger;
using Tizen.TV.Security.DrmDecrypt.emeCDM;

namespace JuvoPlayer.Drms.Cenc
{
    public class CencHandler : IDrmHandler
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public CencHandler()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public IDrmSession CreateDRMSession(DRMInitData initData, DRMDescription drmDescription)
        {
            var iemeKeySystemName = CencUtils.GetKeySystemName(initData.SystemId);
            if (IEME.isKeySystemSupported(iemeKeySystemName) != Status.kSupported)
            {
                Logger.Warn($"Key System: {iemeKeySystemName} is not supported");
                return null;
            }
            return CencSession.Create(initData, drmDescription);
        }

        public bool SupportsSystemId(byte[] uuid)
        {
            if (!CencUtils.SupportsSystemId(uuid))
                return false;

            var iemeKeySystemName = CencUtils.GetKeySystemName(uuid);
            return IEME.isKeySystemSupported(iemeKeySystemName) == Status.kSupported;
        }

        public bool SupportsType(string type)
        {
            return CencUtils.SupportsType(type);
        }

        public string GetScheme(byte[] uuid)
        {
            return CencUtils.GetScheme(uuid);
        }
    }
}
