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

﻿using System.Collections.Generic;
using System.IO;

namespace JuvoPlayer.Subtitles
{
    /// <summary>
    /// A generic subtitle parser interface.
    /// </summary>
    internal interface ISubtitleParser
    {
        /// <summary>
        /// Represents a default encoding.
        /// </summary>
        string DefaultEncoding { get; }

        /// <summary>
        /// Parses subtitle content.
        /// </summary>
        /// <param name="reader">A reader which allows to read subtitle content</param>
        /// <exception cref="JuvoPlayer.Subtitles.SubtitleParserException">A <paramref name="reader"/> has invalid subtitle format</exception>
        /// <returns><see cref="System.Collections.Generic.IEnumerable"/> iterator, which contains parsed <see cref="JuvoPlayer.Subtitles.Cue"></see> cues</returns>
        IEnumerable<Cue> Parse(StreamReader reader);
    }
}
