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
using System.Net;
using System.Net.NetworkInformation;

namespace JuvoLogger.Udp
{
    internal static class UdpLoggerToolBox
    {
        public static bool SameAs(this EndPoint a, EndPoint b)
        {
            var thisEp = (IPEndPoint)a;
            var otherEp = (IPEndPoint)b;

            return thisEp.Port == otherEp?.Port && thisEp.Address.Equals(otherEp?.Address);
        }

        public static bool IsAddressValid(this EndPoint ep) => !((IPEndPoint)ep).Address.Equals(IPAddress.None);

        public static void InvalidateAddress(this EndPoint ep)
        {
            ((IPEndPoint)ep).Address = IPAddress.None;
        }

        public static void CopyTo(this EndPoint sourceEp, EndPoint destinationEp)
        {
            ((IPEndPoint)destinationEp).Address = ((IPEndPoint)sourceEp).Address;
            ((IPEndPoint)destinationEp).Port = ((IPEndPoint)sourceEp).Port;
        }
        public static int GetLowestCommonMtu()
        {
            var lowestMtu = int.MaxValue;
            var nics = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var nic in nics)
            {
                var nicMtu = Math.Min(nic.GetIPProperties().GetIPv4Properties().Mtu, nic.GetIPProperties().GetIPv6Properties().Mtu);
                lowestMtu = Math.Min(nicMtu, lowestMtu);
            }

            return lowestMtu;
        }

        public static void Dispose<T>(this T obj) => ((IDisposable)obj).Dispose();

        public static TOutput[] ConvertAll<TInput, TOutput>(Converter<TInput, TOutput> converter, params TInput[] data) =>
            Array.ConvertAll(data, converter);
    }
}
