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
    internal class ContentListPageViewModel : INotifyPropertyChanged, IEventSender
    {
        private IClipReaderService _clipReader;
        private IDialogService _dialog;

        private DetailContentData _currentContent;
        private List<DetailContentData> _contentList;
        private bool _isActive;

        public event PropertyChangedEventHandler PropertyChanged;
        public ICommand NextCommand => new Command(Next);
        public ICommand PreviousCommand => new Command(Previous);
        public ICommand ActivateCommand => new Command(Activate);
        public ICommand DeactivateCommand => new Command(Deactivate);

        public ContentListPageViewModel(IDialogService dialog)
        {
            _dialog = dialog;
            _clipReader = DependencyService.Get<IClipReaderService>(DependencyFetchTarget.NewInstance);
            Task.Run(PrepareContent);
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value)
                    return;
                _isActive = value;
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
            List<Clip> clips = null;
            try
            {
                clips = await _clipReader.ReadClips();
            }
            catch (Exception e)
            {
                OnReadError(e.Message);
            }

            if (clips == null)
                return;

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
            CurrentContent = ContentList.Count == 0 ? null : ContentList[0];
            IsActive = true;
        }

        private void Next()
        {
            if (ContentList == null) return;
            int index = ContentList.IndexOf(_currentContent);
            if (index >= ContentList.Count - 1)
                return;
            CurrentContent = ContentList[index + 1];
        }

        private void Previous()
        {
            if (ContentList == null) return;
            int index = ContentList.IndexOf(_currentContent);
            if (index <= 0)
                return;
            CurrentContent = ContentList[index - 1];
        }

        private void Activate()
        {
            IsActive = true;
        }

        private void Deactivate()
        {
            IsActive = false;
        }

        private void OnReadError(string message)
        {
            if (!string.IsNullOrEmpty(message))
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await _dialog.ShowError(message, "Clips Reading Error", "OK",
                        () => MessagingCenter.Send<IEventSender, string>(this, "Pop", null));
                });
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}