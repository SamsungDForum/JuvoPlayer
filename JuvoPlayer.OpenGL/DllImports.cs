using System;
using System.Runtime.InteropServices;
using Tizen.TV.NUI.GLApplication;

namespace JuvoPlayer.OpenGL
{
    internal unsafe partial class Program : TVGLApplication
    {
        private const string GlDemoLib = "libgles_sample.so";

        [DllImport(GlDemoLib, EntryPoint = "Create")]
        private static extern void Create();

        [DllImport(GlDemoLib, EntryPoint = "Draw")]
        private static extern void Draw(IntPtr eglDisplay, IntPtr eglSurface);

        [DllImport(GlDemoLib, EntryPoint = "AddTile")]
        private static extern int AddTile();

        [DllImport(GlDemoLib, EntryPoint = "SetTileData")]
        private static extern void SetTileData(int tileId, byte* pixels, int w, int h, byte* name, int nameLen,
            byte* desc, int descLen);

        [DllImport(GlDemoLib, EntryPoint = "AddEmptyTile")]
        private static extern int AddEmptyTile();

        [DllImport(GlDemoLib, EntryPoint = "SetTileTexture")]
        private static extern int SetTileTexture(int tileNo, byte* pixels, int w, int h);

        [DllImport(GlDemoLib, EntryPoint = "SelectTile")]
        private static extern void SelectTile(int tileNo);

        [DllImport(GlDemoLib, EntryPoint = "ShowMenu")]
        private static extern void ShowMenu(int enable);

        [DllImport(GlDemoLib, EntryPoint = "AddFont")]
        private static extern int AddFont(byte* data, int size);

        [DllImport(GlDemoLib, EntryPoint = "ShowLoader")]
        private static extern void ShowLoader(int enabled, int percent);

        [DllImport(GlDemoLib, EntryPoint = "UpdatePlaybackControls")]
        private static extern void UpdatePlaybackControls(int show, int state, int currentTime, int totalTime,
            byte* text, int textLen);

        [DllImport(GlDemoLib, EntryPoint = "SetIcon")]
        private static extern void SetIcon(int id, byte* pixels, int w, int h);

        [DllImport(GlDemoLib, EntryPoint = "SetFooter")]
        private static extern void SetFooter(byte* footer, int footerLen);

        [DllImport(GlDemoLib, EntryPoint = "SwitchTextRenderingMode")]
        private static extern void SwitchTextRenderingMode();

        [DllImport(GlDemoLib, EntryPoint = "SwitchFPSCounterVisibility")]
        private static extern void SwitchFPSCounterVisibility();

        [DllImport(GlDemoLib, EntryPoint = "ShowSubtitle")]
        private static extern void ShowSubtitle(int duration, byte* text, int textLen);

        [DllImport(GlDemoLib, EntryPoint = "OpenGLLibVersion")]
        private static extern int OpenGLLibVersion();

        [DllImport(GlDemoLib, EntryPoint = "AddOption")]
        private static extern int AddOption(int id, byte* text, int textLen);

        [DllImport(GlDemoLib, EntryPoint = "AddSuboption")]
        private static extern int AddSuboption(int parentId, int id, byte* text, int textLen);

        [DllImport(GlDemoLib, EntryPoint = "UpdateSelection")]
        private static extern int UpdateSelection(int show, int activeOptionId, int activeSuboptionId,
            int selectedOptionId, int selectedSuboptionId);

        [DllImport(GlDemoLib, EntryPoint = "ClearOptions")]
        private static extern void ClearOptions();
    }
}