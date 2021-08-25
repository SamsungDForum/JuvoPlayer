/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2021, Samsung Electronics Co., Ltd
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
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.FFmpeg
{
    public class FFmpegStream : IStream
    {
        private ILogger _logger;
        private readonly FFmpegStreamHandler _streamHandler;
        private IStreamRenderer _streamRenderer;
        private Segment _segment;
        private readonly IClock _clock;
        private readonly TimeSpan _maxBufferTime;
        private readonly Subject<Exception> _exceptionSubject;

        public FFmpegStream(
            IClock clock,
            FFmpegStreamHandler streamHandler,
            StreamConfig streamConfig,
            IStreamSelector streamSelector)
        {
            _clock = clock;
            _streamHandler = streamHandler;
            StreamConfig = streamConfig;
            StreamSelector = streamSelector;
            var streamType = streamConfig
                .StreamType()
                .ToString();
            _logger = Log
                .Logger
                .CopyWithPrefix(streamType);
            _maxBufferTime = TimeSpan.FromSeconds(8);
            _exceptionSubject = new Subject<Exception>();
        }

        public IObservable<Exception> OnException()
        {
            return _exceptionSubject.AsObservable();
        }

        public IStreamSelector StreamSelector { get; set; }
        public StreamConfig StreamConfig { get; }

        public Task Prepare()
        {
            return Task.CompletedTask;
        }

        public async Task LoadChunks(
            Segment segment,
            IStreamRenderer streamRenderer,
            CancellationToken token)
        {
            _logger.Info();
            _segment = segment;
            _streamRenderer = streamRenderer;
            try
            {
                await _streamHandler.EnableStream(
                    StreamConfig,
                    segment.Start);

                await RunGetPacketsLoop(token);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Cancelled");
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                _exceptionSubject.OnNext(ex);
            }
            finally
            {
                _logger.Info();
                await _streamHandler.DisableStream(StreamConfig);
            }
        }

        private async Task RunGetPacketsLoop(CancellationToken cancellationToken)
        {
            _logger.Info();
            var bufferPosition = _segment.Start;
            while (!cancellationToken.IsCancellationRequested)
            {
                var playbackPosition = _segment.ToPlaybackTime(_clock.Elapsed);
                var bufferedDuration = bufferPosition - playbackPosition;
                _logger.Info($"{bufferedDuration} = {bufferPosition} - {playbackPosition}");
                if (bufferedDuration <= _maxBufferTime)
                {
                    var packet = await _streamHandler.GetPacket(
                        StreamConfig,
                        cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    _streamRenderer.HandlePacket(packet);
                    if (packet is EosPacket)
                        break;
                    bufferPosition = packet.Pts;
                    continue;
                }

                var halfBufferTime = TimeSpan.FromTicks(_maxBufferTime.Ticks / 2);
                var delayTime = bufferedDuration - halfBufferTime;
                await Task.Delay(delayTime, cancellationToken);
            }
        }

        public Task<StreamConfig> GetStreamConfig(CancellationToken token)
        {
            return Task.FromResult(StreamConfig);
        }

        public Task<TimeSpan> GetAdjustedSeekPosition(TimeSpan position)
        {
            return _streamHandler.GetAdjustedSeekPosition(
                StreamConfig,
                position);
        }

        public void Dispose()
        {
            _exceptionSubject.Dispose();
        }
    }
}