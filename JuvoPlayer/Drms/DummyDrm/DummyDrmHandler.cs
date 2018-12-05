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

using JuvoPlayer.Common;
using JuvoLogger;
using System;
using System.Linq;

namespace JuvoPlayer.Drms.DummyDrm
{
    public class DummyDrmHandler : IDrmHandler
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        public static readonly byte[] DummySystemId = {0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0,
                                                            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, };
        public static readonly string DummyScheme = "dummy";

        public DummyDrmHandler()
        {
        }

        public IDrmSession CreateDRMSession(DRMInitData initData, DRMDescription drmDescription)
        {
            return DummyDrmSession.Create();
        }

        public bool SupportsSystemId(byte[] uuid)
        {
            return uuid.SequenceEqual(DummySystemId);
        }

        public bool SupportsType(string type)
        {
            return string.Equals(type, DummyScheme, StringComparison.CurrentCultureIgnoreCase);
        }

        public string GetScheme(byte[] uuid)
        {
            return DummyScheme;
        }
    }
}
