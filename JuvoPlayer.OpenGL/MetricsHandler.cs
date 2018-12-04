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
                id = DllImports.AddGraph(tagBytes, tag.Length, minimumValue, maximumValue, sampleCount);
            if (id <= DllImports.fpsGraphId)
                return DllImports.wrongGraphId;
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
            DllImports.SetGraphVisibility(DllImports.fpsGraphId, _metricsShown ? 1 : 0);
            foreach(Metric metric in _metrics)
                DllImports.SetGraphVisibility(metric.Id, _metricsShown ? 1 : 0);
            DllImports.SetLogConsoleVisibility(_metricsShown ? 1 : 0);
        }
    }
}
