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
using System.IO;
using Newtonsoft.Json;

namespace JuvoPlayer.Utils
{
    public class JSONFileReader
    {
        public static T DeserializeJsonFile<T>(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath cannot be null");

            if (filePath.Length == 0)
                throw new ArgumentException("filePath cannot be empty");

            if (!File.Exists(filePath))
                throw new ArgumentException("json file does not exist");

            var jsonText = File.ReadAllText(filePath);
            return DeserializeJsonText<T>(jsonText);
        }

        public static T DeserializeJsonText<T>(string json)
        {
            if (json == null)
                throw new ArgumentNullException("json cannot be null");

            if (json.Length == 0)
                throw new ArgumentException("json cannot be empty");

            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
