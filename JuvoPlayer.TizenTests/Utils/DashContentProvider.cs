/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
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
 *
 */

using System.IO;
using System.Reflection;

namespace JuvoPlayer.TizenTests.Utils
{
    public class DashContentProvider
    {
        private DashContent googleCar;

        private static byte[] ReadAllBytes(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream(resourceName);
            using (var reader = new BinaryReader(stream))
            {
                return reader.ReadBytes((int) stream.Length);
            }
        }

        public DashContent GetGoogleCar()
        {
            if (!googleCar.IsInitialized)
                LoadGoogleCar();
            return googleCar;
        }

        private void LoadGoogleCar()
        {
            googleCar.Title = "Clean byte range MPEG DASH";
            googleCar.InitSegment =
                ReadAllBytes("JuvoPlayer.TizenTests.res.googlecar.car-20120827-89.mp4-init-segment");
            googleCar.Segments = new[]
                {ReadAllBytes("JuvoPlayer.TizenTests.res.googlecar.car-20120827-89.mp4-3901498-7700066")};
        }
    }
}