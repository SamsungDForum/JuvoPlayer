using System;
using System.Net.Http;
using JuvoPlayer.Dash.MpdParser;

namespace JuvoPlayer.Dash
{
    public class DashManifest
    {
        public static string Tag = "JuvoPlayer";
        public Uri Uri { get; set; }
        public string XmlManifest { get; set; }
        public Document Document { get; set; }
        public DashManifest(string url)
        {
            Uri = new Uri(
                url ?? throw new ArgumentNullException(nameof(url)));
            XmlManifest = DownloadManifest();
            Document = ParseManifest();

        }

        public Document ReloadManifest() {
            XmlManifest = DownloadManifest();
            Document = ParseManifest();
            return Document;
        }

        private string DownloadManifest()
        {
            try
            {
                var client = new HttpClient();
                var response = client.GetAsync(
                    Uri,
                    HttpCompletionOption.ResponseHeadersRead).Result;
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception ex)
            {
                Tizen.Log.Error(
                    Tag,
                    "Cannot download manifest file. Error: " + ex.Message);
                return null;
            }
        }
        private Document ParseManifest()
        {
            try
            {
                var document = new Document(
                    XmlManifest ?? throw new ArgumentNullException(nameof(XmlManifest)),
                    Uri.ToString());
                return document;
            }
            catch (Exception ex)
            {
                Tizen.Log.Error(
                    Tag,
                    "Cannot parse manifest file. Error: " + ex.Message);
                return null;
            }
        }
    }
}
