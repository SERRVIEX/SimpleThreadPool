namespace SimpleThreadPool
{
    using System;

    public sealed class Job
    {
        public string Name { get; private set; }
        public Action Action { get; private set; }

        public bool IsCompleted { get; private set; }
        public Action OnComplete;

        // Constructors

        public Job(string name, Action action)
        {
            Name = name;
            Action = action;
        }

        // Methods

        public void Complete()
        {
            IsCompleted = true;
            OnComplete?.Invoke();
        }
    }
}