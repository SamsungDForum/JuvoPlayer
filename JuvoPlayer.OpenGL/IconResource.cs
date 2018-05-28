namespace JuvoPlayer.OpenGL
{
    class IconResource : Resource
    {
        private IconType _type;
        private ImageData _image;

        public IconResource(IconType iconType, string path) : base()
        {
            _type = iconType;
            _image.Path = path;
        }

        public override void Load()
        {
            _image = GetImage(_image.Path, ColorSpace.RGBA);
        }

        public override unsafe void Push()
        {
            fixed (byte* p = _image.Pixels)
            {
                DllImports.SetIcon((int)_type, p, _image.Width, _image.Height);
            }
        }
    }
}
