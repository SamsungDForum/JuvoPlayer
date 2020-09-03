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

using System.Collections.Generic;

namespace JuvoPlayer.Dash.MPD
{
    internal class Formatter
    {
        private readonly char _fill;
        private readonly int _pad;
        public readonly List<int> Positions;
        public readonly string TrueKey;

        public Formatter(string key)
        {
            Positions = new List<int>();
            var keyFmt = key.Split('%');
            TrueKey = keyFmt[0];
            _pad = 1;
            _fill = '0';

            if (keyFmt.Length <= 1 || string.IsNullOrEmpty(keyFmt[1]))
                return;
            var fmt = keyFmt[1];
            var ndx = 0;
            if (fmt[0] < '1' || fmt[0] > '9')
            {
                _fill = fmt[0];
                ndx = 1;
            }

            _pad = 0;
            while (fmt[ndx] >= '0' && fmt[ndx] <= '9')
            {
                _pad *= 10;
                _pad += fmt[ndx] - '0';
                ++ndx;
            }

            if (_pad < 1)
                _pad = 1;
        }

        public string GetValue(object value)
        {
            if (value != null)
                return value.ToString().PadLeft(_pad, _fill);
            if (string.IsNullOrEmpty(TrueKey))
                return "$"; // $$ escapes a dollar sign
            return "$" + TrueKey + "$";
        }
    }

    public class UrlTemplate
    {
        private readonly string[] _chunks;
        private readonly Dictionary<string, Formatter> _keys = new Dictionary<string, Formatter>();

        public UrlTemplate(string text)
        {
            _chunks = text.Split('$');
            for (var i = 0; i < _chunks.Length; ++i)
            {
                if (i % 2 == 0)
                    continue;
                var chunk = _chunks[i];
                if (!_keys.ContainsKey(chunk))
                    _keys.Add(chunk, new Formatter(chunk));
                _keys[chunk].Positions.Add(i);
            }
        }

        public override string ToString()
        {
            return Get(null, null);
        }

        private string Get(IReadOnlyDictionary<string, object> args)
        {
            var result = new string[_chunks.Length];
            _chunks.CopyTo(result, 0);
            for (var i = 0; i < _chunks.Length; ++i)
            {
                if (i % 2 == 0)
                    continue;
                result[i] = "$"; // assume empty
            }

            foreach (var key in _keys.Keys)
            {
                var fmt = _keys[key];
                var value = fmt.GetValue(
                    args.TryGetValue(fmt.TrueKey, out var arg) ? arg : null);

                foreach (var i in fmt.Positions) result[i] = value;
            }

            return string.Join("", result);
        }

        public string Get(uint? bandwidth, string reprId)
        {
            var dict = new Dictionary<string, object>();
            if (bandwidth != null)
                dict.Add("Bandwidth", bandwidth.Value);
            if (reprId != null)
                dict.Add("RepresentationID", reprId);
            return Get(dict);
        }

        public string Get(int? bandwidth, string reprId, long number, long time)
        {
            var dict = new Dictionary<string, object> { ["Number"] = number, ["Time"] = time };
            if (bandwidth != null)
                dict.Add("Bandwidth", bandwidth.Value);
            if (reprId != null)
                dict.Add("RepresentationID", reprId);
            return Get(dict);
        }
    }
}
