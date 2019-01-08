using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.AIWorker
{
    public static class SystemWorker
    {
        private static List<LevelWorker> level_workers = new List<LevelWorker>();
        private static LevelWorker cur_level_worker;
        public static string worker_path = System.IO.Path.GetFullPath(".");


        public static void Start()
        {
            ParseRunArguments();
        }

        public static LevelWorker StartOneLevel(int level_index)
        {
            LevelWorker lw = new LevelWorker(level_index, worker_path);
            level_workers.Add(lw);
            cur_level_worker = level_workers[level_workers.Count - 1];
            return cur_level_worker;
        }

        ///<summary>
        ///Parse the arguments of running exe file.
        ///Arguments can be: -p work_path ; -l level_auto_start
        ///</summary>
        public static void ParseRunArguments()
        {
            String[] arguments = Environment.GetCommandLineArgs();

            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (arguments[i].Equals("-p"))
                {
                    if (System.IO.Directory.Exists(arguments[i + 1])) SystemWorker.worker_path = arguments[i + 1];
                    else Debug.Log("Wrong work path: " + arguments[i + 1] + ", use exe path insteaded");
                }
                else if (arguments[i].Equals("-l"))
                {
                    int level_index = -1;
                    try
                    {
                        level_index = Convert.ToInt32(arguments[i + 1]) - 1;
                    }
                    catch (Exception)
                    {
                        Debug.Log("Wrong level index: " + arguments[i + 1]);
                    }

                    if (level_index != -1)
                    {
                        LevelList.Instance.SetLevel(level_index);
                        ABSceneManager.Instance.LoadScene("GameWorld");
                    }
                }
                else
                {
                    Debug.Log("Wrong arguments: " + arguments[i]);
                }
            }
        }
    }

    public class LevelWorker
    {
        public LevelWorker(int level_index, string worker_path = "")
        {
            LevelIndex = level_index;

            if (worker_path.Equals("")) WorkPath = SystemWorker.worker_path;
            else WorkPath = worker_path;
        }

        public int LevelIndex { get; private set; }
        public string WorkPath { get; private set; }

        private int frames = 0;

        public void WorkOneUpdate()
        {
            frames += 1;
            Debug.Log("Passed frames: " + frames.ToString());
        }
    }
}
