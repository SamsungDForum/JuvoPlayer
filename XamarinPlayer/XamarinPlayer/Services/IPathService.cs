using System;

namespace XamarinPlayer.Services
{
    public interface IPathService : IDisposable
    {
        string ApplicationPath { get; }
    }
}
