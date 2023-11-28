namespace SimpleThreadPool
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;

    public class Test : MonoBehaviour
    {
        [SerializeField] private ThreadPool _threadPool;

        private IEnumerator Start()
        {
            for (int i = 0; i < 1; i++)
            {

                Action a1 = () =>
                {
                    Thread.Sleep(1000);
                    //Debug.Log("a1");
                };

                Action a2 = () =>
                {
                    Thread.Sleep(2000); 
                    //Debug.Log("a2");
                };

                Action a3 = () =>
                {
                    Thread.Sleep(3000);
                    //Debug.Log("a3");
                };

                Job a1Job = _threadPool.Create("n10",  a1);
                Job a2Job = _threadPool.Create("n100", a2);
                Job a3Job = _threadPool.Create("n1000", a3);
                List<Job> jobs = new List<Job>();   
                jobs.Add(a1Job);
                jobs.Add(a2Job);
                jobs.Add(a3Job);
                yield return _threadPool.Handle(jobs);

                //yield return _threadPool.Handle(a1Job);
                //yield return _threadPool.Handle(a2Job);
                //yield return _threadPool.Handle(a3Job);
            }

            yield return new WaitForSeconds(4);

            Action b1 = () =>
            {
                Thread.Sleep(3000);
                Debug.Log("b1");
            };

            Job b1J = _threadPool.Create("BBBSKA", b1);

            yield return _threadPool.Handle(b1J);

            _threadPool.Handle(b1J);
            _threadPool.Handle(b1J);
            _threadPool.Handle(b1J);
            _threadPool.Handle(b1J);
            _threadPool.Handle(b1J);

            _threadPool.Handle(b1J);
            _threadPool.Handle(b1J);
            _threadPool.Handle(b1J);
            _threadPool.Handle(b1J);
            _threadPool.Handle(b1J);
        }
    }
}