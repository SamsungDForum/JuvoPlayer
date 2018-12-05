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

using System;

namespace JuvoPlayer.DataProviders
{
    /// <summary>
    /// Fake ThroughputHistory based on sin function. Useful for testing.
    /// </summary>
    public class SinThroughputHistory : IThroughputHistory
    {
        private double x;
        private readonly double step;
        private readonly double min;
        private readonly double max;

        public SinThroughputHistory(double step, double min, double max)
        {
            this.step = step;
            this.min = min;
            this.max = max;
        }

        public double GetAverageThroughput()
        {
            var y = Math.Sin(x);
            var yScaled = (max - min) * (y + 1) / 2 + min;
            x += step;
            return yScaled;
        }

        public void Push(int sizeInBytes, TimeSpan duration)
        {
        }

        public void Reset()
        {
            x = 0;
        }
    }
}