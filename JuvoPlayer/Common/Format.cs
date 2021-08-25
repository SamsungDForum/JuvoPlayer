/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2020, Samsung Electronics Co., Ltd
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

namespace JuvoPlayer.Common
{
    public class Format
    {
        public Format(string id, string label, SelectionFlags selectionFlags, RoleFlags roleFlags, int? bitrate,
            string codecs, string containerMimeType, string sampleMimeType, int? width, int? height, float? frameRate,
            int? channelCount, int? sampleRate, string language, int? accessibilityChannel)
        {
            Id = id;
            Label = label;
            SelectionFlags = selectionFlags;
            RoleFlags = roleFlags;
            Bitrate = bitrate;
            Codecs = codecs;
            ContainerMimeType = containerMimeType;
            SampleMimeType = sampleMimeType;
            Width = width;
            Height = height;
            FrameRate = frameRate;
            ChannelCount = channelCount;
            SampleRate = sampleRate;
            Language = language;
            AccessibilityChannel = accessibilityChannel;
        }

        public string Id { get; }
        public string Label { get; }
        public SelectionFlags SelectionFlags { get; }
        public RoleFlags RoleFlags { get; }
        public int? Bitrate { get; }
        public string Codecs { get; }
        public string ContainerMimeType { get; }
        public string SampleMimeType { get; }
        public int? Width { get; }
        public int? Height { get; }
        public float? FrameRate { get; }
        public int? ChannelCount { get; }
        public int? SampleRate { get; }
        public string Language { get; }
        public int? AccessibilityChannel { get; }

        public Format CopyWithLabel(string label)
        {
            return new Format(Id, label, SelectionFlags, RoleFlags, Bitrate, Codecs, ContainerMimeType, SampleMimeType,
                Width, Height, FrameRate, ChannelCount, SampleRate, Language, AccessibilityChannel);
        }

        public static Format CreateVideoContainerFormat(string id, string label, string containerMimeType,
            string sampleMimeType, string codecs, int? bitrate, int? width, int? height, float? frameRate,
            SelectionFlags selectionFlags, RoleFlags roleFlags)
        {
            return new Format(id, label, selectionFlags, roleFlags, bitrate, codecs, containerMimeType, sampleMimeType,
                width, height, frameRate, null, null, null, null);
        }

        public static Format CreateAudioContainerFormat(string id, string label, string containerMimeType,
            string sampleMimeType, string codecs, int? bitrate, int? channelCount, int? sampleRate,
            SelectionFlags selectionFlags, RoleFlags roleFlags, string language)
        {
            return new Format(id, label, selectionFlags, roleFlags, bitrate, codecs, containerMimeType, sampleMimeType,
                null, null, null, channelCount, sampleRate, language, null);
        }

        public static Format CreateTextContainerFormat(string id, string label, string containerMimeType,
            string sampleMimeType, string codecs, int? bitrate, SelectionFlags selectionFlags, RoleFlags roleFlags,
            string language, int? accessibilityChannel)
        {
            return new Format(id, label, selectionFlags, roleFlags, bitrate, codecs, containerMimeType, sampleMimeType,
                null, null, null, null, null, language, accessibilityChannel);
        }

        public static Format CreateContainerFormat(string id, string label, string containerMimeType,
            string sampleMimeType, string codecs, int? bitrate, SelectionFlags selectionFlags, RoleFlags roleFlags,
            string language)
        {
            return new Format(id, label, selectionFlags, roleFlags, bitrate, codecs, containerMimeType, sampleMimeType,
                null, null, null, null, null, language, null);
        }

        public static Format CreateAudioSampleFormat(string id, string sampleMimeType, string codecs, int bitrate,
            int channelCount, int sampleRate, string language)
        {
            return new Format(id, null, SelectionFlags.Unknown, RoleFlags.Main, bitrate, codecs, null,
                sampleMimeType, null, null, null, channelCount, sampleRate, language, null);
        }

        public static Format CreateVideoSampleFormat(string id, string sampleMimeType, string codecs, int bitrate,
            int width, int height, float frameRate)
        {
            return new Format(id, null, SelectionFlags.Unknown, RoleFlags.Main, bitrate, codecs, null,
                sampleMimeType, width, height, frameRate, null, null, null, null);
        }
    }
}