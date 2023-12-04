namespace SimpleThreadPool
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;

    using UnityEngine;

    using SimpleThreadPool.Internal;

    /// <summary>
    /// Responsible for managing Worker instances and job handling.
    /// </summary>
    public sealed class ThreadPool : MonoBehaviour
    {
        /// <summary>
        /// Enables or disables multithreading for running more than one background thread.
        /// </summary>
        public bool EnableMultithreading = true;

        /// <summary>
        /// Number of physical processors available.
        /// </summary>
        public int ProcessorCount { get; private set; }

        /// <summary>
        /// Maximum available threads for the ThreadPool based on processor count.
        /// </summary>
        public int MaxThreads { get; private set; }

        /// <summary>
        /// How many threads will be used.
        /// </summary>
        public Performance Performance => _performance;
        [SerializeField] private Performance _performance;

        /// <summary>
        /// List to store Worker instances representing threads in the ThreadPool.
        /// </summary>
        private List<Worker> _workers = new List<Worker>();
        public int WorkerCount => _workers.Count;

        public int SleepWorkerCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _workers.Count; i++)
                    if (_workers[i].CurrentState == WorkerState.Sleeping)
                        count++;
                return count;
            }
        }

        public int IdleWorkerCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _workers.Count; i++)
                    if (_workers[i].CurrentState == WorkerState.Idling)
                        count++;
                return count;
            }
        }

        public int BusyWorkerCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _workers.Count; i++)
                    if (_workers[i].CurrentState == WorkerState.Working)
                        count++;
                return count;
            }
        }

        // Jobs.
        private int _handleCounter = 0;
        private ConcurrentDictionary<int, Handler> _pendingLow = new ConcurrentDictionary<int, Handler>();
        private ConcurrentDictionary<int, Handler> _pendingHigh = new ConcurrentDictionary<int, Handler>();

        public int PendingJobs => _pendingLow.Count + _pendingHigh.Count;
        public int TotalJobs => PendingJobs + BusyWorkerCount;

        private ConcurrentQueue<int> _pendingToRemoveLow = new ConcurrentQueue<int>();
        private ConcurrentQueue<int> _pendingToRemoveHigh = new ConcurrentQueue<int>();

        private ConcurrentQueue<JobHandler> _completedHandlers = new ConcurrentQueue<JobHandler>();

        // Methods

        /// <summary>
        /// Initialization on Awake, sets ProcessorCount and creates Worker instances.
        /// </summary>
        private void Awake()
        {
            // Leave one thread for the system.
            int systemThread = 1;
            // Leave one thread for the main thread.
            int mainThread = 1;

            ProcessorCount = SystemInfo.processorCount - mainThread - systemThread;

            if(EnableMultithreading)
            {
                float percent = Performance switch
                {
                    Performance.Low => 25f,
                    Performance.Medium => 50f,
                    Performance.High => 75f,
                    Performance.Max => 100f,
                    _ => 25f,
                };

                MaxThreads = (int)(percent * ProcessorCount / 100f);

                TryGetThreads(MaxThreads, out int lowPriorityThreads, out int highPriorityThreads);

                int index = 0;
                for (int i = 0; i < lowPriorityThreads; i++)
                {
                    Worker worker = new Worker(index, HandlePriority.Low, completedHandler => { }, completedJobHandler =>
                    {
                        _completedHandlers.Enqueue(completedJobHandler);
                    });

                    _workers.Add(worker);
                    index++;
                }

                for (int i = 0; i < highPriorityThreads; i++)
                {
                    Worker worker = new Worker(index, HandlePriority.High, completedHandler => { }, completedJobHandler =>
                    {
                        _completedHandlers.Enqueue(completedJobHandler);
                    });

                    _workers.Add(worker);
                    index++;
                }
            }
            else
            {
                MaxThreads = 1;
                for (int i = 0; i < MaxThreads; i++)
                {
                    Worker worker = new Worker(i, HandlePriority.Low, completedHandler => { }, completedJobHandler =>
                    {
                        _completedHandlers.Enqueue(completedJobHandler);
                    });

                    _workers.Add(worker);
                }
            }
        }

        public void TryGetThreads(int maxThreads, out int lowPriorityThreads, out int highPriorityThreads)
        {
            if (maxThreads <= 0)
                throw new Exception("The number of threads in the thread pool must be greater than zero.");

            switch (maxThreads)
            {
                case 1:
                    lowPriorityThreads = 1;
                    highPriorityThreads = 0;
                    break;

                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                    highPriorityThreads = 1;
                    lowPriorityThreads = maxThreads - highPriorityThreads;
                    break;

                case 7:
                case 8:
                case 9:
                case 10:
                    highPriorityThreads = 2;
                    lowPriorityThreads = maxThreads - highPriorityThreads;
                    break;

                default:
                    highPriorityThreads = 3;
                    lowPriorityThreads = maxThreads - highPriorityThreads;
                    break;
            }
        }

        /// <summary>
        /// Update method to process completed handlers and pending jobs.
        /// </summary>
        private void Update()
        {
            while (_completedHandlers.Count > 0)
                if(_completedHandlers.TryDequeue(out JobHandler jobHandler))
                    jobHandler.Complete();

            foreach (var item in _pendingHigh)
            {
                if (!HandleWithRemove(item.Value))
                {
                    ClearPendingToRemoveJobs();
                    return;
                }

                _pendingToRemoveHigh.Enqueue(item.Key);
            }

            foreach (var item in _pendingLow)
            {
                if (!HandleWithRemove(item.Value))
                {
                    ClearPendingToRemoveJobs();
                    return;
                }

                _pendingToRemoveLow.Enqueue(item.Key);
            }

            ClearPendingToRemoveJobs();
        }

        /// <summary>
        /// Creates a new job instance with provided action.
        /// </summary>
        public Job Create(string name, Action action)
        {
            Job job = new Job(name, action);
            return job;
        }

        public Handler Handle(Job job, HandlePriority priority = HandlePriority.Low)
        {
            _handleCounter++;
            return HandleImpl(new ConcurrentBag<Job>() { job }, priority);
        }

        public Handler Handle(Job job, out int identifier, HandlePriority priority = HandlePriority.Low)
        { 
            _handleCounter++;
            identifier = _handleCounter;
            return HandleImpl(new ConcurrentBag<Job>() { job }, priority);
        }

        public Handler Handle(Job[] jobs, HandlePriority priority = HandlePriority.Low)
        {  
            _handleCounter++;
            ConcurrentBag<Job> concurrentBug = new ConcurrentBag<Job>();
            for (int i = 0; i < jobs.Length; i++)
                concurrentBug.Add(jobs[i]);
            return HandleImpl(concurrentBug, priority);
        }

        public Handler Handle(Job[] jobs, out int identifier, HandlePriority priority = HandlePriority.Low)
        {  
            _handleCounter++;
            identifier = _handleCounter;
            ConcurrentBag<Job> concurrentBug = new ConcurrentBag<Job>();
            for (int i = 0; i < jobs.Length; i++)
                concurrentBug.Add(jobs[i]);
            return HandleImpl(concurrentBug, priority);
        }

        public Handler Handle(ConcurrentBag<Job> jobs, HandlePriority priority = HandlePriority.Low)
        { 
            _handleCounter++;
            return HandleImpl(jobs, priority);
        }

        public Handler Handle(ConcurrentBag<Job> jobs, out int identifier, HandlePriority priority = HandlePriority.Low)
        {
            _handleCounter++;
            identifier = _handleCounter;
            return HandleImpl(jobs, priority);
        }

        private Handler HandleImpl(ConcurrentBag<Job> jobs, HandlePriority priority = HandlePriority.Low)
        {
            Handler handler = new Handler(_handleCounter, jobs, priority);

            if(!Handle(handler))
            {
                // If no more available workers there then add it to pending and break.
                if (priority == HandlePriority.Low)
                    _pendingLow.TryAdd(_handleCounter, handler);

                else
                    _pendingHigh.TryAdd(_handleCounter, handler);
            }

            return handler;
        }

        private bool Handle(Handler handler)
        {
            // Loop through job handlers.
            while (handler.Count > 0)
            {
                // Find an available worker.
                Worker worker = GetAvailableWorker(handler.Priority);

                if (worker != null)
                {
                    //  If an available worker was found then dequeue a job handler and send
                    //  it to the available worker and do it until no more handlers or workers left.
                    worker.Enqueue(handler);
                }
                else
                    return false;
            }

            return true;
        }

        private bool HandleWithRemove(Handler handler)
        {
            if(!Handle(handler))
                return false;

            if (handler.Priority == HandlePriority.Low)
                _pendingToRemoveLow.Enqueue(handler.Identifier);
            else 
                _pendingToRemoveHigh.Enqueue(handler.Identifier);

            return true;
        }

        private void ClearPendingToRemoveJobs()
        {
            while (_pendingToRemoveHigh.Count > 0)
                if (_pendingToRemoveHigh.TryDequeue(out int result))
                    _pendingHigh.TryRemove(result, out _);

            while (_pendingToRemoveLow.Count > 0)
                if (_pendingToRemoveLow.TryDequeue(out int result))
                    _pendingLow.Remove(result, out _);
        }

        private Worker GetAvailableWorker(HandlePriority priority)
        {
            for (int i = 0; i < _workers.Count; i++)
            {
                Worker worker = _workers[i];
                if (worker.Priority == priority && worker.CurrentJobHandler == null)
                    return worker;
            }

            return null;
        }

        public void Stop(int identifier)
        {
            if (_pendingLow.ContainsKey(identifier))
                _pendingLow.TryRemove(identifier, out _);

            if (_pendingHigh.ContainsKey(identifier))
                _pendingHigh.TryRemove(identifier, out _);

            for (int i = 0; i < _workers.Count; i++)
            {
                Worker worker = _workers[i];
                if (worker.CurrentJobHandler != null && worker.CurrentHandler.Identifier == identifier)
                    worker.Dequeue();
            }
        }

        public Worker.WorkerInfo GetWorkerData(int index)
        {
            return _workers[index].GetWorkerInfo();
        }

        /// <summary>
        /// Cleans up pending jobs and worker instances on disabling the ThreadPool.
        /// </summary>
        private void OnDisable()
        {
            _pendingLow.Clear();
            _pendingHigh.Clear();
            for (int i = 0; i < _workers.Count; i++)
                _workers[i].Stop();
            _workers.Clear();
            _pendingToRemoveLow.Clear();
            _pendingToRemoveHigh.Clear();
            _completedHandlers.Clear();
        }
    }
}