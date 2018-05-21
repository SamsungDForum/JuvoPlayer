using Tizen.System;

namespace JuvoPlayer.OpenGL
{
    class Metrics
    {

        private readonly SystemMemoryUsage _systemMemoryUsage = new SystemMemoryUsage();
        private readonly int _systemMemoryUsageGraphId;

        private readonly SystemCpuUsage _systemCpuUsage = new SystemCpuUsage();
        private readonly int _systemCpuUsageGraphId;

        private bool _metricsShown;

        public unsafe Metrics()
        {
            string tag = "MEM";
            fixed (byte* name = ResourceLoader.GetBytes(tag))
                _systemMemoryUsageGraphId = DllImports.AddGraph(name, tag.Length, 0, (float)_systemMemoryUsage.Total / 1024, 100);
            if (_systemMemoryUsageGraphId > DllImports.fpsGraphId)
                DllImports.SetGraphVisibility(_systemMemoryUsageGraphId, _metricsShown ? 1 : 0);

            tag = "CPU";
            fixed (byte* name = ResourceLoader.GetBytes(tag))
                _systemCpuUsageGraphId = DllImports.AddGraph(name, tag.Length, 0, 100, 100);
            if (_systemCpuUsageGraphId > DllImports.fpsGraphId)
                DllImports.SetGraphVisibility(_systemCpuUsageGraphId, _metricsShown ? 1 : 0);

            _metricsShown = false;
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
            if (_systemMemoryUsageGraphId > DllImports.fpsGraphId)
                DllImports.SetGraphVisibility(_systemMemoryUsageGraphId, _metricsShown ? 1 : 0);

            DllImports.SetGraphVisibility(DllImports.fpsGraphId, _metricsShown ? 1 : 0);
            if (_systemCpuUsageGraphId > DllImports.fpsGraphId)
                DllImports.SetGraphVisibility(_systemCpuUsageGraphId, _metricsShown ? 1 : 0);
        }

        public void Update()
        {
            try // updates throw exceptions on faliure
            {
                if (_systemMemoryUsageGraphId > DllImports.fpsGraphId)
                {
                    _systemMemoryUsage.Update();
                    DllImports.UpdateGraphValue(_systemMemoryUsageGraphId, (float) _systemMemoryUsage.Used / 1024);
                }

                if (_systemCpuUsageGraphId > DllImports.fpsGraphId)
                {
                    _systemCpuUsage.Update(); // underlying code is broken - it takes only one sample from /proc/stat, so it's giving average load from system boot till now (like "top -n1" => us + sy + ni)
                    DllImports.UpdateGraphValue(_systemCpuUsageGraphId, (float) (_systemCpuUsage.User + _systemCpuUsage.Nice + _systemCpuUsage.System));
                }
            }
            catch
            {
            }
        }
    }
}
