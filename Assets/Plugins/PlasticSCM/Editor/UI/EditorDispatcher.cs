﻿using System;
using System.Collections.Generic;
using System.Threading;

using UnityEditor;

namespace Codice.UI
{
    [InitializeOnLoad]
    internal static class EditorDispatcher
    {
        static EditorDispatcher()
        {
            mMainThread = Thread.CurrentThread;
        }

        internal static bool IsOnMainThread
        {
            get { return Thread.CurrentThread == mMainThread; } 
        }

        internal static void Dispatch(Action task)
        {
            lock (mDispatchQueue)
            {
                if (mDispatchQueue.Count == 0)
                    EditorApplication.update += Update;

                mDispatchQueue.Enqueue(task);
            }
        }

        internal static void Update()
        {
            Action[] actions;

            lock (mDispatchQueue)
            {
                if (mDispatchQueue.Count == 0)
                    return;

                actions = mDispatchQueue.ToArray();
                mDispatchQueue.Clear();

                EditorApplication.update -= Update;
            }

            foreach (Action action in actions)
                action();
        }

        static readonly Queue<Action> mDispatchQueue = new Queue<Action>();
        static Thread mMainThread;
    }
}
