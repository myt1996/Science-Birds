using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using SimpleJSON;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;

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

        private static string host = "127.0.0.1";
        private static int port = 9999;
        private static Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        public static void Start()
        {
            ConnectToServer();
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

            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].Equals("-p"))
                {
                    if (System.IO.Directory.Exists(arguments[i + 1]))
                    {
                        SystemWorker.worker_path = arguments[i + 1];
                        Log("Using work path: " + arguments[i + 1]);
                        i = i + 1;
                    }
                    else Log("Wrong work path: " + arguments[i + 1] + ", use exe path insteaded");
                }
                else if (arguments[i].Equals("-l"))
                {
                    Log(arguments[i + 1]);
                    int level_index = -1;
                    try
                    {
                        level_index = Convert.ToInt32(arguments[i + 1]) - 1;
                    }
                    catch (Exception)
                    {
                        Log("Wrong level index: " + arguments[i + 1]);
                    }

                    if (level_index != -1)
                    {
                        ABSceneManager.Instance.LoadScene("LevelSelectMenu");
                        Log("Jump to level " + arguments[i + 1]);
                        i = i + 1;
                    }
                    else Log("Wrong level argument " + arguments[i + 1]);
                }
                else if (i!=0)
                {
                    Log("Wrong arguments: " + arguments[i]);
                }
            }
        }

        ///<summary>
        ///Connect to server
        ///</summary>
        public static void ConnectToServer()
        {
            Log("connect to server");
            try
            {
                client.Connect(new IPEndPoint(IPAddress.Parse(host), port));
            }
            catch (Exception e)
            {
                Log(e.Message);
                return;
            }

            string welcome = Recieve();
            Log("New message from server: " + welcome);

            Send("ok");
        }

        ///<summary>
        ///Recieve one send
        ///</summary>
        public static string Recieve()
        {
            Log("Recieving");
            string data = "";
            // Recieve header
            var header = new byte[4];
            var header_count = client.Receive(header);
            int length = BitConverter.ToInt32(header, 0);
            var total = 0; // total bytes to received
            var dataleft = length; // bytes that havend been received 
            var bytes = new byte[length];
            // 1. check if the total bytes that are received < than the size you've send before to the server.
            // 2. if true read the bytes that have not been receive jet
            while (total < length)
            {
                // receive bytes in byte array data[]
                // from position of total received and if the case data that havend been received.
                var recv = client.Receive(bytes, total, dataleft, SocketFlags.None);
                if (recv == 0) // if received data = 0 than stop reseaving
                {
                    bytes = null;
                    break;
                }
                total += recv;  // total bytes read + bytes that are received
                dataleft -= recv; // bytes that havend been received
            }
            data = Encoding.UTF8.GetString(bytes, 0, length);
            return data;
        }

        ///<summary>
        ///Send 
        ///</summary>
        public static int Send(string str)
        {
            int length = -1;

            length = str.Length;
            byte[] header = BitConverter.GetBytes(length);
            byte[] body = Encoding.UTF8.GetBytes(str);

            byte[] ret = new byte[header.Length + body.Length];
            Buffer.BlockCopy(header, 0, ret, 0, header.Length);
            Buffer.BlockCopy(body, 0, ret, header.Length, body.Length);

            client.Send(ret);

            return length;
        }

        ///<summary>
        ///Save log 
        ///</summary>
        public static void Log(string str)
        {
            string path = worker_path + "/log.txt";
            // This text is added only once to the file.
            if (!File.Exists(path))
            {
                // Create a file to write to.
                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine(str);
                }
            }
            else
            {
                // This text is always added, making the file longer over time
                // if it is not deleted.
                using (StreamWriter sw = File.AppendText(path))
                {
                    sw.WriteLine(str);
                }
            }

            
        }
    }

    [Serializable]
    public class StateUpload
    {
        public string request_name = "STATEUPLOAD";
        public string state_path;
        public bool return_action;
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
            //Log(ABGameWorld.Instance.IsLevelStable() && (ABGameWorld.Instance.GetCurrentBird().IsFlying == false) && (ABGameWorld.Instance.GetCurrentBird().IsDying == false));
            //Log("Passed frames: " + frames.ToString());
            if (NeedAction())
            {
                Action action = ReadAction();
                DoAction(action);
            }
        }

        public void DoAction(Action action)
        {
            SystemWorker.Log(action.ToString());
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
            string str = SystemWorker.Recieve();

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
            SystemWorker.Log("Action: "+action.ToString());
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

            DirectoryInfo states_info = new DirectoryInfo(WorkPath + "/Data/States");
            int states_count = states_info.GetFiles().Length;
            string filename = states_info.FullName + "/" + (states_count+1).ToString() + ".array";
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(filename))
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
            SystemWorker.Log("State: " + filename);

            StateUpload state_upload = new StateUpload();
            state_upload.state_path = filename;
            state_upload.return_action = NeedAction();
            string json = JsonUtility.ToJson(state_upload);
            SystemWorker.Send(json);
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
