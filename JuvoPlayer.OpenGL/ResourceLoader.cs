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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using JuvoPlayer.Common;
using JuvoPlayer.ResourceLoaders;
using JuvoPlayer.Utils;

namespace JuvoPlayer.OpenGL
{
    internal class ResourceLoader
    {
        public List<ClipDefinition> ContentList { get; private set; }
        public int TilesCount => ContentList?.Count ?? 0;

        public bool IsLoadingFinished { get; private set; }
        public bool IsQueueingFinished { get; private set; }

        private int _resourcesLoadedCount;
        private int _resourcesTargetCount;
        private readonly SynchronizationContext _synchronizationContext = SynchronizationContext.Current; // If "Current" is null, then the thread's current context is "new SynchronizationContext()", by convention.
        private Action _doAfterFinishedLoading;

        private static ResourceLoader _instance;

        protected ResourceLoader()
        {
        }

        public static ResourceLoader GetInstance()
        {
            return _instance ?? (_instance = new ResourceLoader());
        }

        public async void LoadResources(string fullExecutablePath, Func<string, Action> onLoadingErrorHandler, Action doAfterFinishedLoading = null)
        {
            IsQueueingFinished = false;
            IsLoadingFinished = false;
            _doAfterFinishedLoading = doAfterFinishedLoading;

            InitLoadingScreen();

            var localResourcesDirPath = Path.Combine(fullExecutablePath, "res");

            LoadFonts(localResourcesDirPath);
            LoadIcons(localResourcesDirPath);
            
            try
            {
                await LoadContentList(Paths.VideoClipJsonPath);
            }
            catch (Exception e)
            {
                ScheduleOnMainThread(onLoadingErrorHandler(e.Message));
                return;
            }

            LoadTiles();

            IsQueueingFinished = true;
        }

        public static byte[] GetBytes(string str)
        {
            return Encoding.ASCII.GetBytes(str);
        }

        private void ScheduleOnMainThread(Action lambda)
        {
            _synchronizationContext.Post(delegate { lambda.Invoke(); }, null);
        }

        private async void LoadAndSchedule(Resource resource)
        {
            await Task.Run(async () =>
            {
                await resource.Load();
            });

            NativeActions.GetInstance().Enqueue(resource.Push);
            ++_resourcesLoadedCount;
            UpdateLoadingState();
        }

        private void FinishLoading()
        {
            IsLoadingFinished = true;
            if(_doAfterFinishedLoading != null)
                ScheduleOnMainThread(_doAfterFinishedLoading); // it's already called from the main thread since last job calls this method, but just to be safe let's schedule it for the main thread
        }

        private async Task LoadContentList(string uri)
        {
            using (var resource = ResourceFactory.Create(uri))
            {
                var content = await resource.ReadAsStringAsync();
                ContentList = JSONFileReader.DeserializeJsonText<List<ClipDefinition>>(content).ToList();
                foreach (var definition in ContentList)
                {
                    definition.Poster = resource.Resolve(definition.Poster).AbsolutePath;
                    if (definition.TilePreviewPath != null)
                        definition.TilePreviewPath = resource.Resolve(definition.TilePreviewPath).AbsolutePath;
                    if (definition.SeekPreviewPath != null)
                        definition.SeekPreviewPath = resource.Resolve(definition.SeekPreviewPath).AbsolutePath;
                }
            }
        }
        private void LoadTiles()
        {
            _resourcesTargetCount += ContentList.Count;
            foreach (var contentItem in ContentList)
                NativeActions.GetInstance().Enqueue(() => LoadAndSchedule(new TileResource(DllImports.AddTile(), contentItem.Poster, contentItem.Title ?? "", contentItem.Description ?? "")));
        }

        private void LoadIcons(string dirPath)
        {
            _resourcesTargetCount += Icons.Length;
            foreach (var icon in Icons)
                LoadAndSchedule(new IconResource(icon.Id, Path.Combine(dirPath, "icons", icon.Image.Path)));
        }

        private void LoadFonts(string dirPath)
        {
            _resourcesTargetCount += 1;
            LoadAndSchedule(new FontResource(Path.Combine(dirPath, "fonts/akashi.ttf")));
        }

        private void InitLoadingScreen()
        {
            DllImports.ShowLoader(1, 0);
        }

        private void UpdateLoadingState()
        {
            UpdateLoadingScreen();
            if (IsQueueingFinished && _resourcesLoadedCount >= _resourcesTargetCount)
                FinishLoading();
        }

        private void UpdateLoadingScreen()
        {
            DllImports.ShowLoader(_resourcesLoadedCount < _resourcesTargetCount ? 1 : 0, _resourcesTargetCount > 0 ? 100 * _resourcesLoadedCount / _resourcesTargetCount : 0);
        }

        private static readonly Icon[] Icons =
        {
            new Icon
            {
                Id = IconType.Play,
                Image = new ImageData { Path = "play.png" }
            },
            new Icon
            {
                Id = IconType.Resume,
                Image = new ImageData { Path = "resume.png" }
            },
            new Icon
            {
                Id = IconType.Stop,
                Image = new ImageData { Path = "stop.png" }
            },
            new Icon
            {
                Id = IconType.Pause,
                Image = new ImageData { Path = "pause.png" }
            },
            new Icon
            {
                Id = IconType.FastForward,
                Image = new ImageData { Path = "fast-forward.png" }
            },
            new Icon
            {
                Id = IconType.Rewind,
                Image = new ImageData { Path = "rewind.png" }
            },
            new Icon
            {
                Id = IconType.SkipToEnd,
                Image = new ImageData { Path = "skip-to-end.png" }
            },
            new Icon
            {
                Id = IconType.SkipToStart,
                Image = new ImageData { Path = "skip-to-start.png" }
            },
            new Icon
            {
                Id = IconType.Options,
                Image = new ImageData { Path = "options.png" }
            }
        };
    }
}
