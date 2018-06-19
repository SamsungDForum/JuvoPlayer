using System;
using System.Runtime.InteropServices;

namespace JuvoPlayer.OpenGL
{
    internal static unsafe class DllImports
    {
        private const string GlDemoLib = "libgles.so";

        // Main functions

        [DllImport(GlDemoLib, EntryPoint = "Create")]
        public static extern void Create();

        [DllImport(GlDemoLib, EntryPoint = "Draw")]
        public static extern void Draw(IntPtr eglDisplay, IntPtr eglSurface);

        // Resource management

        [DllImport(GlDemoLib, EntryPoint = "AddTile")]
        public static extern int AddTile();

        [DllImport(GlDemoLib, EntryPoint = "SetTileData")]
        public static extern void SetTileData(int tileId, byte* pixels, int w, int h, byte* name, int nameLen, byte* desc, int descLen);

        [DllImport(GlDemoLib, EntryPoint = "AddEmptyTile")]
        public static extern int AddEmptyTile();

        [DllImport(GlDemoLib, EntryPoint = "SetTileTexture")]
        public static extern int SetTileTexture(int tileNo, byte* pixels, int w, int h);

        [DllImport(GlDemoLib, EntryPoint = "AddFont")]
        public static extern int AddFont(byte* data, int size);

        [DllImport(GlDemoLib, EntryPoint = "SetIcon")]
        public static extern void SetIcon(int id, byte* pixels, int w, int h);

        [DllImport(GlDemoLib, EntryPoint = "SwitchTextRenderingMode")]
        public static extern void SwitchTextRenderingMode();

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
        public static extern void UpdatePlaybackControls(int show, int state, int currentTime, int totalTime, byte* text, int textLen);

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
        public static extern int AddSuboption(int parentId, int id, byte* text, int textLen);

        [DllImport(GlDemoLib, EntryPoint = "UpdateSelection")]
        public static extern int UpdateSelection(int show, int activeOptionId, int activeSuboptionId, int selectedOptionId, int selectedSuboptionId);

        [DllImport(GlDemoLib, EntryPoint = "ClearOptions")]
        public static extern void ClearOptions();

        // Metrics

        public const int fpsGraphId = 0; // computations handled by C lib

        [DllImport(GlDemoLib, EntryPoint = "AddGraph")]
        public static extern int AddGraph(byte* tag, int tagLen, float minVal, float maxVal, int valuesCount);

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
        public static extern void ShowAlert(byte *title, int titleLen, byte* body, int bodyLen, byte* button, int buttonLen);

        [DllImport(GlDemoLib, EntryPoint = "HideAlert")]
        public static extern void HideAlert();

        [DllImport(GlDemoLib, EntryPoint = "IsAlertVisible")]
        public static extern int IsAlertVisible();
    }
}