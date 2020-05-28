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

        public static SubmitStatus SubmitPacketExt(this ESPlayer player, ESPacket packet)
        {
            Dictionary<string, IntPtr> unmanagedMemories = null;
            try
            {
                unmanagedMemories = GetNativePacket(packet);
                var playerType = typeof(ESPlayer);
                var submitPacketMethod = GetSubmitPacketMethod(playerType);
                return (SubmitStatus) submitPacketMethod.Invoke(
                    null,
                    new[]
                    {
                        GetNativePlayerHandle(playerType, player),
                        unmanagedMemories["unmanagedNativePacket"]
                    });
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

        private static object GetNativePlayerHandle(Type playerType, ESPlayer player)
        {
            var nativePlayerField = playerType.GetField(
                "player",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return nativePlayerField.GetValue(player);
        }
    }
}