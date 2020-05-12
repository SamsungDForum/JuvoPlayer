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

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;
using XamarinPlayer.Tizen.TV.Models;
using XamarinPlayer.Tizen.TV.Services;

namespace XamarinPlayer.Tizen.TV.ViewModels
{
    internal class ContentListPageViewModel : INotifyPropertyChanged
    {
        private DetailContentData _currentContent;
        private List<DetailContentData> _contentList;
        private bool _isBusy = true;

        public event PropertyChangedEventHandler PropertyChanged;
        public ICommand NextCommand => new Command(Next);
        public ICommand PreviousCommand => new Command(Previous);
        public ICommand LoadCommand => new Command(Load);
        public ICommand UnloadCommand => new Command(Unload);

        public ContentListPageViewModel()
        {
            PrepareContent();
        }
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value)
                    return;
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public List<DetailContentData> ContentList
        {
            get => _contentList;
            set
            {
                if (_contentList == value)
                    return;
                _contentList = value;
                OnPropertyChanged();
            }
        }

        public DetailContentData CurrentContent
        {
            get => _currentContent;
            set
            {
                if (_currentContent == value)
                    return;
                _currentContent = value;
                OnPropertyChanged();
            }
        }
        
        private async void PrepareContent()
        {
            await Task.Yield();
            var clips = DependencyService.Get<IClipReaderService>(DependencyFetchTarget.NewInstance).ReadClips().Result;

            ContentList = clips.Select(o => new DetailContentData
            {
                Bg = o.Image,
                Clip = o.ClipDetailsHandle,
                Description = o.Description,
                Image = o.Image,
                Source = o.Source,
                Title = o.Title,
                TilePreviewPath = o.TilePreviewPath
            }).ToList();
            CurrentContent = ContentList == null || ContentList.Count == 0 ? null : ContentList[0];
            IsBusy = false;
        }

        private void Next()
        {
            int index = ContentList.IndexOf(_currentContent);
            if (index >= ContentList.Count - 1)
                return;
            CurrentContent = ContentList[index + 1];
        }

        private void Previous()
        {
            int index = ContentList.IndexOf(_currentContent);
            if (index <= 0)
                return;
            CurrentContent = ContentList[index - 1];
        }
        
        private void Load()
        {
            IsBusy = false;
        }

        private void Unload()
        {
            IsBusy = true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}