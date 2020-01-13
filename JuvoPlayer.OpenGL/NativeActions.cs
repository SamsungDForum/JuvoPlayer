using System;
using System.Collections.Generic;

namespace JuvoPlayer.OpenGL
{
    class NativeActions
    {
        private static NativeActions Instance;

        public static NativeActions GetInstance()
        {
            return Instance ?? (Instance = new NativeActions());
        }

        private readonly Queue<Action> _actions = new Queue<Action>();

        public void Execute()
        {
            while (_actions.Count > 0)
                _actions.Dequeue().Invoke();
        }

        public void Enqueue(Action action)
        {
            _actions.Enqueue(action);
        }
    }
}
