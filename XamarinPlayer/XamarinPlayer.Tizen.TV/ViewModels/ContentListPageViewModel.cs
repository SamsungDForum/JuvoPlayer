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

﻿using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Xamarin.Forms;
using XamarinPlayer.Controls;
using XamarinPlayer.Models;
using XamarinPlayer.Services;

namespace XamarinPlayer.ViewModels
{
    class ContentListPageViewModel : INotifyPropertyChanged
    {
        public List<DetailContentData> ContentList { get; set; }
        public ContentItem FocusedContent { get; set; }
        public ICommand ContentFocusedCommand
        {
            protected set;
            get;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ContentListPageViewModel()
        {
            var clips = DependencyService.Get<IClipReaderService>(DependencyFetchTarget.NewInstance).ReadClips();

            ContentList = clips.Select(o => new DetailContentData()
            {
                Bg = o.Image,
                Clip = o.ClipDetailsHandle,
                ContentFocusedCommand = CreateFocusedCommand(),
                Description = o.Description,
                Image = o.Image,
                Source = o.Source,
                Title = o.Title,
            }).ToList();
        }

        protected ICommand CreateFocusedCommand()
        {
            ICommand command = new Command<ContentItem>((item) =>
            {
                FocusedContent = item;
                OnPropertyChanged("FocusedContent");
            });

            return command;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
