using System;

using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Channels;


namespace JuvoLogger.Udp
{
    internal class UdpLoggerService
    {
        private UdpClient _udpClent = new UdpClient();
        private Channel<string> _logChannel;

        public UdpLoggerService(string ip, int port)
        {
            _udpClent.Connect(ip, port);
            _logChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = false
            });

            Run(_logChannel.Reader);
        }

        public void Log(string msg)
        {
            var allGood = _logChannel.Writer.TryWrite(msg);
        }

        private async Task Run(ChannelReader<string> reader)
        {
            var byteBufferSize = 66;
            var byteBuffer = new byte[byteBufferSize];

            while (true)
            {
                var msg = await reader.ReadAsync();
                var msgLen = msg.Length;
                if(msgLen > byteBufferSize-2)
                {
                    byteBufferSize = msgLen + 2;
                    byteBuffer = new byte[byteBufferSize];
                }

                Encoding.UTF8.GetBytes(msg, 0, msgLen, byteBuffer, 0);
                byteBuffer[msgLen] = (byte)'\r';
                byteBuffer[msgLen+1] = (byte)'\n';

                await _udpClent.SendAsync(byteBuffer, msgLen+2);
            }
        }
    }
   
}
