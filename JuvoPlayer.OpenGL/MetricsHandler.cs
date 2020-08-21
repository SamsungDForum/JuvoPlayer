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
using System.Collections.Generic;

namespace JuvoPlayer.OpenGL
{
    class MetricsHandler
    {
        public class Metric
        {
            public int Id { get; set; }
            public Func<float> Update { get; set; }
        }
        private readonly List<Metric> _metrics = new List<Metric>();
        private bool _metricsShown;

        public unsafe int AddMetric(string tag, float minimumValue, float maximumValue, int sampleCount, Func<float> getCurrentValue)
        {
            int id;
            fixed (byte* tagBytes = ResourceLoader.GetBytes(tag))
            {
                id = DllImports.AddGraph(new DllImports.GraphData()
                {
                    tag = tagBytes,
                    tagLen = tag.Length,
                    minVal = minimumValue,
                    maxVal = maximumValue,
                    valuesCount = sampleCount
                });
            }

            if (id <= DllImports.FpsGraphId)
                return DllImports.WrongGraphId;
            _metrics.Add(new Metric
            {
                Id = id,
                Update = getCurrentValue
            });
            DllImports.SetGraphVisibility(id, _metricsShown ? 1 : 0);
            return id;
        }

        public void Update()
        {
            foreach (Metric metric in _metrics)
                DllImports.UpdateGraphValue(metric.Id, metric.Update());
        }

        public void UpdateGraphRange(int id, float minimum, float maximum)
        {
            DllImports.UpdateGraphRange(id, minimum, maximum);
        }

        public void SwitchVisibility()
        {
            if (IsShown())
                Hide();
            else
                Show();
        }

        public void Show()
        {
            _metricsShown = true;
            UpdateState();
        }

        public void Hide()
        {
            _metricsShown = false;
            UpdateState();
        }

        public bool IsShown()
        {
            return _metricsShown;
        }

        private void UpdateState()
        {
            DllImports.SetGraphVisibility(DllImports.FpsGraphId, _metricsShown ? 1 : 0);
            foreach(Metric metric in _metrics)
                DllImports.SetGraphVisibility(metric.Id, _metricsShown ? 1 : 0);
            DllImports.SetLogConsoleVisibility(_metricsShown ? 1 : 0);
        }
    }
}
