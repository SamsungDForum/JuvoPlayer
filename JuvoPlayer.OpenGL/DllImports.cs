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

using System.Runtime.InteropServices;

namespace JuvoPlayer.OpenGL
{
    internal static unsafe class DllImports
    {
        private const string GlDemoLib = "libgles.so";

        // Structures

        [StructLayout(LayoutKind.Sequential)]
        public struct TileData
        {
            public int tileId;
            public byte* pixels;
            public int width;
            public int height;
            public byte* name;
            public int nameLen;
            public byte* desc;
            public int descLen;
            public int format;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ImageData
        {
            public int id;
            public byte* pixels;
            public int width;
            public int height;
            public int format;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PlaybackData
        {
            public int show;
            public int state;
            public int currentTime;
            public int totalTime;
            public byte* text;
            public int textLen;
            public int buffering;
            public int bufferingPercent;
            public int seeking;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GraphData
        {
            public byte* tag;
            public int tagLen;
            public float minVal;
            public float maxVal;
            public int valuesCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AlertData
        {
            public byte* title;
            public int titleLen;
            public byte* body;
            public int bodyLen;
            public byte* button;
            public int buttonLen;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SelectionData
        {
            public int show;
            public int activeOptionId;
            public int activeSubOptionId;
            public int selectedOptionId;
            public int selectedSubOptionId;
        }

        // Main functions

        [DllImport(GlDemoLib, EntryPoint = "Create")]
        public static extern void Create();

        [DllImport(GlDemoLib, EntryPoint = "Terminate")]
        public static extern void Terminate();

        [DllImport(GlDemoLib, EntryPoint = "Draw")]
        public static extern void Draw();

        // Resource management

        [DllImport(GlDemoLib, EntryPoint = "AddTile")]
        public static extern int AddTile();

        [DllImport(GlDemoLib, EntryPoint = "SetTileData")]
        public static extern void SetTileData(TileData tileData);

        [DllImport(GlDemoLib, EntryPoint = "AddFont")]
        public static extern int AddFont(byte* data, int size);

        [DllImport(GlDemoLib, EntryPoint = "SetIcon")]
        public static extern void SetIcon(ImageData image);

        // Menu management

        [DllImport(GlDemoLib, EntryPoint = "ShowMenu")]
        public static extern void ShowMenu(int enable);

        [DllImport(GlDemoLib, EntryPoint = "ShowLoader")]
        public static extern void ShowLoader(int enabled, int percent);

        [DllImport(GlDemoLib, EntryPoint = "ShowSubtitle")]
        public static extern void ShowSubtitle(int duration, byte* text, int textLen);

        [DllImport(GlDemoLib, EntryPoint = "SelectTile")]
        public static extern void SelectTile(int tileNo);

        [DllImport(GlDemoLib, EntryPoint = "UpdatePlaybackControls")]
        public static extern void UpdatePlaybackControls(PlaybackData playbackData);

        [DllImport(GlDemoLib, EntryPoint = "SetFooter")]
        public static extern void SetFooter(byte* footer, int footerLen);

        [DllImport(GlDemoLib, EntryPoint = "OpenGLLibVersion")]
        public static extern int OpenGLLibVersion();

        [DllImport(GlDemoLib, EntryPoint = "SelectAction")]
        public static extern void SelectAction(int id);

        // Options menu

        [DllImport(GlDemoLib, EntryPoint = "AddOption")]
        public static extern int AddOption(int id, byte* text, int textLen);

        [DllImport(GlDemoLib, EntryPoint = "AddSuboption")]
        public static extern int AddSubOption(int parentId, int id, byte* text, int textLen);

        [DllImport(GlDemoLib, EntryPoint = "UpdateSelection")]
        public static extern int UpdateSelection(SelectionData selectionData);

        [DllImport(GlDemoLib, EntryPoint = "ClearOptions")]
        public static extern void ClearOptions();

        // Metrics

        public const int wrongGraphId = -1;
        public const int fpsGraphId = 0; // computations handled by C lib

        [DllImport(GlDemoLib, EntryPoint = "AddGraph")]
        public static extern int AddGraph(GraphData graphData);

        [DllImport(GlDemoLib, EntryPoint = "SetGraphVisibility")]
        public static extern void SetGraphVisibility(int graphId, int visible);

        [DllImport(GlDemoLib, EntryPoint = "UpdateGraphValues")]
        public static extern void UpdateGraphValues(int graphId, float* values, int valuesCount);

        [DllImport(GlDemoLib, EntryPoint = "UpdateGraphValue")]
        public static extern void UpdateGraphValue(int graphId, float value);

        [DllImport(GlDemoLib, EntryPoint = "UpdateGraphRange")]
        public static extern void UpdateGraphRange(int graphId, float minVal, float maxVal);

        [DllImport(GlDemoLib, EntryPoint = "SetLogConsoleVisibility")]
        public static extern void SetLogConsoleVisibility(int visible);

        [DllImport(GlDemoLib, EntryPoint = "PushLog")]
        public static extern void PushLog(byte* log, int logLen);

        [DllImport(GlDemoLib, EntryPoint = "ShowAlert")]
        public static extern void ShowAlert(AlertData alertData);

        [DllImport(GlDemoLib, EntryPoint = "HideAlert")]
        public static extern void HideAlert();

        [DllImport(GlDemoLib, EntryPoint = "IsAlertVisible")]
        public static extern int IsAlertVisible();
    }
}