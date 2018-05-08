using System.Collections.Generic;
using JuvoPlayer.OpenGL.Services;
using Tizen;
using Tizen.TV.NUI.GLApplication;

namespace JuvoPlayer.OpenGL
{
    internal unsafe partial class Program : TVGLApplication
    {
        private class OptionsMenu {
            private bool _optionsShown = false;
            private int _activeOption = -1;
            private int _activeSuboption = -1;
            private int _selectedOption = -1;
            private int _selectedSuboption = -1;
            private bool _subtitlesOn = false;

            private class Stream {
                public List<Services.StreamDescription> descriptions;
                public StreamDescription.StreamType streamType;
                public string title;
                public int active;
                public int id;
            };

            private List<Stream> streams = new List<Stream>();

            public void GetStreams(PlayerService player) {
                ClearOptionsMenu();
                if (player != null) {
                    foreach (var streamType in new[] { StreamDescription.StreamType.Video, StreamDescription.StreamType.Audio, StreamDescription.StreamType.Subtitle }) {
                        var stream = new Stream();
                        stream.id = streams.Count;
                        stream.streamType = streamType;
                        stream.title = streamType.ToString();
                        if (streamType == StreamDescription.StreamType.Subtitle) {
                            stream.descriptions = new List<StreamDescription>
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
                        else {
                            stream.descriptions = new List<StreamDescription>();
                        }
                        stream.descriptions.AddRange(player.GetStreamsDescription(streamType));
                        stream.active = -1;
                        fixed (byte* text = GetBytes(stream.title))
                            AddOption(stream.id, text, stream.title.Length);
                        for (int id = 0; id < stream.descriptions.Count; ++id) {
                            var s = stream.descriptions[id];
                            Log.Info("JuvoPlayer", "stream.Description=\"" + s.Description + "\", stream.Id=\"" + s.Id + "\", stream.Type=\"" + s.Type + "\", stream.Default=\"" + s.Default + "\"");
                            if (s.Default) {
                                stream.active = id;
                                if (streamType == StreamDescription.StreamType.Subtitle)
                                    _subtitlesOn = true;
                            }
                            fixed (byte* text = GetBytes(s.Description))
                                AddSuboption(stream.id, id, text, s.Description.Length);
                        }
                        streams.Add(stream);
                    }
                    _activeOption = -1;
                    _activeSuboption = -1;
                    _selectedOption = 0;
                    _selectedSuboption = -1;
                    UpdateOptionsSelection();
                }
            }

            private void UpdateOptionsSelection() {
                Log.Info("JuvoPlayer", "activeOption=" + _activeOption + ", activeSuboption=" + _activeSuboption + ", selectedOption=" + _selectedOption + ", selectedSuboption=" + _selectedSuboption);
                if (_selectedOption >= 0 && _selectedOption < streams.Count)
                    _activeSuboption = streams[_selectedOption].active;
                UpdateSelection(_optionsShown ? 1 : 0, _activeOption, _activeSuboption, _selectedOption, _selectedSuboption);
            }

            private void ClearOptionsMenu() {
                _activeOption = -1;
                _activeSuboption = -1;
                _selectedOption = -1;
                _selectedSuboption = -1;
                streams = new List<Stream>();
                ClearOptions();
            }
            public void ControlLeft() {
                if (_selectedSuboption == -1)
                    Hide();
                else {
                    _selectedSuboption = -1;
                    UpdateOptionsSelection();
                }
            }

            public void ControlRight() {
                if (_selectedSuboption == -1 && streams[_selectedOption].descriptions.Count > 0)
                    _selectedSuboption = streams[_selectedOption].active >= 0 && streams[_selectedOption].active < streams[_selectedOption].descriptions.Count ? streams[_selectedOption].active : streams[_selectedOption].descriptions.Count - 1;
                UpdateOptionsSelection();
            }

            public void ControlUp() {
                if (_selectedSuboption == -1) {
                    if (_selectedOption > 0)
                        --_selectedOption;
                }
                else {
                    if (_selectedSuboption > 0)
                        --_selectedSuboption;
                }
                UpdateOptionsSelection();
            }

            public void ControlDown() {
                if (_selectedSuboption == -1) {
                    if (_selectedOption < streams.Count - 1)
                        ++_selectedOption;
                }
                else {
                    if (_selectedSuboption < streams[_selectedOption].descriptions.Count - 1)
                        ++_selectedSuboption;
                }
                UpdateOptionsSelection();
            }

            public void ControlSelect(PlayerService player) {
                if (_selectedSuboption >= 0 && _selectedSuboption < streams[_selectedOption].descriptions.Count) {
                    if (streams[_selectedOption].descriptions[_selectedSuboption].Type == StreamDescription.StreamType.Subtitle && _selectedSuboption == 0) // turn off subtitles
                    {
                        // TODO(g.skowinski): Turn off subtitles
                        _subtitlesOn = false;
                    }
                    else {
                        if (streams[_selectedOption].descriptions[_selectedSuboption].Type == StreamDescription.StreamType.Subtitle)
                            _subtitlesOn = true;
                        player.ChangeActiveStream(streams[_selectedOption].descriptions[_selectedSuboption]);
                    }
                    streams[_selectedOption].active = _selectedSuboption;
                    _activeSuboption = _selectedSuboption;
                }
                UpdateOptionsSelection();
            }

            public void Show() {
                _selectedOption = streams.Count - 1;
                _selectedSuboption = -1;
                _optionsShown = true;
                UpdateOptionsSelection();
            }

            public void Hide() {
                _optionsShown = false;
                UpdateOptionsSelection();
            }

            public bool IsShown() {
                return _optionsShown;
            }

            public bool SubtitlesOn() {
                return _subtitlesOn;
            }
        }
    }
};