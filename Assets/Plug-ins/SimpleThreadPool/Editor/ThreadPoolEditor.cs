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
            serializedObject.Update();

            Settings();
            Info();
            Threads();

            if (GUI.changed)
                EditorUtility.SetDirty(_target);

            Repaint();

            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
        }

        private void Settings()
        {
            AdvancedGUI.Headline("Settings");
            _target.EnableMultithreading = EditorGUILayout.Toggle("Enable Multithreading", _target.EnableMultithreading);

            if (_target.EnableMultithreading)
            {
                SerializedProperty performance = serializedObject.FindProperty("_performance");
                EditorGUILayout.PropertyField(performance, true);

                // Leave one thread for the system.
                int systemThread = 1;
                // Leave one thread for the main thread.
                int mainThread = 1;

                int processorCount = SystemInfo.processorCount - mainThread - systemThread;

                float percent = _target.Performance switch
                {
                    Performance.Low => 25f,
                    Performance.Medium => 50f,
                    Performance.High => 75f,
                    Performance.Max => 100f,
                    _ => 25f,
                };

                int maxThreads = (int)(percent * processorCount / 100f);
                _target.TryGetThreads(maxThreads, out int lowPriorityThreads, out int highPriorityThreads);

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Total Threads", SystemInfo.processorCount.ToString());
                EditorGUILayout.LabelField("System Thread", "1");
                EditorGUILayout.LabelField("Main Thread", "1");
                EditorGUILayout.LabelField("Low Priority Threads", lowPriorityThreads.ToString());
                EditorGUILayout.LabelField("High Priority Threads", highPriorityThreads.ToString());
                EditorGUI.indentLevel--;
            }
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