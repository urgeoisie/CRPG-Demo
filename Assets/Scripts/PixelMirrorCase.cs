
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MirrorCase2D
{
    public sealed class PixelMirrorCase : MonoBehaviour
    {
        private const float RoomW = 16f;
        private const float RoomH = 10f;
        private const float Gap = 40f;
        private static bool built;
        private static Sprite pixel;

        private readonly Dictionary<string, Vector2> rooms = new Dictionary<string, Vector2>();
        private readonly List<Hotspot> hotspots = new List<Hotspot>();
        private readonly List<string> evidence = new List<string>();
        private readonly HashSet<string> flags = new HashSet<string>();
        private Transform player;
        private Camera cam;
        private string room = "unknown";
        private Hotspot hover;
        private Hotspot near;
        private Dialogue dialogue;
        private string[] pages = Array.Empty<string>();
        private int page;
        private bool mainMenu = true;
        private bool pause;
        private bool evidenceOpen;
        private int slotMode;
        private string message = "";
        private Vector2 evidenceScroll;
        private GUIStyle titleStyle, bodyStyle, buttonStyle, panelStyle, smallStyle;
        private Texture2D panelTex, buttonTex, blackTex;
        private Font font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            built = false;
            pixel = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (built) return;
            built = true;
            // Scene-authored version: the map and props are saved in the Unity scene.
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.antiAliasing = 0;
            Time.timeScale = 0f;
            foreach (Camera c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(c.gameObject);
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);
            BuildWorld();
            MoveTo("unknown", new Vector2(-5.2f, -2.3f));
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void BuildWorld()
        {
            cam = new GameObject("Pixel Camera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0, 0, -10);

            GameObject po = new GameObject("Player");
            player = po.transform;
            Rect(po.transform, "coat", Vector2.zero, new Vector2(.62f,.78f), C(22,25,28), 80);
            Rect(po.transform, "head", new Vector2(0,.52f), new Vector2(.46f,.38f), C(35,38,41), 81);

            BuildUnknown(O("unknown", 0));
            BuildHospital(O("hospital", 1));
            BuildPolice(O("police", 2));
            BuildBedroom(O("bedroom", 3));
            BuildCorridor(O("corridor", 4));
            BuildMirror(O("mirror", 5));
            BuildBriefing(O("briefing", 6));
            BuildCrime(O("crime", 7), "现实客厅：空间痕迹");
            BuildMissing(O("missing", 8));
            BuildRelation(O("relation", 9));
        }

        private Vector2 O(string id, int index)
        {
            Vector2 o = new Vector2(index * Gap, 0);
            rooms[id] = o;
            return o;
        }

        private void BuildUnknown(Vector2 o)
        {
            Room(o, "未知之地", C(232,235,229), C(12,12,13));
            Obj(o+new Vector2(-6.9f,0), new Vector2(.45f,8.8f), C(2,2,3), 8, "黑色高墙");
            Obj(o+new Vector2(2.7f,.1f), new Vector2(1.8f,6.6f), C(246,247,240), 12, "白塔塔身");
            Obj(o+new Vector2(2.7f,3.75f), new Vector2(1.15f,.65f), C(235,238,232), 13, "白塔塔冠");
            Obj(o+new Vector2(2.7f,-3.35f), new Vector2(2.8f,.45f), C(213,218,212), 12, "白塔基座");
            Obj(o+new Vector2(4.35f,-1.1f), new Vector2(.55f,1.45f), C(28,30,34), 20, "撕裂的门");
            Obj(o+new Vector2(-1.2f,-1.15f), new Vector2(.68f,.82f), C(226,204,184), 22, "另一个你");
            Obj(o+new Vector2(-1.2f,-.55f), new Vector2(.46f,.42f), C(215,193,174), 23, "另一个你头部");
            TileMarks(o, C(220,223,217));
            Add("塔前的门", o+new Vector2(4.35f,-1.1f), new Vector2(1.1f,1.8f), new Dialogue("未知之地", T("PrologueMemory"), Choice.Go("进入门后的天空", "你靠近那道被撕开的门。下一秒，脚下只剩下坠落。", "hospital", new Vector2(-4.6f,-2.6f))), "E 进入");
            Add("另一个你", o+new Vector2(-1.2f,-.85f), new Vector2(1.2f,1.7f), new Dialogue("另一个你", "他看着你，像看着一件无法理解的事物。\n\n“你是我？”\n\n你的喉咙干涩得无法发声，只能点头。"), "E 交谈");
        }

        private void BuildHospital(Vector2 o)
        {
            Room(o, "病房", C(198,215,218), C(164,185,189));
            Bed(o+new Vector2(-3.8f,-1.0f), C(230,237,239), C(128,157,164));
            Obj(o+new Vector2(-5.8f,1.5f), new Vector2(1.3f,1.4f), C(26,40,38), 18, "心电仪");
            Obj(o+new Vector2(-5.8f,1.5f), new Vector2(.92f,.12f), C(64,210,142), 19, "心电线");
            Obj(o+new Vector2(4.7f,1.3f), new Vector2(1.6f,2.1f), C(174,184,184), 14, "储物柜");
            Obj(o+new Vector2(1.1f,3.65f), new Vector2(2.4f,.7f), C(231,234,220), 15, "墙上病历板");
            Obj(o+new Vector2(2.6f,-.4f), new Vector2(.56f,.84f), C(232,235,238), 35, "医生身体");
            Obj(o+new Vector2(2.6f,.18f), new Vector2(.42f,.38f), C(214,191,172), 36, "医生头部");
            Scatter(o, C(181,201,204), 16);
            Add("病历", o+new Vector2(.85f,3.55f), new Vector2(2.7f,.9f), new Dialogue("病历", T("HospitalRecord"), Choice.Ev("记住涂黑姓名", "你记住了那条黑线。它不像隐藏姓名，更像替你划掉某种责任。", "病历姓名被涂黑，身份信息被系统性回避。")), "E 查看");
            Add("医生", o+new Vector2(2.6f,-.2f), new Vector2(1.2f,1.8f), new Dialogue("医生", T("DoctorAwakening"), Choice.Go("下地离开病房", "你扶着床沿站起，身体没有想象中虚弱。", "police", new Vector2(-4.7f,-2.4f))), "E 交谈");
        }

        private void BuildPolice(Vector2 o)
        {
            Room(o, "警局档案室", C(66,72,76), C(43,48,52));
            Desk(o, C(56,42,34));
            Obj(o+new Vector2(-2.2f,.35f), new Vector2(1.1f,.72f), C(224,207,169), 30, "卷宗堆");
            Obj(o+new Vector2(2f,.25f), new Vector2(1.2f,.64f), C(196,171,126), 30, "证据链堆");
            Obj(o+new Vector2(0,3.65f), new Vector2(4.2f,.9f), C(92,70,48), 18, "线索板");
            Shelves(o); Scatter(o, C(82,88,92), 20);
            Add("卷宗", o+new Vector2(-2.2f,.35f), new Vector2(1.4f,1f), new Dialogue("卷宗", T("CaseFileInitial"), Choice.Go("整理案件，进入夜晚卧室", "卷宗合上时，灯光像被纸页吸走。夜幕悄然降临。", "bedroom", new Vector2(-4.3f,-2.2f))), "E 查看");
            Add("证据链", o+new Vector2(2f,.25f), new Vector2(1.5f,1f), new Dialogue("证据链", T("EvidenceDesk"), Choice.Ev("加入证据", "你把“过于完整的证据链”记入当前掌握的证据。", "证据链完整得异常。")), "E 查看");
        }

        private void BuildBedroom(Vector2 o)
        {
            Room(o, "卧室", C(37,34,39), C(27,25,29));
            Bed(o+new Vector2(-2.6f,-.8f), C(93,96,110), C(44,42,50));
            Obj(o+new Vector2(1.8f,3.45f), new Vector2(3.1f,.9f), C(9,14,21), 18, "雨夜窗户");
            Obj(o+new Vector2(4.8f,-1f), new Vector2(1.1f,1.4f), C(50,38,32), 18, "床头柜");
            Obj(o+new Vector2(4.8f,-.16f), new Vector2(.55f,.42f), C(166,154,118), 21, "台灯");
            Scatter(o, C(47,44,51), 16);
            Add("床", o+new Vector2(-2.6f,-.8f), new Vector2(3.6f,2f), new Dialogue("床", T("DreamBedroomNote"), Choice.Go("闭上眼", "雨点与心跳完全重叠。卧室的边界消失。", "corridor", new Vector2(-6.2f,-2.2f))), "E 入睡");
        }

        private void BuildCorridor(Vector2 o)
        {
            Room(o, "第一夜走廊", C(30,31,34), C(11,12,14));
            for (int i=0;i<7;i++){float x=-6+i*2; Obj(o+new Vector2(x,2.7f), new Vector2(.88f,1.55f), C(45,45,48), 18, "重复的门201"); Obj(o+new Vector2(x,3.4f), new Vector2(.52f,.18f), C(130,132,132), 19, "201门牌");}
            Obj(o+new Vector2(0,-.6f), new Vector2(9.6f,.38f), C(52,56,61), 6, "潮湿反光");
            Obj(o+new Vector2(6.2f,-2.15f), new Vector2(1.1f,1.8f), C(35,39,44), 25, "走廊尽头的门");
            Add("走廊", o+new Vector2(-2,.2f), new Vector2(4.8f,3.8f), new Dialogue("第一夜走廊", T("FirstDreamCorridor")), "E 倾听");
            Add("门", o+new Vector2(6.2f,-2.15f), new Vector2(1.4f,1.9f), new Dialogue("走廊尽头的门", "门缝里没有光。你却能听见门后有一间灰白色的房间，像记忆正在里面屏住呼吸。", Choice.Go("推门进入", "门轴发出玻璃一样的裂响，城市也荡然无存。", "mirror", new Vector2(-4.8f,-2.2f))), "E 进入");
        }

        private void BuildMirror(Vector2 o)
        {
            Room(o, "灰白客厅", C(156,156,151), C(118,118,114));
            Living(o, true);
            Obj(o+new Vector2(2.2f,-.15f), new Vector2(.62f,.94f), C(225,225,220), 35, "看不清脸的人身体");
            Obj(o+new Vector2(2.2f,.52f), new Vector2(.48f,.38f), C(42,42,42), 36, "模糊的脸");
            Add("灰白客厅", o+new Vector2(-.8f,-.7f), new Vector2(6.2f,3.1f), new Dialogue("灰白客厅", T("MirrorDreamLivingRoom"), Choice.Go("醒来并整理卷宗", "现实中的客厅也在等待。", "briefing", new Vector2(-4.8f,-2.3f))), "E 调查");
            Add("看不清脸的人", o+new Vector2(2.2f,.1f), new Vector2(1.4f,1.9f), new Dialogue("看不清脸的人", T("FacelessPerson"), Choice.Ev("记住他的话", "“名声不是你的一切。”这句话留在证据之外，却留在你身体里。", "灰白梦境中出现看不清脸的人，他指向被抹去的关系。")), "E 交谈");
        }

        private void BuildBriefing(Vector2 o)
        {
            Room(o, "警局调查室", C(66,72,76), C(43,48,52));
            Desk(o, C(56,42,34)); Shelves(o); Scatter(o, C(82,88,92), 18);
            Obj(o+new Vector2(0,.4f), new Vector2(1.3f,.82f), C(224,207,169), 30, "唯一卷宗");
            Obj(o+new Vector2(6.9f,-.4f), new Vector2(.55f,2.4f), C(26,30,34), 18, "保留的门");
            Add("整理后的卷宗", o+new Vector2(0,.35f), new Vector2(1.8f,1.1f), new Dialogue("整理后的卷宗", T("InvestigationBriefingFile"), Choice.Go("先查客厅痕迹", "你决定先看空间本身。", "crime", new Vector2(-4.9f,-2.2f)), Choice.Go("先查失物与凶器", "你决定先寻找那个没有被列进去的缺失物。", "missing", new Vector2(-4.9f,-2.2f)), Choice.Go("先查受害者关系", "你决定先把客厅当成一段关系的残留。", "relation", new Vector2(-4.9f,-2.2f))), "E 决定调查方向");
            Add("门", o+new Vector2(6.9f,-.4f), new Vector2(.9f,2.7f), new Dialogue("警局侧门", "门旁的标牌写着：走廊。\n\n今天的调查入口不在这里。真正的选择已经被整理进桌上的卷宗。"), "E 查看");
        }

        private void BuildCrime(Vector2 o, string title)
        {
            Room(o, title, C(75,60,49), C(45,37,32)); Living(o, false);
            Add("茶壶", o+new Vector2(-.7f,-.05f), new Vector2(1.4f,1f), new Dialogue("茶壶", T("CrimeTeapot"), Choice.Ev("记录茶壶", "廉价袋泡红茶、过久浸泡的苦味，以及长期服用药物的可能性连在了一起。", "茶壶中可能被动过手脚。")), "E 查看");
            Add("地面痕迹", o+new Vector2(-1.5f,-1.35f), new Vector2(4.4f,1.7f), new Dialogue("地面痕迹", T("CrimeFloor"), Choice.Ev("记录伪装痕迹", "现场混乱可能是被摆放出来的。", "现场混乱像被人为布置。")), "E 调查");
        }

        private void BuildMissing(Vector2 o)
        {
            BuildCrime(o, "现实客厅：失物与凶器");
            Add("桌边压痕", o+new Vector2(.9f,.35f), new Vector2(1.4f,.85f), new Dialogue("缺失的拆信刀", T("CrimeKnifeMark"), Choice.Ev("记录拆信刀", "缺失的拆信刀不是普通失物，而是一段关系的证词。", "缺失的拆信刀可能是被处理掉的凶器。")), "E 调查");
        }

        private void BuildRelation(Vector2 o)
        {
            BuildCrime(o, "现实客厅：受害者关系");
            Obj(o+new Vector2(2.2f,2.5f), new Vector2(2.1f,1.1f), C(96,73,54), 18, "书架");
            Obj(o+new Vector2(2.2f,2.85f), new Vector2(1.8f,.14f), C(142,122,90), 20, "心理学书籍");
            Add("书架与会面记录", o+new Vector2(2.2f,2.5f), new Vector2(2.4f,1.5f), new Dialogue("受害者关系", "心理医生的房间不是普通客厅。它保存过秘密、礼貌、诊断和争执。\n\n被涂黑的会面记录、病历上的黑线、灰白梦境里看不清脸的人，此刻终于不再像彼此无关的线索。\n\n它们指向同一种处理方式：把关系从纸面上抹掉，再让现场替你讲另一个故事。", Choice.Ev("记录关系线索", "被抹去的不是姓名，而是关系。", "主角与受害者之间存在被抹去的咨询关系。")), "E 调查");
        }

        private void Update()
        {
            UpdateHover();
            if (mainMenu || pause) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { pause = true; slotMode = 0; Time.timeScale = 0f; return; }
            if (dialogue != null) { if (Input.GetMouseButtonDown(0) && !dialogue.HasChoices(page, pages.Length)) Advance(); return; }
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1) input.Normalize();
            Vector3 next = player.position + new Vector3(input.x, input.y, 0) * (3.1f * Time.unscaledDeltaTime);
            Vector2 o = rooms[room]; next.x = Mathf.Clamp(next.x, o.x-RoomW*.5f+.8f, o.x+RoomW*.5f-.8f); next.y = Mathf.Clamp(next.y, o.y-RoomH*.5f+.8f, o.y+RoomH*.5f-.8f); player.position = next;
            if ((Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)) && near != null) Use(near);
        }

        private void UpdateHover()
        {
            if (cam == null || player == null) return;
            Vector3 mp = cam.ScreenToWorldPoint(Input.mousePosition); Vector2 m = new Vector2(mp.x, mp.y); Vector2 p = player.position; hover = null; near = null; float best = 99;
            foreach (Hotspot h in hotspots)
            {
                bool active = InRoom(h.pos); bool over = active && h.Contains(m); h.outline.gameObject.SetActive(over); if (over) hover = h;
                float d = active ? Vector2.Distance(p, h.pos) : 99; if (d < best && d < 2f) { best = d; near = h; }
            }
            if (hover != null) near = hover;
        }

        private bool InRoom(Vector2 p){Vector2 o=rooms[room]; return Mathf.Abs(p.x-o.x)<RoomW*.55f && Mathf.Abs(p.y-o.y)<RoomH*.55f;}
        private void Use(Hotspot h){dialogue=h.dialogue; pages=Pages(dialogue.body); page=0; if(!string.IsNullOrWhiteSpace(dialogue.evidence)) AddEvidence(dialogue.evidence); Time.timeScale=0f;}
        private void Advance(){ if(page<pages.Length-1){page++;return;} dialogue=null; pages=Array.Empty<string>(); Time.timeScale=1f; }
        private void Apply(Choice c){ if(!string.IsNullOrWhiteSpace(c.evidence)) AddEvidence(c.evidence); if(!string.IsNullOrWhiteSpace(c.flag)) flags.Add(c.flag); if(!string.IsNullOrWhiteSpace(c.room)){dialogue=null; MoveTo(c.room,c.local); Time.timeScale=1f; return;} dialogue=new Dialogue(dialogue.title,c.response); pages=Pages(c.response); page=0;}
        private void MoveTo(string r, Vector2 local){room=r; Vector2 o=rooms[r]; player.position=new Vector3(o.x+local.x,o.y+local.y,0); cam.transform.position=new Vector3(o.x,o.y,-10);}
        private void AddEvidence(string e){if(!string.IsNullOrWhiteSpace(e)&&!evidence.Contains(e)) evidence.Add(e);}

        private void OnGUI(){Styles(); if(mainMenu){Menu();return;} if(pause){Pause();return;} EvidenceUI(); Prompt(); if(dialogue!=null) DialogueUI();}
        private void Menu(){GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),blackTex); float w=Mathf.Min(420,Screen.width-56); Rect a=new Rect((Screen.width-w)/2,(Screen.height-360)/2,w,360); GUI.Label(new Rect(a.x,a.y+8,a.width,62),"标题",titleStyle); GUI.Label(new Rect(a.x,a.y+72,a.width,28),"title",smallStyle); if(GUI.Button(new Rect(a.x,a.y+132,a.width,44),"开始新游戏",buttonStyle)){mainMenu=false; evidence.Clear(); flags.Clear(); MoveTo("unknown",new Vector2(-5.2f,-2.3f)); Time.timeScale=1;} GUI.enabled=HasSave(0); if(GUI.Button(new Rect(a.x,a.y+190,a.width,44),"继续已有存档",buttonStyle))Load(0); GUI.enabled=true; if(GUI.Button(new Rect(a.x,a.y+248,a.width,44),"退出",buttonStyle))Application.Quit(); GUI.Label(new Rect(a.x,a.y+314,a.width,28),message,smallStyle);}
        private void Pause(){GUI.Box(new Rect(0,0,Screen.width,Screen.height),GUIContent.none,panelStyle); Rect a=new Rect((Screen.width-760)/2,(Screen.height-520)/2,760,520); GUI.Box(a,GUIContent.none,panelStyle); GUI.Label(new Rect(a.x+24,a.y+20,300,42),"暂停",titleStyle); float x=a.x+24,y=a.y+86; if(GUI.Button(new Rect(x,y,180,42),"返回游戏",buttonStyle)){pause=false;Time.timeScale=dialogue==null?1:0;} if(GUI.Button(new Rect(x,y+56,180,42),"存档",buttonStyle))slotMode=1; if(GUI.Button(new Rect(x,y+112,180,42),"加载",buttonStyle))slotMode=2; if(GUI.Button(new Rect(x,y+168,180,42),"回到主菜单",buttonStyle)){pause=false;mainMenu=true;Time.timeScale=0;} Rect s=new Rect(a.x+240,a.y+88,480,330); if(slotMode==0)GUI.Label(s,"选择左侧的“存档”或“加载”。",bodyStyle); else {GUI.Label(new Rect(s.x,s.y-42,s.width,32),slotMode==1?"选择存档槽":"选择读取槽",titleStyle); for(int i=0;i<5;i++){float ry=s.y+i*58;GUI.Label(new Rect(s.x,ry,320,32),SlotLabel(i),bodyStyle); if(slotMode==1){if(GUI.Button(new Rect(s.x+350,ry,86,36),"存档",buttonStyle)){Save(i);message="已保存到存档 "+(i+1);}}else{GUI.enabled=HasSave(i); if(GUI.Button(new Rect(s.x+350,ry,86,36),"读取",buttonStyle))Load(i); GUI.enabled=true;}}} GUI.Label(new Rect(a.x+24,a.y+470,a.width-48,28),message,smallStyle);}
        private void EvidenceUI(){Rect t=new Rect(Screen.width-172,20,144,34); if(GUI.Button(t,evidenceOpen?"证据 关闭":"证据 展开",buttonStyle))evidenceOpen=!evidenceOpen; if(!evidenceOpen)return; Rect a=new Rect(Screen.width-392,64,364,Mathf.Min(360,Screen.height-100)); GUI.Box(a,GUIContent.none,panelStyle); GUI.Label(new Rect(a.x+16,a.y+12,a.width-32,28),"目前掌握的证据",titleStyle); Rect view=new Rect(a.x+16,a.y+52,a.width-32,a.height-68); Rect content=new Rect(0,0,view.width-18,Mathf.Max(view.height,evidence.Count*68+24)); evidenceScroll=GUI.BeginScrollView(view,evidenceScroll,content); if(evidence.Count==0)GUI.Label(new Rect(0,0,content.width,40),"尚未记录证据。",bodyStyle); for(int i=0;i<evidence.Count;i++)GUI.Label(new Rect(0,i*68,content.width,60),"• "+evidence[i],bodyStyle); GUI.EndScrollView();}
        private void Prompt(){if(near==null||dialogue!=null)return; Rect r=new Rect(24,Screen.height-62,360,38); GUI.Box(r,GUIContent.none,panelStyle); GUI.Label(new Rect(r.x+14,r.y+8,r.width-28,22),near.prompt+" - "+near.name,bodyStyle);}
        private void DialogueUI(){float w=Mathf.Min(660,Screen.width-56); float h=dialogue.choices.Length>0&&page>=pages.Length-1?384:306; Rect a=new Rect(Screen.width-w-28,Screen.height-h-28,w,h); GUI.Box(a,GUIContent.none,panelStyle); GUI.Label(new Rect(a.x+22,a.y+18,a.width-44,30),dialogue.title,titleStyle); GUI.Label(new Rect(a.x+24,a.y+62,a.width-48,162),pages.Length==0?"":pages[Mathf.Clamp(page,0,pages.Length-1)],bodyStyle); GUI.Label(new Rect(a.x+24,a.y+h-36,a.width-48,22),(page+1)+"/"+Mathf.Max(1,pages.Length)+"  点击鼠标进入下一句",smallStyle); if(dialogue.choices.Length>0&&page>=pages.Length-1){for(int i=0;i<dialogue.choices.Length;i++){float bw=(a.width-60)/dialogue.choices.Length; Rect br=new Rect(a.x+24+i*(bw+6),a.y+248,bw,44); if(GUI.Button(br,dialogue.choices[i].label,buttonStyle))Apply(dialogue.choices[i]);}}}

        private void Save(int slot){string p="MC2D_"+slot+"_"; PlayerPrefs.SetInt(p+"has",1); PlayerPrefs.SetString(p+"room",room); PlayerPrefs.SetFloat(p+"x",player.position.x-rooms[room].x); PlayerPrefs.SetFloat(p+"y",player.position.y-rooms[room].y); PlayerPrefs.SetString(p+"evidence",string.Join("|",evidence)); PlayerPrefs.SetString(p+"time",DateTime.Now.ToString("yyyy-MM-dd HH:mm")); PlayerPrefs.Save();}
        private void Load(int slot){if(!HasSave(slot)){message="没有可读取的存档";return;} string p="MC2D_"+slot+"_"; evidence.Clear(); foreach(string e in PlayerPrefs.GetString(p+"evidence","").Split('|'))if(!string.IsNullOrWhiteSpace(e))evidence.Add(e); MoveTo(PlayerPrefs.GetString(p+"room","unknown"),new Vector2(PlayerPrefs.GetFloat(p+"x",-5.2f),PlayerPrefs.GetFloat(p+"y",-2.3f))); mainMenu=false; pause=false; dialogue=null; Time.timeScale=1;}
        private static bool HasSave(int slot){return PlayerPrefs.GetInt("MC2D_"+slot+"_has",0)==1;} private static string SlotLabel(int slot){string p="MC2D_"+slot+"_"; return HasSave(slot)?"存档 "+(slot+1)+"："+PlayerPrefs.GetString(p+"time",""):"存档 "+(slot+1)+"：空";}

        private static string[] Pages(string body){if(string.IsNullOrWhiteSpace(body))return new[]{""}; List<string> r=new List<string>(); StringBuilder b=new StringBuilder(); foreach(string raw in body.Replace("\r","").Split('\n')){string line=raw.Trim(); if(line.Length==0){Flush();continue;} if(b.Length+line.Length>64)Flush(); if(b.Length>0)b.Append("\n"); b.Append(line);} Flush(); return r.Count==0?new[]{body}:r.ToArray(); void Flush(){if(b.Length>0){r.Add(b.ToString());b.Clear();}}}
        private static string T(string name){TextAsset a=Resources.Load<TextAsset>("Narrative/"+name); return a==null?name:a.text;}

        private void Styles(){if(titleStyle!=null)return; font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); if(font==null) font=Resources.GetBuiltinResource<Font>("Arial.ttf"); panelTex=Tex(C(10,12,14,224)); buttonTex=Tex(C(34,38,42,245)); blackTex=Tex(Color.black); titleStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=47,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=C(240,240,232)},wordWrap=true}; bodyStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=30,alignment=TextAnchor.UpperLeft,normal={textColor=C(226,226,218)},wordWrap=true}; smallStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=24,alignment=TextAnchor.MiddleCenter,normal={textColor=C(150,158,162)},wordWrap=true}; panelStyle=new GUIStyle(GUI.skin.box){normal={background=panelTex}}; buttonStyle=new GUIStyle(GUI.skin.button){font=font,fontSize=30,alignment=TextAnchor.MiddleCenter,normal={background=buttonTex,textColor=C(232,234,232)},hover={background=buttonTex,textColor=Color.white},wordWrap=true};}
        private Texture2D Tex(Color c){Texture2D t=new Texture2D(1,1);t.filterMode=FilterMode.Point;t.SetPixel(0,0,c);t.Apply();return t;}
        private void Room(Vector2 o,string n,Color f,Color w){Obj(o,new Vector2(RoomW,RoomH),f,0,n); Obj(o+new Vector2(0,RoomH*.5f-.35f),new Vector2(RoomW,.7f),w,5,n+"北墙"); Obj(o+new Vector2(0,-RoomH*.5f+.35f),new Vector2(RoomW,.7f),w,5,n+"南墙"); Obj(o+new Vector2(-RoomW*.5f+.35f,0),new Vector2(.7f,RoomH),w,5,n+"西墙"); Obj(o+new Vector2(RoomW*.5f-.35f,0),new Vector2(.7f,RoomH),w,5,n+"东墙"); for(int i=0;i<8;i++){float x=-6.5f+i*1.85f; Obj(o+new Vector2(x,4.32f),new Vector2(.9f,.18f),C(230,222,190,160),9,"顶灯");}}
        private void Living(Vector2 o,bool gray){Color sofa=gray?C(121,121,116):C(84,50,40), table=gray?C(135,135,130):C(80,55,41), rug=gray?C(133,133,127):C(54,38,34); Obj(o+new Vector2(-3.5f,-1f),new Vector2(3.2f,1.25f),sofa,18,"沙发"); Obj(o+new Vector2(1.7f,-1.35f),new Vector2(1,1.1f),gray?C(103,103,99):C(78,49,35),20,"偏移椅子"); Obj(o+new Vector2(-.7f,-.35f),new Vector2(2.35f,1.15f),table,22,"茶几"); Obj(o+new Vector2(-.7f,-.05f),new Vector2(.54f,.42f),gray?C(188,188,180):C(151,176,154),30,"茶壶"); Obj(o+new Vector2(-1.4f,-1.55f),new Vector2(5.8f,1.85f),rug,10,"地毯"); Obj(o+new Vector2(3.85f,2.25f),new Vector2(1.3f,2.4f),gray?C(102,102,98):C(83,61,44),18,"书架"); Obj(o+new Vector2(-5.65f,2.4f),new Vector2(1.5f,1),gray?C(160,160,154):C(112,86,66),18,"墙上相框"); Obj(o+new Vector2(.2f,-1.15f),new Vector2(.6f,.12f),gray?C(202,202,194):C(168,154,132),32,"杯子碎片");}
        private void Bed(Vector2 p,Color sheet,Color frame){Obj(p,new Vector2(3.4f,1.8f),frame,18,"床架"); Obj(p+new Vector2(.2f,.1f),new Vector2(2.75f,1.35f),sheet,19,"床单"); Obj(p+new Vector2(-1,.42f),new Vector2(.92f,.5f),C(238,238,230),20,"枕头"); Obj(p+new Vector2(1.12f,-.18f),new Vector2(.65f,1),sheet*.86f,21,"被子");}
        private void Desk(Vector2 p,Color c){Obj(p,new Vector2(4.6f,2),c,18,"桌子"); Obj(p+new Vector2(-1.9f,-.95f),new Vector2(.45f,.55f),c*.7f,19,"桌腿"); Obj(p+new Vector2(1.9f,-.95f),new Vector2(.45f,.55f),c*.7f,19,"桌腿");}
        private void Shelves(Vector2 o){Obj(o+new Vector2(-5.7f,1.6f),new Vector2(1.1f,3.2f),C(48,42,36),18,"档案柜左"); Obj(o+new Vector2(5.7f,1.7f),new Vector2(1.1f,3.1f),C(48,42,36),18,"档案柜右");}
        private void TileMarks(Vector2 o,Color c){for(int x=-5;x<=5;x+=2)for(int y=-3;y<=3;y+=2)Obj(o+new Vector2(x,y),new Vector2(.9f,.08f),c,2,"浅色地砖缝");}
        private void Scatter(Vector2 o,Color c,int n){for(int i=0;i<n;i++){float x=-6.2f+(i*2.37f)%12.4f,y=-3.6f+(i*1.41f)%7.2f;Obj(o+new Vector2(x,y),new Vector2(.28f+(i%3)*.08f,.12f),c,7,"装饰杂物");}}
        private GameObject Obj(Vector2 p,Vector2 s,Color c,int order,string name){GameObject g=new GameObject(name);g.transform.SetParent(transform);g.transform.position=new Vector3(p.x,p.y,0);Rect(g.transform,"sprite",Vector2.zero,s,c,order);return g;}
        private SpriteRenderer Rect(Transform parent,string name,Vector2 local,Vector2 size,Color color,int order){GameObject g=new GameObject(name);g.transform.SetParent(parent);g.transform.localPosition=new Vector3(local.x,local.y,0);g.transform.localScale=new Vector3(size.x,size.y,1);SpriteRenderer sr=g.AddComponent<SpriteRenderer>();sr.sprite=Pixel();sr.color=color;sr.sortingOrder=order;return sr;}
        private void Add(string n,Vector2 p,Vector2 s,Dialogue d,string prompt){Hotspot h=new Hotspot(n,p,s,d,prompt);h.outline=new GameObject(n+" Outline").transform;h.outline.SetParent(transform);Outline(h.outline,p,s);h.outline.gameObject.SetActive(false);hotspots.Add(h);} private void Outline(Transform r,Vector2 p,Vector2 s){r.position=new Vector3(p.x,p.y,-.05f);Rect(r,"top",new Vector2(0,s.y*.5f),new Vector2(s.x+.06f,.045f),Color.black,100);Rect(r,"bottom",new Vector2(0,-s.y*.5f),new Vector2(s.x+.06f,.045f),Color.black,100);Rect(r,"left",new Vector2(-s.x*.5f,0),new Vector2(.045f,s.y+.06f),Color.black,100);Rect(r,"right",new Vector2(s.x*.5f,0),new Vector2(.045f,s.y+.06f),Color.black,100);}
        private static Sprite Pixel(){if(pixel!=null)return pixel;Texture2D t=new Texture2D(1,1);t.filterMode=FilterMode.Point;t.SetPixel(0,0,Color.white);t.Apply();pixel=Sprite.Create(t,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);return pixel;} private static Color C(byte r,byte g,byte b,byte a=255){return new Color32(r,g,b,a);} 
        private sealed class Hotspot{public string name,prompt;public Vector2 pos,size;public Dialogue dialogue;public Transform outline;public Hotspot(string n,Vector2 p,Vector2 s,Dialogue d,string pr){name=n;pos=p;size=s;dialogue=d;prompt=pr;}public bool Contains(Vector2 v){return Mathf.Abs(v.x-pos.x)<=size.x*.5f&&Mathf.Abs(v.y-pos.y)<=size.y*.5f;}}
        private sealed class Dialogue{public string title,body,evidence;public Choice[] choices;public Dialogue(string t,string b,params Choice[] c){title=t;body=b;choices=c??Array.Empty<Choice>();evidence="";} public bool HasChoices(int p,int pc){return choices.Length>0&&p>=pc-1;}}
        private sealed class Choice{public string label,response,flag,evidence,room;public Vector2 local;public static Choice Ev(string l,string r,string e){return new Choice{label=l,response=r,evidence=e};} public static Choice Go(string l,string r,string room,Vector2 local){return new Choice{label=l,response=r,room=room,local=local};}}
    }
}
