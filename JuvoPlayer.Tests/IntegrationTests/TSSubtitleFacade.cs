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

﻿using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JuvoPlayer.Common;
using JuvoPlayer.Subtitles;
using NUnit.Framework;

namespace JuvoPlayer.Tests.IntegrationTests
{
    [TestFixture]
    class TSSubtitleFacade
    {
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_windows-1250.srt", "windows-1250", "[Central Europe] 2.5: Ąą Ćć Ęę Łł Ńń Óó Śś Źź Żż")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_windows-1251.srt", "windows-1251", "[Cyrylic] 5.0: Жж Зз Кк Лл Ыы Чч Щщ Ъъ Ыы Юю Яя")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_windows-1252.srt", "windows-1252", "[Western Europe] 4.5: Àà Áá Ââ Ææ Çç Èè Ïï Ðð Ññ Õõ Øø Ýý Þþ £ €")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_windows-1253.srt", "windows-1253", "[Greek] 3.5: Άά Αα Ββ Γγ Δδ Εε Ζζ Ηη Θθ Ιι Κκ Λλ Ξξ Ψψ Ωω ώ")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_windows-1254.srt", "windows-1254", "[Turkish] 3.5: Çç Ğğ Öö Üü Şş ¡ ¿ § µ ¾")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_windows-1255.srt", "windows-1255", "[Herbrew] 3.0: ₪ װױ  אבג דהוז חטיכ ל ר ¡ ¿ § µ ¾ ¥")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_windows-1256.srt", "windows-1256", "[Arabic] 3.0: علاء ديه القط، والقط هو علاء")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_windows-1257.srt", "windows-1257", "[Baltic] 1.5: Ąą Įį Āā Ćć Čč Ģģ Ķķ Ļļ Ńń Ņņ Õõ Ųų Øø Ææ µ ß")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_windows-1258.srt", "windows-1258", "[Vietnamese] 0.5: Àà Áá Ââ Ææ Çç Đđ Èè Ïï Ăă Ññ Ơơ Øø Ưư ₫ £ €")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_big5.srt", "big5", "[Chinese Traditional] 3.0: 她有一隻貓。貓有皮毛")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_cp-949.srt", "euc-kr", "[Korean] 3.5: 그녀는 고양이가. 고양이 모피를 가지고")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_gb2312.srt", "gb2312", "[Chinese Simplified] 4.5: 她有一只猫.猫的皮毛有")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.media_player_subs_utf8.srt", "utf-8", "[UTF-8] 3.0: 고양이. Чч Щщ Ъъ. Óó Śś Źź. Ýý Ţţ Ł €. 猫.")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.subtitles_de.vtt", "utf-8", "Stellen Sie beste Streaming Performance zur Verfügung,")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.subtitles_en.vtt", "utf-8", "A dangerous quest for a lone hunter.")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.subtitles_es.vtt", "utf-8", "Éstas son tierras de dragones, Sintel.")]
        [TestCase("JuvoPlayer.Tests.res.subtitles.subtitles_fr.vtt", "utf-8", "C'est bientôt fini. Chut...")]
        [Category("Positive")]
        public void Parse_DifferentEncodings_ParsesSuccessfully(string resourceName, string encodingName, string expectedCueText)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var parser = new SubtitleFacade();
            var subtitleInfo = CreateSubtitleInfo(resourceName, encodingName);

            var cues = parser.LoadSubtitles(subtitleInfo);

            var found = cues.Any(cue => cue.Text.Equals(expectedCueText));
            Assert.That(found, Is.True);
        }

        private SubtitleInfo CreateSubtitleInfo(string path, string encoding)
        {
            return new SubtitleInfo()
            {
                Path = path,
                Encoding = encoding,
            };
        }
    }
}
