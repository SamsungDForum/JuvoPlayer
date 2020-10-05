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

namespace JuvoPlayer.Common
{
    public readonly struct PlatformCapabilities
    {
        private readonly Func<string, bool> _supportsKeySystem;

        public bool SupportsSeamlessAudioChange { get; }
        public bool SupportsDrms { get; }
        public bool Supports8K { get; }

        public PlatformCapabilities(
            bool supportsSeamlessAudioChange,
            bool supportsDrms,
            bool supports8K,
            Func<string, bool> supportsKeySystem)
        {
            SupportsSeamlessAudioChange = supportsSeamlessAudioChange;
            SupportsDrms = supportsDrms;
            Supports8K = supports8K;
            _supportsKeySystem = supportsKeySystem;
        }

        public bool SupportsKeySystem(string keySystem)
        {
            return _supportsKeySystem.Invoke(keySystem);
        }
    }
}
