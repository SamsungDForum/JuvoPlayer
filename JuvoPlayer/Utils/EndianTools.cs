/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2019, Samsung Electronics Co., Ltd
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

using System.Runtime.InteropServices;

namespace JuvoPlayer.Utils
{
    internal static class EndianTools
    {
        internal static class LittleEndian
        {
            [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 4)]
            private struct Int32Le
            {
                [FieldOffset(0)] public readonly int intData;
                [FieldOffset(0)] public byte b0;
                [FieldOffset(1)] public byte b1;
                [FieldOffset(2)] public byte b2;
                [FieldOffset(3)] public byte b3;
            }

            [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 2)]
            private struct Int16Le
            {
                [FieldOffset(0)] public readonly short shortData;
                [FieldOffset(0)] public byte b0;
                [FieldOffset(1)] public byte b1;
            }

            public static int AsInt32(byte[] value, int offset) =>
                new Int32Le
                {
                    b0 = value[offset],
                    b1 = value[1 + offset],
                    b2 = value[2 + offset],
                    b3 = value[3 + offset]
                }.intData;

            public static uint AsUInt32(byte[] value, int offset) =>
                (uint)AsInt32(value, offset);

            public static short AsShort16(byte[] value, int offset) =>
                new Int16Le
                {
                    b0 = value[offset],
                    b1 = value[1 + offset]
                }.shortData;

            public static ushort AsUShort16(byte[] value, int offset) =>
                (ushort)AsShort16(value, offset);

            public static byte[] AsCanonicalUuid(byte[] value, int offset, int[] format)
            {
                var cUuid = new byte[16];
                var writeIdx = 0;

                // Conversion of data defined in format array.
                // An entry defines number of convertable bytes
                foreach (var entry in format)
                {
                    for (var i = entry - 1; i >= 0; i--)
                        cUuid[writeIdx++] = value[offset + i];

                    offset += entry;
                }

                // Data not covered by format[] is re-written "as is"
                for (var i = offset; i < 16; i++)
                    cUuid[i] = value[i];

                return cUuid;
            }
        }

        internal class BigEndian
        {
            [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 4)]
            private struct Int32Be
            {
                [FieldOffset(0)] public readonly int intData;
                [FieldOffset(0)] public byte b3;
                [FieldOffset(1)] public byte b2;
                [FieldOffset(2)] public byte b1;
                [FieldOffset(3)] public byte b0;
            }

            [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 2)]
            private struct Int16Be
            {
                [FieldOffset(0)] public readonly short shortData;
                [FieldOffset(2)] public byte b1;
                [FieldOffset(3)] public byte b0;
            }

            public static int AsInt32(byte[] value, int offset) =>
                new Int32Be
                {
                    b0 = value[offset],
                    b1 = value[1 + offset],
                    b2 = value[2 + offset],
                    b3 = value[3 + offset]
                }.intData;

            public static uint AsUInt32(byte[] value, int offset) =>
                (uint)AsInt32(value, offset);

            public static short AsShort16(byte[] value, int offset) =>
                new Int16Be
                {
                    b0 = value[offset],
                    b1 = value[1 + offset]
                }.shortData;

            public static ushort AsUShort16(byte[] value, int offset) =>
                (ushort)AsShort16(value, offset);
        }
    }
}
