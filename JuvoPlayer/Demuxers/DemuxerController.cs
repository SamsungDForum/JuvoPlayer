/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
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
 *
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using Nito.AsyncEx;

namespace JuvoPlayer.Demuxers
{
    public interface IDemuxerCommand
    {
        Task Execute();
    }

    public class InitCommand : IDemuxerCommand
    {
        private readonly DemuxerController _demuxerController;
        private readonly IDemuxerDataSource _demuxerDataSource;
        private readonly IDemuxer _demuxer;
        private CancellationToken _cancellationToken;
        private readonly ILogger _logger;

        public InitCommand(
            DemuxerController demuxerController,
            IDemuxer demuxer,
            IDemuxerDataSource demuxerDataSource,
            CancellationToken token)
        {
            _demuxerController = demuxerController;
            _demuxer = demuxer;
            _demuxerDataSource = demuxerDataSource;
            _cancellationToken = token;
            var contentType = _demuxerController.ContentType;
            _logger = Log
                .WithPrefix(contentType.ToString());
        }

        public async Task Execute()
        {
            try
            {
                _logger.Info();
                _cancellationToken.ThrowIfCancellationRequested();
                if (_demuxer.IsInitialized())
                {
                    _logger.Info();
                    var getPacketsTask = _demuxerController.GetPacketsTask;
                    if (getPacketsTask != null)
                    {
                        try
                        {
                            await getPacketsTask;
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }

                    _cancellationToken.ThrowIfCancellationRequested();
                    _demuxer.Reset();
                }

                _logger.Info();
                _demuxer.SetClient(_demuxerDataSource);
                var clipConfiguration = await _demuxer.InitForEs();
                _cancellationToken.ThrowIfCancellationRequested();
                await _demuxerController.OnDemuxerInitialized(clipConfiguration);
            }
            catch (Exception e)
            {
                _logger.Error(e);
                throw;
            }
        }
    }

    public class EnableStreamsCommand : IDemuxerCommand
    {
        private readonly IDemuxer _demuxer;
        private readonly IList<StreamConfig> _streamConfigs;
        private CancellationToken _cancellationToken;

        public EnableStreamsCommand(
            IDemuxer demuxer,
            IList<StreamConfig> streamConfigs,
            CancellationToken token)
        {
            _demuxer = demuxer;
            _streamConfigs = streamConfigs;
            _cancellationToken = token;
        }

        public Task Execute()
        {
            _cancellationToken.ThrowIfCancellationRequested();
            return _demuxer.EnableStreams(_streamConfigs);
        }
    }

    public class GetPacketsCommand : IDemuxerCommand
    {
        private readonly ILogger _logger;
        private readonly DemuxerController _demuxerController;
        private readonly IDemuxer _demuxer;
        private readonly TimeSpan? _minPts;
        private CancellationToken _cancellationToken;

        public GetPacketsCommand(
            DemuxerController demuxerController,
            IDemuxer demuxer,
            TimeSpan? minPts,
            CancellationToken token)
        {
            _demuxerController = demuxerController;
            var contentType = _demuxerController.ContentType;
            _logger = Log
                .WithPrefix(contentType.ToString());
            _demuxer = demuxer;
            _minPts = minPts;
            _cancellationToken = token;
        }

        public Task Execute()
        {
            var task = RunGetPacketsLoop();
            _demuxerController.GetPacketsTask = task;
            return Task.CompletedTask;
        }

        private async Task RunGetPacketsLoop()
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            using (_cancellationToken.Register(() => taskCompletionSource.SetCanceled()))
            {
                var cancellationTask = taskCompletionSource.Task;
                while (!_cancellationToken.IsCancellationRequested)
                {
                    var completedTask = await Task.WhenAny(
                        _demuxer.NextPacket(_minPts),
                        cancellationTask);
                    if (completedTask == cancellationTask)
                    {
                        _logger.Info("Operation cancelled");
                        return;
                    }

                    var packet = await (Task<Packet>) completedTask;
                    _cancellationToken.ThrowIfCancellationRequested();
                    if (packet == null)
                        return;
                    await _demuxerController.OnPacketReady(packet);
                }
            }
        }
    }

    public class EosCommand : IDemuxerCommand
    {
        private readonly DemuxerController _demuxerController;
        private readonly IList<StreamType> _streamTypes;
        private CancellationToken _cancellationToken;
        private readonly ILogger _logger;

        public EosCommand(
            DemuxerController demuxerController,
            IList<StreamType> streamTypes,
            CancellationToken token)
        {
            _demuxerController = demuxerController;
            _streamTypes = streamTypes;
            _cancellationToken = token;
            var contentType = _demuxerController.ContentType;
            _logger = Log
                .WithPrefix(contentType.ToString());
        }

        public async Task Execute()
        {
            _logger.Info();
            _cancellationToken.ThrowIfCancellationRequested();
            var getPacketsTask = _demuxerController.GetPacketsTask;
            if (getPacketsTask != null)
                await getPacketsTask;
            _cancellationToken.ThrowIfCancellationRequested();
            foreach (var streamType in _streamTypes)
                await _demuxerController.OnPacketReady(new EosPacket(streamType));
        }
    }

    public class DemuxerInitializedEventArgs : EventArgs
    {
        public DemuxerInitializedEventArgs(ClipConfiguration clipConfiguration)
        {
            ClipConfiguration = clipConfiguration;
        }

        public ClipConfiguration ClipConfiguration { get; }
    }

    public class PacketReadyEventArgs : EventArgs
    {
        public PacketReadyEventArgs(Packet packet)
        {
            Packet = packet;
        }

        public Packet Packet { get; }
    }

    public class DemuxerController
    {
        private readonly ILogger _logger;
        private readonly IDemuxer _demuxer;
        private readonly AsyncCollection<IDemuxerCommand> _commandsQueue;
        private Task _executeCommandsTask;
        public ContentType ContentType { get; }

        internal Task GetPacketsTask { get; set; }

        public event Func<object, DemuxerInitializedEventArgs, Task> DemuxerInitialized;
        public event Func<object, PacketReadyEventArgs, Task> PacketReady;

        public DemuxerController(
            IDemuxer demuxer,
            ContentType contentType)
        {
            _logger = Log
                .WithPrefix(contentType.ToString());
            _demuxer = demuxer;
            ContentType = contentType;
            _commandsQueue = new AsyncCollection<IDemuxerCommand>();
        }

        public void Run()
        {
            _executeCommandsTask = ExecuteCommands();
        }

        private async Task ExecuteCommands()
        {
            try
            {
                while (await _commandsQueue.OutputAvailableAsync())
                {
                    var currentCommand = await _commandsQueue.TakeAsync();
                    var commandType = currentCommand.GetType();

                    _logger.Info($"Executing {commandType}");
                    await currentCommand.Execute();
                    _logger.Info($"Executed {commandType}");
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
            finally
            {
                _demuxer.Reset();
            }
        }

        public void Init(
            IDemuxerDataSource dataSource,
            CancellationToken token)
        {
            var command = new InitCommand(
                this,
                _demuxer,
                dataSource,
                token);
            ScheduleCommand(
                command,
                token);
        }

        public Task EnableStreams(
            IList<StreamConfig> streamConfigs,
            CancellationToken token,
            bool callImmediately = false)
        {
            var command = new EnableStreamsCommand(
                _demuxer,
                streamConfigs,
                token);
            if (callImmediately)
                return command.Execute();
            ScheduleCommand(
                command,
                token);
            return Task.CompletedTask;
        }

        public void GetPackets(
            TimeSpan? minPts,
            CancellationToken token,
            bool callImmediately = false)
        {
            var command = new GetPacketsCommand(
                this,
                _demuxer,
                minPts,
                token);
            if (callImmediately)
            {
                command.Execute();
                return;
            }

            ScheduleCommand(
                command,
                token);
        }

        public void NotifyEos(
            IList<StreamType> streamTypes,
            CancellationToken token)
        {
            var command = new EosCommand(
                this,
                streamTypes,
                token);
            ScheduleCommand(
                command,
                token);
        }

        private void ScheduleCommand(
            IDemuxerCommand command,
            CancellationToken token)
        {
            _commandsQueue.Add(command, token);
        }

        internal Task OnDemuxerInitialized(ClipConfiguration clipConfiguration)
        {
            var task = DemuxerInitialized?.Invoke(
                this,
                new DemuxerInitializedEventArgs(clipConfiguration));
            return task ?? Task.CompletedTask;
        }

        internal Task OnPacketReady(Packet packet)
        {
            var task = PacketReady?.Invoke(
                this,
                new PacketReadyEventArgs(packet));
            if (task == null)
                packet?.Dispose();

            return task ?? Task.CompletedTask;
        }

        public async Task CompleteAsync()
        {
            _commandsQueue?.CompleteAdding();
            if (GetPacketsTask != null)
                await AwaitAndIgnoreExceptions(GetPacketsTask);

            if (_executeCommandsTask != null)
                await AwaitAndIgnoreExceptions(_executeCommandsTask);
        }

        private async Task AwaitAndIgnoreExceptions(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                _logger.Warn(e);
            }
        }
    }
}