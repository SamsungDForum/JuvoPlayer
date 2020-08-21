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
using System.Text;

namespace JuvoPlayer.Subtitles
{
    /// <summary>Represents a single cue block.</summary>
    public class Cue
    {
        private TimeSpan begin;
        private TimeSpan end;

        public string Text { get; set; }

        /// <summary>A timestamp representing the begin time offset of the cue. The time represented by this timestamp
        /// must be less than end time offset of the cue.</summary>
        /// <exception cref="T:System.ArgumentException">Begin time offset is greater than end time offset</exception>
        public TimeSpan Begin
        {
            get => begin;
            set
            {
                if (!VerifyTimeStamps(value, End))
                    throw new ArgumentException("Invalid timestamp range");
                begin = value;
            }
        }

        /// <summary>A timestamp representing the end time offset of the cue. The time represented by this timestamp
        /// must be greater than begin time offset of the cue.</summary>
        /// <exception cref="T:System.ArgumentException">End time offset is less than begin time offset</exception>
        public TimeSpan End
        {
            get => end;
            set
            {
                if (!VerifyTimeStamps(Begin, value))
                    throw new ArgumentException("Invalid timestamp range");
                end = value;
            }
        }

        /// <summary>Compares a given <see cref="T:System.TimeSpan"></see> object with current cue.</summary>
        /// <param name="time">A time to compare</param>
        /// <returns>Returns -1 when given <paramref name="time">time</paramref> is less than Cue's begin.
        /// Returns 1 when given <paramref name="time">time</paramref> is greater than Cue's end.
        /// Otherwise returns 0.</returns>
        public int Compare(TimeSpan time)
        {
            if (time < Begin) return -1;
            if (time >= End) return 1;
            return 0;
        }

        private static bool VerifyTimeStamps(TimeSpan begin, TimeSpan end)
        {
            if (begin == TimeSpan.Zero || end == TimeSpan.Zero)
                return true;
            if (end <= begin)
                return false;
            return begin <= end;
        }
    }
}
