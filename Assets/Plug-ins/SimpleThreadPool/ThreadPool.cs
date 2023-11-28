namespace SimpleThreadPool
{
    using System;
    using System.Linq;
    using System.Collections.Generic;

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
        private Dictionary<int, Handler> _pendingLow = new Dictionary<int, Handler>();
        private Dictionary<int, Handler> _pendingHigh = new Dictionary<int, Handler>();

        public int PendingJobs => _pendingLow.Count + _pendingHigh.Count;
        public int TotalJobs => PendingJobs + BusyWorkerCount;

        private Queue<int> _pendingToRemoveLow = new Queue<int>();
        private Queue<int> _pendingToRemoveHigh = new Queue<int>();

        private Queue<JobHandler> _completedHandlers = new Queue<JobHandler>();

        // Methods

        /// <summary>
        /// Initialization on Awake, sets ProcessorCount and creates Worker instances.
        /// </summary>
        private void Awake()
        {
            ProcessorCount = SystemInfo.processorCount;
            MaxThreads = EnableMultithreading ? ProcessorCount - 1 : 1;

            for (int i = 0; i < MaxThreads; i++)
            {
                Worker worker = new Worker(i, completedHandler => { }, completedJobHandler =>
                {
                    //_completedHandlers.Enqueue(completedJobHandler);
                });

                _workers.Add(worker);
            }
        }

        /// <summary>
        /// Update method to process completed handlers and pending jobs.
        /// </summary>
        private void Update()
        {
            while (_completedHandlers.Count > 0)
                _completedHandlers.Dequeue().Complete();

            foreach (var item in _pendingHigh)
            {
                if (!Handle(item.Value))
                {
                    ClearPendingToRemoveJobs();
                    return;
                }

                _pendingToRemoveHigh.Enqueue(item.Key);
            }

            foreach (var item in _pendingLow)
            {
                if (!Handle(item.Value))
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
            return HandleImpl(new List<Job>() { job }, priority);
        }

        public Handler Handle(Job job, out int identifier, HandlePriority priority = HandlePriority.Low)
        { 
            _handleCounter++;
            identifier = _handleCounter;
            return HandleImpl(new List<Job>() { job }, priority);
        }

        public Handler Handle(Job[] jobs, HandlePriority priority = HandlePriority.Low)
        {  
            _handleCounter++;
            return HandleImpl(jobs.ToList(), priority);
        }

        public Handler Handle(Job[] jobs, out int identifier, HandlePriority priority = HandlePriority.Low)
        {  
            _handleCounter++;
            identifier = _handleCounter;
            return HandleImpl(jobs.ToList(), priority);
        }

        public Handler Handle(List<Job> jobs, HandlePriority priority = HandlePriority.Low)
        { 
            _handleCounter++;
            return HandleImpl(jobs, priority);
        }

        public Handler Handle(List<Job> jobs, out int identifier, HandlePriority priority = HandlePriority.Low)
        {
            _handleCounter++;
            identifier = _handleCounter;
            return HandleImpl(jobs, priority);
        }

        private Handler HandleImpl(List<Job> jobs, HandlePriority priority = HandlePriority.Low)
        {
            Handler handler = new Handler(_handleCounter, jobs);

            if(!Handle(handler))
            {
                // If no more available workers there then add it to pending and break.
                switch (priority)
                {
                    case HandlePriority.Low:
                        _pendingLow.Add(_handleCounter, handler);
                        return handler;

                    case HandlePriority.High:
                        _pendingHigh.Add(_handleCounter, handler);
                        return handler;
                }
            }

            return handler;
        }

        private bool Handle(Handler handler)
        {
            // Loop through job handlers.
            while (handler.Count > 0)
            {
                // Find an available worker.
                Worker worker = GetAvailableWorker();

                if (worker != null)
                {
                    //  If an available worker was found then dequeue a job handler and send
                    //  it to the available worker and do it until no more handlers or workers left.
                    worker.Enqueue(handler);
                }
                else
                    return false;
            }

            if (handler.Priority == HandlePriority.Low)
                _pendingToRemoveLow.Enqueue(handler.Identifier);
            else 
                _pendingToRemoveHigh.Enqueue(handler.Identifier);

            return true;
        }

        private void ClearPendingToRemoveJobs()
        {
            while (_pendingToRemoveHigh.Count > 0)
                _pendingHigh.Remove(_pendingToRemoveHigh.Dequeue());

            while (_pendingToRemoveLow.Count > 0)
                _pendingLow.Remove(_pendingToRemoveLow.Dequeue());
        }

        private Worker GetAvailableWorker()
        {
            for (int i = 0; i < _workers.Count; i++)
                if (_workers[i].CurrentJobHandler == null)
                    return _workers[i];

            return null;
        }

        public void Stop(int identifier)
        {
            if (_pendingLow.ContainsKey(identifier))
                _pendingLow.Remove(identifier);

            if (_pendingHigh.ContainsKey(identifier))
                _pendingHigh.Remove(identifier);

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