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

﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;

namespace JuvoPlayer.Utils
{
    internal class ResourceLoader
    {
        internal HttpClient HttpClient { get; set; }

        public ResourceLoader()
        {
            HttpClient = new HttpClient();
        }

        public virtual Stream Load(string path)
        {
            if (path.StartsWith("http://") || path.StartsWith("https://"))
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
            var assembly = FindAssembly(resourceName);
            return assembly.GetManifestResourceStream(resourceName);
        }

        internal virtual Assembly FindAssembly(string resourceName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            return assemblies.FirstOrDefault(assembly => !assembly.IsDynamic && assembly.GetManifestResourceStream(resourceName) != null);
        }
    }
}
