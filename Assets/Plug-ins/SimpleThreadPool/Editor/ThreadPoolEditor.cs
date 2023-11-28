namespace SimpleThreadPool.Editors
{
    using UnityEngine;

    using UnityEditor;

    using SimpleThreadPool.Internal;

    [CustomEditor(typeof(ThreadPool))]
    public class ThreadPoolEditor : Editor
    {
        private ThreadPool _target;

        // Methods

        private void OnEnable()
        {
            _target = target as ThreadPool;
        }

        public override void OnInspectorGUI()
        {
            Settings();
            Info();
            Threads();

            if (GUI.changed)
                EditorUtility.SetDirty(_target);

            Repaint();
        }

        private void Settings()
        {
            AdvancedGUI.Headline("Settings");
            _target.EnableMultithreading = EditorGUILayout.Toggle("Enable Multithreading", _target.EnableMultithreading);
        }

        private void Info()
        {
            if (!Application.isPlaying)
                return;

            AdvancedGUI.Headline("Info");

            EditorGUILayout.LabelField("Processor Count", _target.ProcessorCount.ToString());
            EditorGUILayout.LabelField("Max Threads", _target.MaxThreads.ToString());
            EditorGUILayout.LabelField("Workers", $"Total: {_target.WorkerCount}  | Sleep: {_target.SleepWorkerCount} | Idle: {_target.IdleWorkerCount} | Busy: {_target.BusyWorkerCount}");
            EditorGUILayout.LabelField("Jobs", $"Total: {_target.TotalJobs} | Pending: {_target.PendingJobs}");
        }

        private void Threads()
        {
            if (_target.WorkerCount > 0)
            {
                AdvancedGUI.HorizontalLine();
                for (int i = 0; i < _target.WorkerCount; i++)
                {
                    Worker.WorkerInfo workerData = _target.GetWorkerData(i);

                    switch (workerData.State)
                    {
                        case WorkerState.Sleeping:
                            GUI.color = Color.gray;
                            break;

                        case WorkerState.Idling:
                            GUI.color = Color.yellow;
                            break;

                        case WorkerState.Working:
                            GUI.color = Color.green;
                            break;

                        default:
                            GUI.color = Color.white;
                            break;
                    }

                    EditorGUILayout.LabelField(workerData.Name);
                    EditorGUI.indentLevel++;

                    if (workerData.State == WorkerState.Sleeping)
                    {
                        EditorGUILayout.LabelField("State", workerData.State.ToString());
                        EditorGUILayout.LabelField("Execution Time", "-");
                    }
                    else if (workerData.State == WorkerState.Idling)
                    {   
                        EditorGUILayout.LabelField("State", workerData.State.ToString());
                        EditorGUILayout.LabelField("Timeout", workerData.Timeout.ToString("0.0") + " s");
                    }
                    else
                    {
                        EditorGUILayout.LabelField("State", workerData.State + $" ({workerData.JobHandler})");
                        EditorGUILayout.LabelField("Execution Time", workerData.HandlerExecutionTime.ToString("0.0") + " s");
                    }
                    EditorGUI.indentLevel--;
                    AdvancedGUI.HorizontalLine();
                }
            }
        }
    }
}