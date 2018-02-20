using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tizen.TV.Security.DrmDecrypt.emeCDM;

namespace JuvoPlayer.DRM.Cenc
{
    class EmeStatusConverter
    {

        public static JuvoPlayer.DRM.ErrorCode Convert(Status status)
        {
            switch (status)
            {
                case Status.kSuccess:
                    return ErrorCode.Success;
                case Status.kNeedsDeviceCertificate:
                    return ErrorCode.NeedsDeviceCertificate;
                case Status.kSessionNotFound:
                    return ErrorCode.SessionNotFound;
                case Status.kDecryptError:
                    return ErrorCode.DecryptError;
                case Status.kNoKey:
                    return ErrorCode.NoKey;
                case Status.kTypeError:
                    return ErrorCode.TypeError;
                case Status.kNotSupported:
                    return ErrorCode.NotSupported;
                case Status.kInvalidState:
                    return ErrorCode.InvalidState;
                case Status.kQuotaExceeded:
                    return ErrorCode.QuotaExceeded;
                case Status.kInvalidHandle:
                    return ErrorCode.InvalidHandle;
                case Status.kRangeError:
                    return ErrorCode.RangeError;
                case Status.kSupported:
                    return ErrorCode.Generic;
                case Status.kUnexpectedError:
                    return ErrorCode.Generic;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }
    }
}
