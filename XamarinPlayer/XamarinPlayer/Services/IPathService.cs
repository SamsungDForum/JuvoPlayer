using System;
using System.Collections.Generic;
using System.Text;

namespace XamarinPlayer.Services
{
    public interface IPathService : IDisposable
    {
        string ApplicationPath { get; }
    }
}
