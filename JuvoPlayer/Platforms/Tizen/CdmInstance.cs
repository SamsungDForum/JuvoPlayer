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
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using Tizen.TV.Security.DrmDecrypt;
using Tizen.TV.Security.DrmDecrypt.emeCDM;
using emeInitDataType = Tizen.TV.Security.DrmDecrypt.emeCDM.InitDataType;
using emeMessageType = Tizen.TV.Security.DrmDecrypt.emeCDM.MessageType;
using MessageType = JuvoPlayer.Drms.MessageType;

namespace JuvoPlayer.Platforms.Tizen
{
    public static class ConversionExtensions
    {
        public static emeInitDataType ToEmeInitDataType(this DrmInitDataType drmInitDataType)
        {
            switch (drmInitDataType)
            {
                case DrmInitDataType.Cenc:
                    return emeInitDataType.kCenc;
                case DrmInitDataType.KeyIds:
                    return emeInitDataType.kKeyIds;
                case DrmInitDataType.WebM:
                    return emeInitDataType.kWebM;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(drmInitDataType),
                        drmInitDataType,
                        null);
            }
        }

        public static string ToEmeBinaryData(this byte[] bytes)
        {
            return Encoding.GetEncoding(437).GetString(bytes);
        }

        public static byte[] ToJuvoBinaryData(this string bytesString)
        {
            return Encoding.GetEncoding(437).GetBytes(bytesString);
        }

        public static MessageType ToJuvoMessageType(this emeMessageType messageType)
        {
            switch (messageType)
            {
                case emeMessageType.kLicenseRequest:
                    return MessageType.LicenseRequest;
                case emeMessageType.kLicenseRenewal:
                    return MessageType.LicenseRenewal;
                case emeMessageType.kLicenseRelease:
                    return MessageType.LicenseRelease;
                case emeMessageType.kIndividualizationRequest:
                    return MessageType.IndividualizationRequest;
                case emeMessageType.kLicenseAlreadyDone:
                    return MessageType.AlreadyDone;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(messageType),
                        messageType,
                        null);
            }
        }

        public static DrmException ToJuvoDrmException(this Status status)
        {
            switch (status)
            {
                case Status.kSuccess:
                    break;
                case Status.kSupported:
                    break;
                case Status.kNeedsDeviceCertificate:
                    return new DrmException("Device Certificate is missing.");
                case Status.kSessionNotFound:
                    return new DrmException("Given session ID is invalid.");
                case Status.kDecryptError:
                    return new DecryptException("Failed to decrypt.");
                case Status.kNoKey:
                    return new NoKeyException("No key present in the given session.");
                case Status.kTypeError:
                    return new DrmException("Data type error.");
                case Status.kNotSupported:
                    return new DrmException("Data type error.");
                case Status.kInvalidState:
                    return new DrmException("Invalid state.");
                case Status.kQuotaExceeded:
                    return new DrmException("Quota exceeded.");
                case Status.kInvalidHandle:
                    return new DrmException("Invalid handle.");
                case Status.kRangeError:
                    return new DrmException("Range error.");
                case Status.kUnexpectedError:
                    return new DrmException("Unexpected error.");
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(status),
                        status,
                        null);
            }

            return null;
        }

        public static DrmException ToJuvoDrmException(this eCDMReturnType eCdmReturnType)
        {
            switch (eCdmReturnType)
            {
                case eCDMReturnType.E_ERROR:
                    return new DecryptException("Unknown error.");
                case eCDMReturnType.E_NEED_KEY:
                    return new NoKeyException();
                case eCDMReturnType.E_SUCCESS:
                    return null;
                case eCDMReturnType.E_MAX:
                    throw new ArgumentOutOfRangeException(
                        nameof(eCdmReturnType),
                        eCdmReturnType,
                        null);
            }

            // Additional error code returned by drm_decrypt api when
            // there is no space in TrustZone
            const int eDecryptBufferFull = 2;
            if ((int) eCdmReturnType == eDecryptBufferFull)
                throw new DecryptBufferFullException();

            throw new ArgumentOutOfRangeException(
                nameof(eCdmReturnType),
                eCdmReturnType,
                null);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct MsdSubsampleInfo
    {
        public uint uBytesOfClearData;
        public uint uBytesOfEncryptedData;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct MsdFmp4Data
    {
        public uint uSubSampleCount;
        public IntPtr pSubSampleInfo;
    }

    public class CdmInstance : IEventListener, ICdmInstance
    {
        private readonly IEME _ieme;
        private readonly string _keySystem;
        private readonly Subject<Message> _messageSubject;
        private readonly Subject<Unit> _keyStatusChanged;
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public CdmInstance(string keySystem)
        {
            _logger.Info($"{keySystem}");
            _ieme = IEME.create(
                this,
                keySystem,
                false,
                CDM_MODEL.E_CDM_MODEL_DEFAULT);
            _keySystem = keySystem;
            _messageSubject = new Subject<Message>();
            _keyStatusChanged = new Subject<Unit>();
        }

        public override void Dispose()
        {
            _logger.Info();
            base.Dispose();
            _ieme.Dispose();
            _messageSubject.Dispose();
            _keyStatusChanged.Dispose();
        }

        public string CreateSession()
        {
            _logger.Info();
            string sessionId = null;
            var status = _ieme.session_create(
                SessionType.kTemporary,
                ref sessionId);
            _logger.Info($"{sessionId}");
            ThrowOnError(status);
            return sessionId;
        }

        public void GenerateRequest(
            string sessionId,
            DrmInitDataType drmInitDataType,
            byte[] initData)
        {
            _logger.Info($"{sessionId} {drmInitDataType} {initData.Length}");
            var status = _ieme.session_generateRequest(
                sessionId,
                drmInitDataType.ToEmeInitDataType(),
                initData.ToEmeBinaryData());
            _logger.Info($"{sessionId} {status}");
            ThrowOnError(status);
        }

        public void UpdateSession(
            string sessionId,
            byte[] sessionData)
        {
            _logger.Info($"{sessionId}");
            var status = _ieme.session_update(
                sessionId,
                sessionData.ToEmeBinaryData());
            _logger.Info($"{status}");
            ThrowOnError(status);
            _keyStatusChanged.OnNext(Unit.Default);
        }

        public void CloseSession(string sessionId)
        {
            var status = _ieme.session_close(sessionId);
            ThrowOnError(status);
        }

        public Packet Decrypt(EncryptedPacket packet)
        {
            // Do padding only for Widevine with keys shorter than 16 bytes
            var iv = packet.Iv;
            if (_keySystem == "com.widevine.alpha" && iv.Length < 16)
                Array.Resize(ref iv, 16);

            var handleArray = new HandleSize[1];
            var result = CreateParamsAndDecryptData(
                packet,
                ref handleArray);
            ThrowOnError(result);
            return new DecryptedPacket
            {
                Dts = packet.Dts,
                Pts = packet.Pts,
                StreamType = packet.StreamType,
                IsKeyFrame = packet.IsKeyFrame,
                Duration = packet.Duration,
                Handle = handleArray[0]
            };
        }

        private unsafe eCDMReturnType CreateParamsAndDecryptData(
            EncryptedPacket packet,
            ref HandleSize[] handleArray)
        {
            switch (packet.Storage)
            {
                case IManagedDataStorage managedStorage:
                    fixed (byte* data = managedStorage.Data)
                        return CreateParamsAndDecryptData(
                            packet,
                            data,
                            managedStorage.Data.Length,
                            ref handleArray);
                case INativeDataStorage nativeStorage:
                    return CreateParamsAndDecryptData(
                        packet,
                        nativeStorage.Data,
                        nativeStorage.Length,
                        ref handleArray);
                default:
                    throw new DrmException(
                        $"Unsupported packet storage: {packet.Storage?.GetType()}");
            }
        }

        private unsafe eCDMReturnType CreateParamsAndDecryptData(
            EncryptedPacket packet,
            byte* data,
            int dataLen,
            ref HandleSize[] handleArray)
        {
            fixed (byte* iv = packet.Iv, kId = packet.KeyId)
            {
                var subdataPointer = MarshalSubsampleArray(packet);
                var subsampleInfoPointer = ((MsdFmp4Data*) subdataPointer.ToPointer())->pSubSampleInfo;
                try
                {
                    var param = new sMsdCipherParam
                    {
                        algorithm = eMsdCipherAlgorithm.MSD_AES128_CTR,
                        format = eMsdMediaFormat.MSD_FORMAT_FMP4,
                        pdata = data,
                        udatalen = (uint) dataLen,
                        piv = iv,
                        uivlen = (uint) packet.Iv.Length,
                        pkid = kId,
                        ukidlen = (uint) packet.KeyId.Length,
                        psubdata = subdataPointer
                    };
                    return DecryptData(
                        new[] {param},
                        ref handleArray);
                }
                finally
                {
                    if (subsampleInfoPointer != IntPtr.Zero)
                        Marshal.FreeHGlobal(subsampleInfoPointer);
                    Marshal.FreeHGlobal(subdataPointer);
                }
            }
        }

        private static unsafe IntPtr MarshalSubsampleArray(EncryptedPacket packet)
        {
            var subsamplePointer = IntPtr.Zero;
            if (packet.SubSamples != null)
            {
                var subsamples = packet.SubSamples.Select(o =>
                    new MsdSubsampleInfo
                    {
                        uBytesOfClearData = o.ClearData,
                        uBytesOfEncryptedData = o.EncData
                    }).ToArray();
                var totalSize = Marshal.SizeOf(typeof(MsdSubsampleInfo)) * subsamples.Length;
                var array = Marshal.AllocHGlobal(totalSize);
                var pointer = (MsdSubsampleInfo*) array.ToPointer();
                for (var i = 0; i < subsamples.Length; i++)
                    pointer[i] = subsamples[i];
                subsamplePointer = (IntPtr) pointer;
            }

            var subData = new MsdFmp4Data
            {
                uSubSampleCount = (uint) (packet.SubSamples?.Length ?? 0),
                pSubSampleInfo = subsamplePointer
            };
            var subdataPointer = Marshal.AllocHGlobal(Marshal.SizeOf(subData));
            Marshal.StructureToPtr(subData, subdataPointer, false);
            return subdataPointer;
        }

        private eCDMReturnType DecryptData(
            sMsdCipherParam[] param,
            ref HandleSize[] handleArray)
        {
            var decryptor = _ieme.getDecryptor();
            var result = API.EmeDecryptarray(
                (eCDMReturnType) decryptor,
                ref param,
                param.Length,
                IntPtr.Zero,
                0,
                ref handleArray);
            return result;
        }

        public IObservable<Message> OnSessionMessage()
        {
            return _messageSubject.AsObservable();
        }

        public IObservable<Unit> OnKeyStatusChanged()
        {
            return _keyStatusChanged.AsObservable();
        }

        public override void onKeyStatusesChange(string sessionId)
        {
        }

        public override void onRemoveComplete(string sessionId)
        {
        }

        public override void onMessage(
            string sessionId,
            emeMessageType messageType,
            string message)
        {
            _logger.Info($"{sessionId} {messageType}");

            var juvoMessage = new Message(
                sessionId,
                messageType.ToJuvoMessageType(),
                message.ToJuvoBinaryData());

            _messageSubject.OnNext(juvoMessage);
        }

        private void ThrowOnError(Status status)
        {
            var exception = status.ToJuvoDrmException();
            if (exception != null)
            {
                _logger.Error($"{exception}");
                throw exception;
            }
        }

        private void ThrowOnError(eCDMReturnType result)
        {
            var exception = result.ToJuvoDrmException();
            if (exception != null)
            {
                _logger.Error($"{exception}");
                throw exception;
            }
        }
    }
}