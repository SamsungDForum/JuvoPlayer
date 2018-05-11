using System.Collections.Generic;
using JuvoLogger;
using JuvoPlayer.OpenGL.Services;

namespace JuvoPlayer.OpenGL
{
    internal unsafe partial class Program
    {
        private class OptionsMenu
        {
            private bool _optionsShown = false;
            private int _activeOption = -1;
            private int _activeSuboption = -1;
            private int _selectedOption = -1;
            private int _selectedSuboption = -1;
            private bool _subtitlesOn = false;

            private class Stream
            {
                public List<StreamDescription> Descriptions;
                public StreamDescription.StreamType StreamType;
                public string Title;
                public int Active;
                public int Id;
            };

            private List<Stream> _streams = new List<Stream>();

            public ILogger Logger { private get; set; }

            public void GetStreams(PlayerService player)
            {
                ClearOptionsMenu();

                if (player == null)
                    return;

                foreach (var streamType in new[] { StreamDescription.StreamType.Video, StreamDescription.StreamType.Audio, StreamDescription.StreamType.Subtitle })
                {
                    var stream = new Stream
                    {
                        Id = _streams.Count,
                        StreamType = streamType,
                        Title = streamType.ToString()
                    };
                    if (streamType == StreamDescription.StreamType.Subtitle)
                    {
                        stream.Descriptions = new List<StreamDescription>
                        {
                            new StreamDescription()
                            {
                                Default = true,
                                Description = "off",
                                Id = 0,
                                Type = StreamDescription.StreamType.Subtitle
                            }
                        };
                        _subtitlesOn = false;
                    }
                    else
                    {
                        stream.Descriptions = new List<StreamDescription>();
                    }

                    stream.Descriptions.AddRange(player.GetStreamsDescription(streamType));
                    stream.Active = -1;
                    fixed (byte* text = ResourceLoader.GetBytes(stream.Title))
                        DllImports.AddOption(stream.Id, text, stream.Title.Length);
                    for (int id = 0; id < stream.Descriptions.Count; ++id)
                    {
                        var s = stream.Descriptions[id];
                        Logger?.Info("stream.Description=\"" + s.Description + "\", stream.Id=\"" + s.Id + "\", stream.Type=\"" + s.Type + "\", stream.Default=\"" + s.Default + "\"");
                        if (s.Default)
                        {
                            stream.Active = id;
                            if (streamType == StreamDescription.StreamType.Subtitle)
                                _subtitlesOn = true;
                        }

                        fixed (byte* text = ResourceLoader.GetBytes(s.Description))
                            DllImports.AddSuboption(stream.Id, id, text, s.Description.Length);
                    }

                    _streams.Add(stream);
                }

                _activeOption = -1;
                _activeSuboption = -1;
                _selectedOption = 0;
                _selectedSuboption = -1;
                _optionsShown = false;
                _subtitlesOn = false;
                UpdateOptionsSelection();
            }

            private void UpdateOptionsSelection()
            {
                Logger?.Info("activeOption=" + _activeOption + ", activeSuboption=" + _activeSuboption + ", selectedOption=" + _selectedOption + ", selectedSuboption=" + _selectedSuboption);
                if (_selectedOption >= 0 && _selectedOption < _streams.Count)
                    _activeSuboption = _streams[_selectedOption].Active;
                DllImports.UpdateSelection(_optionsShown ? 1 : 0, _activeOption, _activeSuboption, _selectedOption, _selectedSuboption);
            }

            private void ClearOptionsMenu()
            {
                _activeOption = -1;
                _activeSuboption = -1;
                _selectedOption = -1;
                _selectedSuboption = -1;
                _optionsShown = false;
                _subtitlesOn = false;
                _streams = new List<Stream>();
                DllImports.ClearOptions();
            }

            public void ControlLeft()
            {
                if (_selectedSuboption == -1)
                    Hide();
                else
                {
                    _selectedSuboption = -1;
                    UpdateOptionsSelection();
                }
            }

            public void ControlRight()
            {
                if (_selectedSuboption == -1 && _streams[_selectedOption].Descriptions.Count > 0)
                    _selectedSuboption =
                        _streams[_selectedOption].Active >= 0 && _streams[_selectedOption].Active <
                        _streams[_selectedOption].Descriptions.Count
                            ? _streams[_selectedOption].Active
                            : _streams[_selectedOption].Descriptions.Count - 1;
                UpdateOptionsSelection();
            }

            public void ControlUp()
            {
                if (_selectedSuboption == -1)
                {
                    if (_selectedOption > 0)
                        --_selectedOption;
                }
                else
                {
                    if (_selectedSuboption > 0)
                        --_selectedSuboption;
                }

                UpdateOptionsSelection();
            }

            public void ControlDown()
            {
                if (_selectedSuboption == -1)
                {
                    if (_selectedOption < _streams.Count - 1)
                        ++_selectedOption;
                }
                else
                {
                    if (_selectedSuboption < _streams[_selectedOption].Descriptions.Count - 1)
                        ++_selectedSuboption;
                }

                UpdateOptionsSelection();
            }

            public void ControlSelect(PlayerService player)
            {
                if (_selectedSuboption >= 0 && _selectedSuboption < _streams[_selectedOption].Descriptions.Count)
                {
                    if (_streams[_selectedOption].Descriptions[_selectedSuboption].Type ==
                        StreamDescription.StreamType.Subtitle && _selectedSuboption == 0) // turn off subtitles
                    {
                        _subtitlesOn = false;
                        player.DeactivateStream(StreamDescription.StreamType.Subtitle);
                    }
                    else
                    {
                        if (_streams[_selectedOption].Descriptions[_selectedSuboption].Type ==
                            StreamDescription.StreamType.Subtitle)
                            _subtitlesOn = true;
                        player.ChangeActiveStream(_streams[_selectedOption].Descriptions[_selectedSuboption]);
                    }

                    _streams[_selectedOption].Active = _selectedSuboption;
                    _activeSuboption = _selectedSuboption;
                }

                UpdateOptionsSelection();
            }

            public void Show()
            {
                _selectedOption = _streams.Count - 1;
                _selectedSuboption = -1;
                _optionsShown = true;
                UpdateOptionsSelection();
            }

            public void Hide()
            {
                _optionsShown = false;
                UpdateOptionsSelection();
            }

            public bool IsShown()
            {
                return _optionsShown;
            }

            public bool SubtitlesOn()
            {
                return _subtitlesOn;
            }
        }
    }
};