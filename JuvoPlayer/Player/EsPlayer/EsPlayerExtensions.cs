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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Tizen.TV.Multimedia;

namespace JuvoPlayer.Player.EsPlayer
{
    internal struct NativePacket
    {
        public StreamType type;
        public IntPtr buffer;
        public uint bufferSize;
        public ulong pts;
        public ulong duration;
        public IntPtr matroskaColorInfo;
        public uint hdr10pMetadataSize;
        public IntPtr hdr10pMetadata;
    }

    static class EsPlayerExtensions
    {
        private static bool _resolvedIsWorkaroundNeeded;
        private static bool _isWorkaroundNeeded;
        private static Func<ESPlayer, IntPtr> _nativePlayerFieldGetter;
        private static Func<IntPtr, IntPtr, SubmitStatus> _submitPacketMethodCaller;

        public static void Init()
        {
            IsWorkaroundNeeded();
        }

        public static bool IsWorkaroundNeeded()
        {
            if (!_resolvedIsWorkaroundNeeded)
            {
                ResolveIsWorkaroundNeeded();
                _resolvedIsWorkaroundNeeded = true;
                if (_isWorkaroundNeeded)
                    LoadDelegates();
            }

            return _isWorkaroundNeeded;
        }

        private static void ResolveIsWorkaroundNeeded()
        {
            if (RuntimeInformation.ProcessArchitecture != Architecture.X86)
                return;
            var playerType = typeof(ESPlayer);
            var nativePacketType = playerType.Assembly.GetType("Tizen.TV.Multimedia.NativePacket");
            if (nativePacketType == null)
                return;
            if (!(nativePacketType.GetField("matroskaColorInfo") != null
                  && nativePacketType.GetField("hdr10pMetadataSize") == null
                  && nativePacketType.GetField("hdr10pMetadata") == null))
                return;
            _isWorkaroundNeeded = true;
        }

        private static void LoadDelegates()
        {
            var playerType = typeof(ESPlayer);
            var submitPacketMethod = GetSubmitPacketMethod(playerType);
            var nativePlayerField = GetNativePlayerField(playerType);
            _nativePlayerFieldGetter = CreateGetter<ESPlayer, IntPtr>(nativePlayerField);
            _submitPacketMethodCaller =
                (Func<IntPtr, IntPtr, SubmitStatus>) Delegate.CreateDelegate(
                    typeof(Func<IntPtr, IntPtr, SubmitStatus>), null, submitPacketMethod);
        }

        private static MethodInfo GetSubmitPacketMethod(Type playerType)
        {
            var interop = playerType.Assembly.GetType("Interop");
            var nativeEsPlusPlayer = interop.GetNestedType(
                "NativeESPlusPlayer",
                BindingFlags.NonPublic | BindingFlags.Static);
            return nativeEsPlusPlayer.GetMethod(
                "SubmitPacket",
                BindingFlags.NonPublic | BindingFlags.Static,
                Type.DefaultBinder,
                new[] {typeof(IntPtr), typeof(IntPtr)},
                new ParameterModifier[0]);
        }

        private static FieldInfo GetNativePlayerField(Type playerType)
        {
            return playerType.GetField(
                "player",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static Func<S, T> CreateGetter<S, T>(FieldInfo field)
        {
#if !NETSTANDARD
            var methodName = field.ReflectedType.FullName + ".get_" + field.Name;
            var setterMethod = new DynamicMethod(methodName, typeof(T), new Type[1] {typeof(S)}, true);
            var gen = setterMethod.GetILGenerator();
            if (field.IsStatic)
                gen.Emit(OpCodes.Ldsfld, field);
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
            }

            gen.Emit(OpCodes.Ret);
            return (Func<S, T>) setterMethod.CreateDelegate(typeof(Func<S, T>));
#else
            return null;
#endif
        }

        public static SubmitStatus SubmitPacketExt(this ESPlayer player, ESPacket packet)
        {
            Dictionary<string, IntPtr> unmanagedMemories = null;
            try
            {
                unmanagedMemories = GetNativePacket(packet);

                var nativePlayerHandle = _nativePlayerFieldGetter.Invoke(player);
                var result =
                    _submitPacketMethodCaller.Invoke(nativePlayerHandle, unmanagedMemories["unmanagedNativePacket"]);
                return result;
            }
            catch (Exception)
            {
                throw new InvalidOperationException("Submitting espacket to native pipeline has failed.");
            }
            finally
            {
                if (unmanagedMemories != null)
                    foreach (var kv in unmanagedMemories)
                        Marshal.FreeHGlobal(kv.Value);
            }
        }

        private static Dictionary<string, IntPtr> GetNativePacket(ESPacket packet)
        {
            var dictionary = new Dictionary<string, IntPtr>();
            try
            {
                if (packet.buffer == null)
                    return null;
                var length = packet.buffer.Length;
                var unmanagedBufferPtr = Marshal.AllocHGlobal(length * Marshal.SizeOf(packet.buffer[0]));
                dictionary.Add("unmanagedBuffer", unmanagedBufferPtr);
                Marshal.Copy(packet.buffer, 0, unmanagedBufferPtr, length);
                var nativePacket = new NativePacket
                {
                    type = packet.type,
                    buffer = unmanagedBufferPtr,
                    bufferSize = (uint) packet.buffer.Length,
                    pts = packet.pts / 1000000UL,
                    duration = packet.duration,
                    matroskaColorInfo = IntPtr.Zero,
                    hdr10pMetadataSize = 0,
                    hdr10pMetadata = IntPtr.Zero
                };
                var unmanagedNativePacketPtr = Marshal.AllocHGlobal(Marshal.SizeOf(nativePacket));
                dictionary.Add("unmanagedNativePacket", unmanagedNativePacketPtr);
                Marshal.StructureToPtr(nativePacket, unmanagedNativePacketPtr, false);
                return dictionary;
            }
            catch (Exception)
            {
                foreach (var kv in dictionary)
                    Marshal.FreeHGlobal(kv.Value);
                throw;
            }
        }
    }
}