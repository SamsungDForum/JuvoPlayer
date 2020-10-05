/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
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
 *
 */

using System;
using System.Runtime.InteropServices;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using JuvoPlayer.Players;
using Tizen.TV.Security.DrmDecrypt.emeCDM;
using Tizen.TV.System.Config;

namespace JuvoPlayer.Platforms.Tizen
{
    public class PlatformTizen : Platform
    {
        private PlatformTizen(PlatformCapabilities capabilities) : base(capabilities)
        {
        }

        public static void Init()
        {
            var supportsDrms = RuntimeInformation.ProcessArchitecture == Architecture.Arm;
            var supportsKeySystem = supportsDrms
                    ? (Func<string, bool>) (keySystem => IEME.isKeySystemSupported(keySystem) == Status.kSupported)
                    : _ => false;

            var width = system_info.GetInt(SystemInfoKeye.SystemInfoKeyPanelResolutionWidth);
            var height = system_info.GetInt(SystemInfoKeye.SystemInfoKeyPanelResolutionHeight);
            var supports8K = width == 7680 && height == 4320;

            var capabilities = new PlatformCapabilities(
                false,
                supportsDrms,
                supports8K,
                supportsKeySystem);

            Current = new PlatformTizen(capabilities);
        }

        public override IPlatformPlayer CreatePlatformPlayer()
        {
            return new EsPlatformPlayer();
        }

        public override ICdmInstance CreateCdmInstance(string keySystem)
        {
            return new CdmInstance(keySystem);
        }
    }
}