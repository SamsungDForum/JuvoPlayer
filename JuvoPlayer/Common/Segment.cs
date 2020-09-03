/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2020, Samsung Electronics Co., Ltd
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
 *
 */

using System;

namespace JuvoPlayer.Common
{
    public struct Segment
    {
        public TimeSpan Base { get; set; }

        public TimeSpan Start { get; set; }

        // TODO: Change Stop to nullable TimeSpan
        public TimeSpan Stop { get; set; }

        public TimeSpan ToClockTime(TimeSpan position)
        {
            if (Stop != TimeSpan.MinValue && position > Stop)
            {
                throw new InvalidOperationException(
                    $"{nameof(position)} ({position}) is greater than {nameof(Stop)} ({Stop})");
            }

            return Base - Start + position;
        }

        public TimeSpan ToPlaybackTime(TimeSpan clockPosition)
        {
            return clockPosition - Base + Start;
        }

        public override string ToString()
        {
            return $"{nameof(Base)}: {Base}, {nameof(Start)}: {Start}, {nameof(Stop)}: {Stop}";
        }
    }
}
