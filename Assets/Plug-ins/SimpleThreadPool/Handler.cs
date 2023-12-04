namespace SimpleThreadPool
{
    using System.Collections;
    using System.Collections.Concurrent;

    using SimpleThreadPool.Internal;

    public class Handler : IEnumerator
    {
        public int Identifier { get; private set; }
        public HandlePriority Priority { get; private set; }

        private ConcurrentQueue<JobHandler> _handlers;
        private JobHandler _currentHandler;

        public int Count => _handlers.Count;

        private int _handlerCount;
        private int _executedHandlerCount;

        public object Current => _currentHandler;

        private readonly object _lockHandler = new object();
        private readonly object _lockExecutedHandlerCount = new object();

        // Constructors

        public Handler(int identifier, ConcurrentBag<Job> jobs, HandlePriority priority = HandlePriority.Low)
        {
            Identifier = identifier;

            _handlers = new ConcurrentQueue<JobHandler>();

            int index = 0;
            foreach (var job in jobs)
            {
                JobHandler jobHandler = new JobHandler(index, job, () =>
                {
                    lock (_lockExecutedHandlerCount)
                    {
                        _executedHandlerCount++;
                    }
                });

                _handlers.Enqueue(jobHandler);
            }

            _handlerCount = _handlers.Count;
            _executedHandlerCount = 0;

            Priority = priority;
        }

        // Methods

        public JobHandler Dequeue()
        {
            lock (_lockHandler)
            {
                if (_handlers.TryDequeue(out JobHandler jobHandler))
                    _currentHandler = jobHandler;
                return _currentHandler;
            }
        }

        public bool MoveNext()
        {
            return _executedHandlerCount < _handlerCount;
        }

        public void Reset()
        {
            _handlers.Clear();
            _handlerCount = 0;
            _executedHandlerCount = 0;
            _currentHandler = null;
        }
    }
}