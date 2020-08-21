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

using ElmSharp;

namespace JuvoPlayer.Utils
{
    public class WindowUtils
    {
        public static readonly int DefaultWindowWidth = 1920;
        public static readonly int DefaultWindowHeight = 1080;

        public static Window CreateElmSharpWindow()
        {
            return CreateElmSharpWindow(DefaultWindowWidth, DefaultWindowHeight);
        }

        public static Window CreateElmSharpWindow(int width, int height)
        {
            var window = new Window("JuvoPlayer")
            {
                Geometry = new Rect(0, 0, width, height)
            };

            // Sample code calls following API:
            // skipping geometry settings
            //
            // window.Resize(width, height);
            // window.Realize(null);
            // window.Active();
            // window.Show();
            //
            // Does not seem to be necessary in case of Juvo/Xamarin
            //

            return window;
        }

        public static void DestroyElmSharpWindow(Window window)
        {
            window.Hide();
            window.Unrealize();
        }
    }
}