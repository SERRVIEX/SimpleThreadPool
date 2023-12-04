#define SIMPLE_THREAD_POOL_DEBUG

namespace SimpleThreadPool.Internal
{
    using System;
    using System.Threading;
    using System.Diagnostics;

    using UnityEngine.Profiling;

    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Represents a Worker class responsible for handling jobs using threads.
    /// </summary>
    public sealed class Worker
    {
        private int _index;
        private Thread _thread;
        public HandlePriority Priority { get; private set; }

        public WorkerState CurrentState { get; private set; }

        /// <summary>
        ///  Timeout (ms) for going sleep if no jobs to execute.
        /// </summary>
        private float _timeout = 10000;

        public Handler CurrentHandler { get; private set; } 
        public JobHandler CurrentJobHandler { get; private set; }

        private Action<Handler> _onCompleteHandler;
        private Action<JobHandler> _onCompleteJobHandler;

        /// <summary>
        /// Used for detect timeout.
        /// </summary>
        private Stopwatch _threadTimeoutStopwatch;

        /// <summary>
        /// Used for measure execution time.
        /// </summary>
        private Stopwatch _jobHandlerStopwatch;

        private readonly object _lockWorker = new object();
        private readonly object _lockThreadStopwatches = new object();

        // Constructors

        public Worker(int index, HandlePriority priority, Action<Handler> onCompleteHandler, Action<JobHandler> onCompleteJobHandler)
        {
            _index = index;
            Priority = priority;
            _onCompleteHandler = onCompleteHandler;
            _onCompleteJobHandler = onCompleteJobHandler;

            CurrentState = WorkerState.Sleeping;

            _threadTimeoutStopwatch = new Stopwatch();
            _jobHandlerStopwatch = new Stopwatch();
        }

        // Methods

        /// <summary>
        /// Handles a new job by assigning it to the worker.
        /// </summary>
        public void Enqueue(Handler handler)
        {
            CurrentHandler = handler;
            CurrentJobHandler = handler.Dequeue();

            CurrentState = WorkerState.Working;

            // If the thread doesn't exist or it is not alive then create new one.
            if (_thread == null || !_thread.IsAlive)
            {
                _thread = new Thread(Update);
                _thread.Start();
            }
        }

        /// <summary>
        /// Stops the current handler and restarts the thread.
        /// </summary>
        public void Dequeue()
        {
            if (CurrentHandler == null)
                return;

            CurrentState = WorkerState.Idling;

            try
            {
                _thread?.Abort();
            }
            catch
            {
                // If an exception occurs when aborting the thread, log the message.
                Debug.Log($"Handler has stopped in the thread '{_index}'.");
            }

            // Reset the current handlers.
            CurrentHandler = null;
            CurrentJobHandler = null;

            // Restart the thread.
            if (_thread == null)
                _thread = new Thread(Update);

            _thread.Start();
        }

        // <summary>
        /// Main method for the worker, handles job execution and timeout logic.
        /// </summary>
        private void Update()
        {
            int milliseconds = 16;
            while (true)
            {
                lock (_lockWorker)
                {
                    if (CurrentJobHandler != null)
                    {
#if SIMPLE_THREAD_POOL_DEBUG
                    var profilerSample = CustomSampler.Create(CurrentJobHandler.Job.Name);
                    Profiler.BeginThreadProfiling("SimpleThreadPool", "Worker " + _index);
                    profilerSample.Begin();
#endif

                        try
                        {
                            _jobHandlerStopwatch.Reset();
                            _jobHandlerStopwatch.Start();
                            CurrentJobHandler.Execute();
                            CurrentJobHandler.Complete();
                            _jobHandlerStopwatch.Stop();

                            _onCompleteJobHandler.Invoke(CurrentJobHandler);
                            if (CurrentHandler.Count == 0)
                                _onCompleteHandler.Invoke(CurrentHandler);

                            CurrentHandler = null;
                            CurrentJobHandler = null;

                            // After executing a job, timeout should be started.
                            _threadTimeoutStopwatch.Reset();
                            _threadTimeoutStopwatch.Start();

                            CurrentState = WorkerState.Idling;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError(ex);
                        }

#if SIMPLE_THREAD_POOL_DEBUG
                    profilerSample.End();
                    Profiler.EndThreadProfiling();
#endif
                    }
                    else
                    {
                        lock (_lockThreadStopwatches)
                        {
                            // If the thread is running and no job was
                            // executed within timeout range, then stop it.
                            if (_threadTimeoutStopwatch.Elapsed.TotalMilliseconds >= _timeout)
                            {
                                Sleep();
                                return;
                            }
                        }
                    }

                    Thread.Sleep(milliseconds);
                }
            }
        }

        /// <summary>
        /// Puts the worker thread to sleep.
        /// </summary>
        private void Sleep()
        {
            lock (_lockWorker)
            {
                CurrentState = WorkerState.Sleeping;

                // Stop the thread
                try { _thread?.Abort(); }
                catch { }

                _thread = null;

                // Reset the current job handler.
                CurrentHandler = null;
                CurrentJobHandler = null;

                _jobHandlerStopwatch.Stop();
                _threadTimeoutStopwatch.Stop();
            }
        }

        /// <summary>
        /// Stops the worker thread without logging any exceptions.
        /// </summary>
        public void Stop()
        {
            CurrentState = WorkerState.Sleeping;

            // Don't need to log errors in console when the
            // ThreadPool is disabled or the application is terminated.
            try { _thread?.Abort(); }
            catch { }

            _thread = null;

            // Reset the current job handler.
            CurrentHandler = null;
            CurrentJobHandler = null;

            _jobHandlerStopwatch.Stop();
            _threadTimeoutStopwatch.Stop();
        }

        /// <summary>
        /// Gets information about the worker.
        /// </summary>
        public WorkerInfo GetWorkerInfo()
        {
            string name = $"Worker {_index} ({Priority})";
            string jobHandler = CurrentJobHandler != null ? CurrentJobHandler.Name : null;
            return new WorkerInfo(name, CurrentState, _threadTimeoutStopwatch.Elapsed.TotalSeconds, jobHandler, _jobHandlerStopwatch.Elapsed.TotalSeconds);
        }

        // Other

        public struct WorkerInfo
        {
            public string Name;
            public WorkerState State;
            public double Timeout;
            public string JobHandler;
            public double HandlerExecutionTime;

            // Methods

            public WorkerInfo(string name, WorkerState state, double timeout, string handler, double handlerExecutionTime)
            {
                Name = name;
                State = state;
                Timeout = timeout;
                JobHandler = handler;
                HandlerExecutionTime = handlerExecutionTime;
            }
        }
    }
}