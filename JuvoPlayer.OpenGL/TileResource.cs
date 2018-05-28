namespace JuvoPlayer.OpenGL
{
    class TileResource : Resource
    {
        private int _id;
        private ImageData _image;
        private string _name;
        private string _description;

        public TileResource(int id, string path, string name, string description) : base()
        {
            _id = id;
            _image.Path = path;
            _name = name;
            _description = description;
        }

        public override void Load()
        {
            _image = GetImage(_image.Path, ColorSpace.RGB);
        }

        public override unsafe void Push()
        {
            fixed (byte* p = _image.Pixels, name = GetBytes(_name), desc = GetBytes(_description))
            {
                DllImports.SetTileData(_id, p, _image.Width, _image.Height, name, _name.Length, desc, _description.Length);
            }
        }
    }
}
