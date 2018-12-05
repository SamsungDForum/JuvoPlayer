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

using System;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using Nito.AsyncEx;
using Tizen.Security.TEEC;
using Tizen.TV.Security.DrmDecrypt;
using JuvoPlayer.Common.Utils.IReferenceCountable;

namespace JuvoPlayer.Drms.DummyDrm
{
    internal class DummyDrmSession : IDrmSession
    {
        private enum DummyDrmCommands : uint
        {
            Init = 0,
            DeInit,
            Decrypt,
            Release
        };

        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly AsyncContextThread thread = new AsyncContextThread();

        private static readonly Guid guid = new Guid(0x60000001, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x3);
        private static readonly uint inputBufferSize = 1048576;

        private Context context;
        private Session session;
        private SharedMemory inputMemory;

        private int Counter;
        ref int IReferenceCoutable.Count => ref Counter;

        private DummyDrmSession()
        {
        }

        public void Dispose()
        {
            //Should be uncommented after Tizen.Net 4.0.1 release
            //inputSharedMemory?.Dispose();
            //session?.Dispose();
            context?.Dispose();
        }

        public static DummyDrmSession Create()
        {
            return new DummyDrmSession();
        }

        public Task Initialize()
        {
            return thread.Factory.Run(() => InitializeOnTEECThread());
        }

        private void InitializeOnTEECThread()
        {
            try
            {
                context = new Context(null);
                session = context.OpenSession(guid, LoginMethod.Public, null, null);
                session.InvokeCommand((uint)DummyDrmCommands.Init, null);
                inputMemory = context.AllocateSharedMemory(inputBufferSize, SharedMemoryFlags.Input);
            }
            catch (Exception e)
            {
                Logger.Error("Error: " + e.Message);
                throw;
            }
        }

        public Task<Packet> DecryptPacket(EncryptedPacket packet)
        {
            return thread.Factory.Run(() => DecryptPacketOnTEECThread(packet));
        }

        private unsafe Packet DecryptPacketOnTEECThread(EncryptedPacket packet)
        {
            if (packet.Data.Length > inputMemory.Size)
            {
                context.ReleaseSharedMemory(inputMemory);
                inputMemory = context.AllocateSharedMemory((uint)packet.Data.Length, SharedMemoryFlags.Input);
            }
            inputMemory.SetData(packet.Data, 0);

            var inputParameter = new RegisteredMemoryReference(inputMemory, (uint)packet.Data.Length, 0, TEFRegisteredMemoryType.PartialInput);
            var outputParameter = new Value(0, 0, TEFValueType.Output);
            Parameter[] parameters = { inputParameter, outputParameter };

            session.InvokeCommand((uint)DummyDrmCommands.Decrypt, parameters);

            var handle = new HandleSize { handle = outputParameter.A, size = (uint)packet.Data.Length };

            return new DecryptedEMEPacket(thread)
            {
                Dts = packet.Dts,
                Pts = packet.Pts,
                StreamType = packet.StreamType,
                IsEOS = packet.IsEOS,
                IsKeyFrame = packet.IsKeyFrame,
                HandleSize = handle
            };
        }
    }
}
