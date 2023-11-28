namespace SimpleThreadPool.Internal
{
    using System;

    public class JobHandler
    {
        public int Identifier { get; protected set; }
        public string Name { get; protected set; }
        public Job Job { get; private set; }

        private Action _onComplete;

        // Constructors

        public JobHandler(int identifier, Job job, Action onComplete)
        {
            Identifier = identifier;
            Job = job;
            Name = job.Name;
            _onComplete = onComplete;
        }

        // Methods

        public void Execute()
        {
            Job.Action();
        }

        public void Complete()
        {
            Job.Complete();
            _onComplete();
        }
    }
}