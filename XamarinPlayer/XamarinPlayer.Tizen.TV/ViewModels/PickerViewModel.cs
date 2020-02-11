using System.Collections.Generic;
using JuvoPlayer.Common;

namespace XamarinPlayer.Tizen.TV.ViewModels
{
    public class PickerViewModel
    {
        public int SelectedIndex;
        public List<StreamDescription> Source;
        public StreamType Type;
    }
}