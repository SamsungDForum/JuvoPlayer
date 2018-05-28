using System.IO;
using System.Text;
using ImageSharp;

namespace JuvoPlayer.OpenGL
{
    abstract class Resource
    {
        public abstract void Load();

        public abstract void Push(); // must be run from the main thread (thread with main OpenGL context)

        public static byte[] GetBytes(string str)
        {
            return Encoding.ASCII.GetBytes(str);
        }

        protected ImageData GetImage(string path, ColorSpace colorSpace)
        {
            ImageData image;
            image.Path = path;
            using (var stream = File.OpenRead(image.Path))
            {
                var img = new Image(stream);
                image.Width = img.Width;
                image.Height = img.Height;
                image.Pixels = GetPixels(img, colorSpace);
            }

            return image;
        }

        protected byte[] GetData(string path)
        {
            byte[] data;
            using (var stream = File.OpenRead(path))
            {
                data = new byte[stream.Length];
                stream.Read(data, 0, (int)stream.Length);
            }

            return data;
        }

        protected static byte[] GetPixels(Image image, ColorSpace colorSpace)
        {
            int channels;
            switch (colorSpace)
            {
                case ColorSpace.RGB:
                    channels = 3;
                    break;
                case ColorSpace.RGBA:
                    channels = 4;
                    break;
                default:
                    return new byte[] { };
            }
            var pixels = new byte[image.Pixels.Length * channels];
            for (var i = 0; i < image.Pixels.Length; ++i)
            {
                pixels[channels * i + 0] = image.Pixels[i].R;
                pixels[channels * i + 1] = image.Pixels[i].G;
                pixels[channels * i + 2] = image.Pixels[i].B;
                if (colorSpace == ColorSpace.RGBA)
                    pixels[channels * i + 3] = image.Pixels[i].A;
            }
            return pixels;
        }
    }
}
