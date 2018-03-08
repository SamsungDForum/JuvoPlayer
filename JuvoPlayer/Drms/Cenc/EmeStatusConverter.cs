using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tizen.TV.Security.DrmDecrypt.emeCDM;

namespace JuvoPlayer.Drms.Cenc
{
    class EmeStatusConverter
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
