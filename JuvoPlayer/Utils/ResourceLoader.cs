using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;

namespace JuvoPlayer.Utils
{
    internal class ResourceLoader
    {
        internal HttpClient HttpClient { get; set; }
        internal Assembly Assembly { get; set; }

        public ResourceLoader()
        {
            HttpClient = new HttpClient();
            Assembly = Assembly.GetExecutingAssembly();
        }

        public virtual Stream Load(string path)
        {
            if (path.StartsWith("http") || path.EndsWith("https"))
            {
                return LoadAsNetworkResource(new Uri(path));
            }
            return LoadAsEmbeddedResource(path);
        }

        public virtual Stream LoadAsNetworkResource(Uri networkUrl)
        {
            var responseTask = HttpClient.GetStreamAsync(networkUrl);
            return responseTask.Result;
        }

        public virtual Stream LoadAsEmbeddedResource(string resourceName)
        {
            var manifestResourceName = Assembly.GetManifestResourceNames().First(name => name.Contains(resourceName));
            return manifestResourceName == null ? null : Assembly.GetManifestResourceStream(manifestResourceName);
        }
    }
}
