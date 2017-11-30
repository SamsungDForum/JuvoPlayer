using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using Tizen.Applications;
using System.Xml.Serialization;
using MpdParser;

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
                url ?? throw new ArgumentNullException(
                    "Dash manifest url is empty."));
            this.XmlManifest = DownloadManifest();
            this.Document = ParseManifest();

        }

        public Document ReloadManifest() {
            this.XmlManifest = DownloadManifest();
            this.Document = ParseManifest();
            return this.Document;
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
                    Tag,
                    "Cannot download manifest file. Error: " + ex.Message);
                return null;
            }
        }
        private Document ParseManifest()
        {
            try
            {
                var document = Document.FromText(
                    this.XmlManifest ?? throw new ArgumentNullException(
                        "Xml manifest is empty."),
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
