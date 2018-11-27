using System;
using System.Reactive.Linq;
using System.Threading;

namespace JuvoPlayer.Common
{
    public static class ObservableExtensions
    {
        public static IDisposable Subscribe<T>(this IObservable<T> observable, Action<T> onNext, SynchronizationContext context)
        {
            if (context != null)
                observable = observable.ObserveOn(context);
            return observable.Subscribe(onNext);
        }
    }
}