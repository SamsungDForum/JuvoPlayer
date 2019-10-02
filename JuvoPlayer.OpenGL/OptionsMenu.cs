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
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.OpenGL
{
    internal unsafe class OptionsMenu
    {
        public bool Visible { get; private set; } = false;
        public bool SubtitlesOn { get; private set; } = false;

        private int _activeOption = -1;
        private int _activeSubOption = -1;
        private int _selectedOption = -1;
        private int _selectedSubOption = -1;

        private class StreamDescriptionsList
        {
            public List<StreamDescription> Descriptions;
            public StreamType StreamType;
            public int Active;
            public int Id;

            public StreamDescriptionsList(StreamType streamType, int id)
            {
                Id = id;
                StreamType = streamType;
                Active = -1;
                Descriptions = new List<StreamDescription>();
                if (streamType == StreamType.Subtitle)
                {
                    Descriptions.Add(new StreamDescription()
                    {
                        Default = true,
                        Description = "off",
                        Id = 0,
                        StreamType = StreamType.Subtitle
                    });
                }
            }
        };

        private List<StreamDescriptionsList> _streams = new List<StreamDescriptionsList>();

        public ILogger Logger { private get; set; }

        public void LoadStreamLists(IPlayerService player)
        {
            ClearOptionsMenu();

            if (player == null)
            {
                Logger?.Error("player null, cannot load stream lists");
                return;
            }
            Logger?.Info($"loading stream lists");

            SubtitlesOn = false;
            foreach (var streamType in new[] { StreamType.Video, StreamType.Audio, StreamType.Subtitle })
            {
                var streamDescriptionsList = new StreamDescriptionsList(streamType, _streams.Count);
                streamDescriptionsList.Descriptions.AddRange(player.GetStreamsDescription(streamType));
                AddSubmenu(streamDescriptionsList, streamType);
                _streams.Add(streamDescriptionsList);
            }

            SetDefaultState();
        }

        private void AddSubmenu(StreamDescriptionsList streamDescriptionsList, StreamType streamType)
        {
            fixed (byte* text = ResourceLoader.GetBytes(streamDescriptionsList.StreamType.ToString()))
                DllImports.AddOption(streamDescriptionsList.Id, text, streamDescriptionsList.StreamType.ToString().Length);
            for (int id = 0; id < streamDescriptionsList.Descriptions.Count; ++id)
            {
                var s = streamDescriptionsList.Descriptions[id];
                Logger?.Info($"stream.Description=\"{s.Description}\", stream.Id=\"{s.Id}\", stream.Type=\"{s.StreamType}\", stream.Default=\"{s.Default}\"");
                if (s.Default)
                {
                    streamDescriptionsList.Active = id;
                    if (streamType == StreamType.Subtitle)
                        SubtitlesOn = true;
                }
                fixed (byte* text = ResourceLoader.GetBytes(s.Description))
                    DllImports.AddSubOption(streamDescriptionsList.Id, id, text, s.Description.Length);
            }
    }

        private void UpdateOptionsSelection()
        {
            Logger?.Info($"activeOption={_activeOption}, activeSubOption={_activeSubOption}, selectedOption={_selectedOption}, selectedSubOption={_selectedSubOption}");
            if (_selectedOption >= 0 && _selectedOption < _streams.Count)
                _activeSubOption = _streams[_selectedOption].Active;
            DllImports.UpdateSelection(new DllImports.SelectionData()
                {
                    show = Visible ? 1 : 0,
                    activeOptionId = _activeOption,
                    activeSubOptionId = _activeSubOption,
                    selectedOptionId = _selectedOption,
                    selectedSubOptionId = _selectedSubOption
                });
        }

        private void SetDefaultState()
        {
            _activeOption = -1;
            _activeSubOption = -1;
            _selectedOption = 0;
            _selectedSubOption = -1;
            Visible = false;
            SubtitlesOn = false;
            UpdateOptionsSelection();
        }

        public void ClearOptionsMenu()
        {
            _activeOption = -1;
            _activeSubOption = -1;
            _selectedOption = -1;
            _selectedSubOption = -1;
            Visible = false;
            SubtitlesOn = false;
            _streams = new List<StreamDescriptionsList>();
            DllImports.ClearOptions();
        }

        public void ControlLeft()
        {
            if (_selectedSubOption == -1)
                Hide();
            else
            {
                _selectedSubOption = -1;
                UpdateOptionsSelection();
            }
        }

        public void ControlRight()
        {
            if (_selectedSubOption == -1 && _selectedOption >= 0 && _selectedOption < _streams.Count && _streams[_selectedOption].Descriptions.Count > 0)
                _selectedSubOption =
                    _streams[_selectedOption].Active >= 0 && _streams[_selectedOption].Active <
                    _streams[_selectedOption].Descriptions.Count
                        ? _streams[_selectedOption].Active
                        : _streams[_selectedOption].Descriptions.Count - 1;
            UpdateOptionsSelection();
        }

        public void ControlUp()
        {
            if (_selectedSubOption == -1)
            {
                if (_selectedOption > 0)
                    --_selectedOption;
            }
            else
            {
                if (_selectedSubOption > 0)
                    --_selectedSubOption;
            }

            UpdateOptionsSelection();
        }

        public void ControlDown()
        {
            if (_selectedSubOption == -1)
            {
                if (_selectedOption < _streams.Count - 1)
                    ++_selectedOption;
                else
                    Hide();
            }
            else
            {
                if (_selectedSubOption < _streams[_selectedOption].Descriptions.Count - 1)
                    ++_selectedSubOption;
            }

            UpdateOptionsSelection();
        }

        public bool ProperSelection()
        {
            int selectedStreamTypeIndex = _selectedOption;
            int selectedStreamIndex = _selectedSubOption;
            return selectedStreamIndex >= 0 && selectedStreamIndex < _streams[selectedStreamTypeIndex].Descriptions.Count;
        }

        public void ControlSelect(IPlayerService player)
        {
            if (player == null)
                return;

            int selectedStreamTypeIndex = _selectedOption;
            int selectedStreamIndex = _selectedSubOption;

            if (selectedStreamIndex >= 0 && selectedStreamIndex < _streams[selectedStreamTypeIndex].Descriptions.Count)
            {
                if (_streams[selectedStreamTypeIndex].Descriptions[selectedStreamIndex].StreamType == StreamType.Subtitle && selectedStreamIndex == 0) // special subtitles:off sub option
                {
                    SubtitlesOn = false;
                    player.DeactivateStream(StreamType.Subtitle);
                }
                else
                {
                    try
                    {
                        player.ChangeActiveStream(_streams[selectedStreamTypeIndex].Descriptions[selectedStreamIndex]);
                        if (_streams[selectedStreamTypeIndex].Descriptions[selectedStreamIndex].StreamType == StreamType.Subtitle)
                            SubtitlesOn = true;
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                        if (_streams[selectedStreamTypeIndex].Descriptions[selectedStreamIndex].StreamType ==
                            StreamType.Subtitle)
                        {
                            SubtitlesOn = false;
                            selectedStreamIndex = 0;
                        }
                    }
                }

                _streams[selectedStreamTypeIndex].Active = selectedStreamIndex;
                _activeSubOption = selectedStreamIndex;
            }

            UpdateOptionsSelection();
        }

        public void Show()
        {
            _selectedOption = _streams.Count - 1;
            _selectedSubOption = -1;
            Visible = true;
            UpdateOptionsSelection();
        }

        public void Hide()
        {
            Visible = false;
            UpdateOptionsSelection();
        }
    }
};