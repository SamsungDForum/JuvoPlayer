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
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.Tests.Utils
{
    [Serializable]
    public class ChangeRepresentationOperation : TestOperation
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("UT");

        public int Index { get; set; }
        public StreamType StreamType { get; set; }

        private bool Equals(ChangeRepresentationOperation other)
        {
            return Index == other.Index && StreamType == other.StreamType;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((ChangeRepresentationOperation)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Index * 397) ^ (int)StreamType;
            }
        }

        public void Prepare(TestContext context)
        {
            var service = context.Service;
            StreamType = GetRandomStreamType();
            Index = GetRandomStreamDescriptionIndex(service, StreamType);
        }

        public async Task Execute(TestContext context)
        {
            var service = context.Service;
            var descriptions = service.GetStreamsDescription(StreamType);
            if (Index == -1)
                return;

            var changeTask = service.ChangeActiveStream(descriptions[Index]);

            try
            {
                if (context.Timeout != TimeSpan.Zero)
                    changeTask = changeTask.WithTimeout(context.Timeout);

                await changeTask.WithCancellation(context.Token);
            }
            catch (Exception e)
            {
                _logger.Error(e,
                    $"State: {changeTask.Status} {descriptions[Index].StreamType} {descriptions[Index].Id} {descriptions[Index].Description}");
                throw;
            }

        }

        private static StreamType GetRandomStreamType()
        {
            var values = Enum.GetValues(typeof(StreamType));
            var random = new Random();
            return (StreamType)values.GetValue(random.Next(values.Length));
        }

        private static int GetRandomStreamDescriptionIndex(IPlayerService service, StreamType streamType)
        {
            var descriptions = service.GetStreamsDescription(streamType);
            if (descriptions.Count == 0)
                return -1;

            var random = new Random();
            return random.Next(descriptions.Count);
        }
    }
}