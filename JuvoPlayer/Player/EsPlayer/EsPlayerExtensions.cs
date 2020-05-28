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
        private static Dictionary<string, IntPtr> GetNativePacket(ESPacket packet, uint handleSize = 0)
        {
            var dictionary = new Dictionary<string, IntPtr>();
            var destination1 = IntPtr.Zero;
            var num1 = handleSize;
            if (handleSize == 0U)
            {
                if (packet.buffer == null)
                    return null;
                var length = packet.buffer.Length;
                destination1 = Marshal.AllocHGlobal(length * Marshal.SizeOf(packet.buffer[0]));
                Marshal.Copy(packet.buffer, 0, destination1, length);
                num1 = (uint)length;
            }
            var ptr1 = IntPtr.Zero;
            var destination2 = IntPtr.Zero;
            const uint num2 = 0;
            var structure = new NativePacket
            {
                type = packet.type,
                buffer = destination1,
                bufferSize = num1,
                pts = packet.pts / 1000000UL,
                duration = packet.duration,
                matroskaColorInfo = ptr1,
                hdr10pMetadataSize = num2,
                hdr10pMetadata = destination2
            };
            var ptr2 = Marshal.AllocHGlobal(Marshal.SizeOf(structure));
            Marshal.StructureToPtr(structure, ptr2, false);
            dictionary.Add("unmanagedNativePacket", ptr2);
            dictionary.Add("unmanagedBuffer", destination1);
            return dictionary;
        }

        public static SubmitStatus SubmitPacketExt(this ESPlayer player, ESPacket packet)
        {
            Dictionary<string, IntPtr> unmanagedMemories = null;
            try
            {
                unmanagedMemories = GetNativePacket(packet, 0U);
                var playerType = typeof(ESPlayer);
                var nativePlayerField = playerType.GetField("player", BindingFlags.NonPublic | BindingFlags.Instance);
                var interop = playerType.Assembly.GetType("Interop");
                var nativeEsPlusPlayer = interop.GetNestedType("NativeESPlusPlayer", BindingFlags.NonPublic | BindingFlags.Static);
                var submitPacketMethod = nativeEsPlusPlayer.GetMethod("SubmitPacket", BindingFlags.NonPublic | BindingFlags.Static, Type.DefaultBinder, new Type[] { typeof(IntPtr), typeof(IntPtr)}, new ParameterModifier[0]);
                return (SubmitStatus) submitPacketMethod.Invoke(null, new object[] { nativePlayerField.GetValue(player), unmanagedMemories["unmanagedNativePacket"] });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Submitting espacket to native pipeline is failed.");
            }
            finally
            {
                if (unmanagedMemories != null)
                {
                    foreach (var kv in unmanagedMemories)
                    {
                        Marshal.FreeHGlobal(kv.Value);
                    }
                }
            }
        }

    }
}
