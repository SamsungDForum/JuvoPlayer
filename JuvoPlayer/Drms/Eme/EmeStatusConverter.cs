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
using Tizen.TV.Security.DrmDecrypt.emeCDM;

namespace JuvoPlayer.Drms
{
    internal class EmeStatusConverter
    {
        public static string Convert(Status status)
        {
            switch (status)
            {
                case Status.kSuccess:
                    return ErrorMessage.Success;
                case Status.kNeedsDeviceCertificate:
                    return ErrorMessage.NeedsDeviceCertificate;
                case Status.kSessionNotFound:
                    return ErrorMessage.SessionNotFound;
                case Status.kDecryptError:
                    return ErrorMessage.DecryptError;
                case Status.kNoKey:
                    return ErrorMessage.NoKey;
                case Status.kTypeError:
                    return ErrorMessage.TypeError;
                case Status.kNotSupported:
                    return ErrorMessage.NotSupported;
                case Status.kInvalidState:
                    return ErrorMessage.InvalidState;
                case Status.kQuotaExceeded:
                    return ErrorMessage.QuotaExceeded;
                case Status.kInvalidHandle:
                    return ErrorMessage.InvalidHandle;
                case Status.kRangeError:
                    return ErrorMessage.RangeError;
                case Status.kSupported:
                    return ErrorMessage.Generic;
                case Status.kUnexpectedError:
                    return ErrorMessage.Generic;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }
    }
}
