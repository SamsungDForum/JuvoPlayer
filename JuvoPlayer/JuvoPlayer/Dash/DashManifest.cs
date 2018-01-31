using System;
using System.Net.Http;
using MpdParser;

namespace JuvoPlayer.Dash
{
    public class DashManifest
    {
        public Uri Uri { get; set; }
        public string XmlManifest { get; set; }
        public Document Document { get; set; }
        public DashManifest(string url)
        {
            Uri = new Uri(
                url ?? throw new ArgumentNullException(
                    "Dash manifest url is empty."));
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
                HttpResponseMessage response = client.GetAsync(
                    Uri,
                    HttpCompletionOption.ResponseHeadersRead).Result;
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception ex)
            {
                Tizen.Log.Error(
                    "JuvoPlayer",
                    "Cannot download manifest file. Error: " + ex.Message);
                return null;
            }
        }
        private Document ParseManifest()
        {
            try
            {
                var document = Document.FromText(
                    XmlManifest ?? throw new ArgumentNullException(
                        "Xml manifest is empty."),
                    Uri.ToString());
                return document;
            }
            catch (Exception ex)
            {
                Tizen.Log.Error(
                    "JuvoPlayer",
                    "Cannot parse manifest file. Error: " + ex.Message);
                return null;
            }
        }
    }
}
