using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using SimpleJSON;
using System.IO;

namespace Assets.Scripts.AIWorker
{
    public enum Action
    {
        LEFT,
        RIGHT,
        UP,
        DOWN,
        RELEASE,
        SKILL
    }

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

        private int state_height = 120;
        private int state_width = 210;
        private int screen_height = 480;
        private int screen_width = 840;

        private float mouse_x = 0;
        private float mouse_y = 0;

        private int frames = 0;
        private bool skill_used = true;

        private bool NeedAction()
        {
            ABGameWorld world = ABGameWorld.Instance;
            ABBird now_bird = world.GetCurrentBird();

            if (world.IsLevelStable() && !now_bird.IsFlying && !now_bird.IsDying)
            {
                return true;
            }
            else if (now_bird.IsFlying && !skill_used && !now_bird.IsDying)
            {
                return true;
            }
            else
                return false;
        }

        public void WorkOneUpdate()
        {
            if (Camera.main.velocity != Vector3.zero) return;
            frames += 1;
            SaveState();
            //Debug.Log(ABGameWorld.Instance.IsLevelStable() && (ABGameWorld.Instance.GetCurrentBird().IsFlying == false) && (ABGameWorld.Instance.GetCurrentBird().IsDying == false));
            //Debug.Log("Passed frames: " + frames.ToString());
            if (NeedAction())
            {
                Action action = ReadAction();
                DoAction(action);
            }
        }

        public void DoAction(Action action)
        {
            Debug.Log(action);
            ABGameWorld world = ABGameWorld.Instance;
            ABBird now_bird = world.GetCurrentBird();
            float new_mouse_x = mouse_x;
            float new_mouse_y = mouse_y;

            switch (action)
            {
                case Action.LEFT:
                    if (!now_bird.IsFlying && !now_bird.IsDying)
                    {
                        new_mouse_x -= 0.1f;
                        now_bird.DragBird(new Vector3(new_mouse_x, new_mouse_y));
                    }
                    break;

                case Action.RIGHT:
                    if (!now_bird.IsFlying && !now_bird.IsDying)
                    {
                        new_mouse_x += 0.1f;
                        now_bird.DragBird(new Vector3(new_mouse_x, new_mouse_y));
                    }
                    break;

                case Action.UP:
                    if (!now_bird.IsFlying && !now_bird.IsDying)
                    {
                        new_mouse_y += 0.1f;
                        now_bird.DragBird(new Vector3(new_mouse_x, new_mouse_y));
                    }
                    break;

                case Action.DOWN:
                    if (!now_bird.IsFlying && !now_bird.IsDying)
                    {
                        new_mouse_y -= 0.1f;
                        now_bird.DragBird(new Vector3(new_mouse_x, new_mouse_y));
                    }
                    break;

                case Action.RELEASE:
                    if (now_bird && !now_bird.IsFlying && !now_bird.IsDying &&
                        now_bird == ABGameWorld.Instance.GetCurrentBird())
                    {
                        skill_used = false;
                        now_bird.LaunchBird();
                    }
                    break;

                case Action.SKILL:
                    now_bird.SendMessage("SpecialAttack", SendMessageOptions.DontRequireReceiver);
                    if (now_bird && now_bird.IsInFrontOfSlingshot() &&
                        now_bird == ABGameWorld.Instance.GetCurrentBird() &&
                        !now_bird.IsDying && !skill_used)
                    {
                        skill_used = true;
                        now_bird.SendMessage("SpecialAttack", SendMessageOptions.DontRequireReceiver);
                    }
                    break;

                default:
                    break;
            }
        }

        public Action ReadAction()
        {
            string action_file = WorkPath + "/action.txt";
            int action = 0;
            while (!File.Exists(action_file))
            {
                System.Threading.Thread.Sleep(100);
            }
            if (File.Exists(action_file))
            {
                
                using (System.IO.StreamReader file = new System.IO.StreamReader(action_file))
                {
                    string action_str = file.ReadLine();
                    file.Close();
                    //System.IO.File.Delete(action_file);

                    try
                    {
                        action = Convert.ToInt32(action_str);
                    }
                    catch (Exception)
                    {

                    }
                }
            }
            return (Action)action;
        }

        public void SaveState()
        {
            int[,] state = new int[state_height, state_width];

            for (int i = 0; i < state_height; ++i)
            {
                for (int j = 0; j < state_width; ++j)
                {
                    state[i, j] = 0;
                }
            }

            ABGameWorld world = ABGameWorld.Instance;

            Transform _blocksTransform = world.BlocksTransform();
            Transform _plaftformsTransform = world.PlatformsTransform();
            List<ABBird> _birds = world.Birds();

            //draw block
            for (int i = 0; i < _blocksTransform.childCount; i++)
            {

                string name = _blocksTransform.GetChild(i).name.Replace("(Clone)", "");
                if (name.IndexOf("Basic") >= 0 || name.IndexOf("TNT") >= 0) continue;

                string material = _blocksTransform.GetChild(i).GetComponent<ABBlock>()._material.ToString();
                int type = 0;

                if (material.Equals("wood")) type = 31;
                else if (material.Equals("stone")) type = 32;
                else if (material.Equals("ice")) type = 33;

                SetObjectToState(_blocksTransform.GetChild(i).gameObject, ref state, type);
            }
            //draw tnt
            for (int i = 0; i < _blocksTransform.childCount; i++)
            {
                string name = _blocksTransform.GetChild(i).name.Replace("(Clone)", "");
                if (name.IndexOf("TNT") < 0) continue;

                SetObjectToState(_blocksTransform.GetChild(i).gameObject, ref state, 51);
            }
            //draw pig
            for (int i = 0; i < _blocksTransform.childCount; i++)
            {
                string name = _blocksTransform.GetChild(i).name.Replace("(Clone)", "");
                if (name.IndexOf("Basic") < 0) continue;

                SetObjectToState(_blocksTransform.GetChild(i).gameObject, ref state, 21);
            }
            //draw platform
            for (int i = 0; i < _plaftformsTransform.childCount; i++)
            {
                SetObjectToState(_plaftformsTransform.GetChild(i).gameObject, ref state, 41);
            }
            //draw bird
            foreach (ABBird bird in _birds)
            {
                int type = 11;
                if (bird.GetType().ToString().Equals("ABBird")) type = 11;
                else if (bird.GetType().ToString().Equals("ABBirdBlack")) type = 12;
                else if (bird.GetType().ToString().Equals("ABBirdWhite")) type = 13;
                else if (bird.GetType().ToString().Equals("ABBirdYellow")) type = 14;
                else if (bird.GetType().ToString().Equals("ABBBirdBlue")) type = 15;

                SetObjectToState(bird.gameObject, ref state, type);
            }
            //draw slingshot
            //Vector3 sling_pos = _birds[0].transform.position;
            //Vector3 sling_screen_pos = Camera.main.WorldToScreenPoint(sling_pos);
            //int sling_screen_x = (int)(sling_screen_pos.x / 4);
            //int sling_screen_y = (int)((480 - sling_screen_pos.y) / 4);
            Vector3 slingshotPos = ABGameWorld.Instance.Slingshot().transform.position - ABConstants.SLING_SELECT_POS;
            state[PointToState(slingshotPos, false), PointToState(slingshotPos, true)] = 1;

            //SetObjectToState(world.GetCurrentBird().gameObject, ref state, 1);
            mouse_x = _birds[0].transform.position.x;
            mouse_y = _birds[0].transform.position.y;

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(WorkPath + "/state.array"))
            {
                for (int i = 0; i < state_height; ++i)
                {
                    for (int j = 0; j < state_width; ++j)
                    {
                        file.Write(state[i, j]);
                        file.Write(" ");
                    }
                    file.Write("\n");
                }
                file.Close();
            }
        }

        private void SetObjectToState(GameObject gameobject, ref int[,] state, int type)
        {
            Bounds bound = new Bounds();
            BoxCollider2D box = gameobject.GetComponent<BoxCollider2D>();
            PolygonCollider2D polygon = gameobject.GetComponent<PolygonCollider2D>();

            if (box != null)
            {
                bound = box.bounds;
            }
            else if (polygon != null)
            {
                bound = polygon.bounds;
            }

            for (float x = bound.min.x; x < bound.max.x; x += (float)0.01)
            {
                for (float y = bound.min.y; y < bound.max.y; y += (float)0.01)
                {
                    Vector3 point = new Vector3(x, y, bound.center.z);
                    if (bound.Contains(point))
                    {
                        //Vector3 screen_point = Camera.main.WorldToScreenPoint(point);
                        //int screen_x = (int)(screen_point.x/4);
                        //int screen_y = (int)((480-screen_point.y)/4);
                        //state[screen_y, screen_x] = type;
                        state[PointToState(point, false), PointToState(point, true)] = type;
                    }
                }
            }
        }

        private int PointToState(Vector3 point, bool isX)
        {
            Vector3 screen_point = Camera.main.WorldToScreenPoint(point);
            if (isX)
            {
                int x = (int)(screen_point.x * state_width / screen_width);
                if (x >= state_width) return state_width - 1;
                else if (x <= 0) return 0;
                else return x;
            }
            else
            {
                int y = (int)( (screen_height - screen_point.y) * state_height / screen_height);
                if (y >= state_height) return state_height - 1;
                else if (y <= 0) return 0;
                else return y;
            }
        }
    }
}
