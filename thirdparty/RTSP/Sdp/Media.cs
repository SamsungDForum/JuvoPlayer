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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Rtsp.Sdp
{
    public class Media
    {
        private string p;

        public Media(string p)
        {
            // TODO: Complete member initialization
            this.p = p;
        }

        // RFC4566 Media Types
        public enum MediaType { video, audio, text, application, message };

        public Connection Connection { get; set; }

        public Bandwidth Bandwidth { get; set; }

        public EncriptionKey EncriptionKey { get; set; }

        public MediaType GetMediaType()
        {
            if (p.StartsWith("video")) return MediaType.video;
            else if (p.StartsWith("audio")) return MediaType.audio;
            else if (p.StartsWith("text")) return MediaType.text;
            else if (p.StartsWith("application")) return MediaType.application;
            else if (p.StartsWith("message")) return MediaType.message;
            else throw new InvalidDataException();
        }

        private readonly List<Attribut> attributs = new List<Attribut>();

        public IList<Attribut> Attributs
        {
            get
            {
                return attributs;
            }
        }
    }
}
