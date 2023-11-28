namespace SimpleThreadPool
{
    using System.Collections;
    using System.Collections.Generic;

    using SimpleThreadPool.Internal;

    public class Handler : IEnumerator
    {
        public int Identifier { get; private set; }
        public HandlePriority Priority { get; private set; }

        private Queue<JobHandler> _handlers;
        private JobHandler _currentHandler;

        public int Count => _handlers.Count;

        private int _handlerCount;
        private int _executedHandlerCount;

        public object Current => _currentHandler;

        // Constructors

        public Handler(int identifier, Job job, HandlePriority priority = HandlePriority.Low)
        {
            Identifier = identifier;

            _handlers = new Queue<JobHandler>();
            JobHandler jobHandler = new JobHandler(0, job, () => _executedHandlerCount++);
            _handlers.Enqueue(jobHandler);

            _handlerCount = _handlers.Count;
            _executedHandlerCount = 0;

            Priority = priority;
        }

        public Handler(int identifier, List<Job> jobs, HandlePriority priority = HandlePriority.Low)
        {
            Identifier = identifier;

            _handlers = new Queue<JobHandler>();
            for (int i = jobs.Count - 1, j = 0; i >= 0; i--, j++)
            {
                JobHandler jobHandler = new JobHandler(j, jobs[i], () => _executedHandlerCount++);
                _handlers.Enqueue(jobHandler);
            }

            _handlerCount = _handlers.Count;
            _executedHandlerCount = 0;

            Priority = priority;
        }

        // Methods

        public JobHandler Dequeue()
        {
            _currentHandler = _handlers.Dequeue();
            return _currentHandler;
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