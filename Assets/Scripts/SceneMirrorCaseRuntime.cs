using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MirrorCase2D
{
    public sealed class SceneMirrorCaseRuntime : MonoBehaviour
    {
        private const float StandardRoomWidth = 16f;
        private const float StandardRoomHeight = 10f;
        private const bool ShowRuntimeHotspotOutlines = false;
        private const bool DisableRoomBlocksTemporarily = true;
        private const float StandingHumanHeightRatio = 0.21f;

        public Transform player;
        public Camera sceneCamera;
        public string currentRoom = "unknown";
        public Vector2 roomSize = new Vector2(StandardRoomWidth, StandardRoomHeight);
        public string[] roomIds;
        public Vector2[] roomOrigins;

        private readonly List<string> evidence = new List<string>();
        private readonly HashSet<string> roomIntrosShown = new HashSet<string>();
        private readonly HashSet<string> firstDayItemsSeen = new HashSet<string>();
        private readonly HashSet<string> day2SceneItemsSeen = new HashSet<string>();
        private readonly HashSet<string> witnessItemsSeen = new HashSet<string>();
        private readonly HashSet<string> clinicItemsSeen = new HashSet<string>();
        private readonly HashSet<string> evidenceRoomItemsSeen = new HashSet<string>();
        private readonly HashSet<string> suspicionItemsSeen = new HashSet<string>();
        private readonly HashSet<string> renewedItemsSeen = new HashSet<string>();
        private readonly HashSet<string> fullMemoryItemsSeen = new HashSet<string>();
        private readonly List<SceneHotspot> hotspots = new List<SceneHotspot>();
        private readonly Dictionary<SceneHotspot, SpriteRenderer> hotspotHighlights = new Dictionary<SceneHotspot, SpriteRenderer>();
        private SceneHotspot nearest;
        private SceneHotspot active;
        private string[] pages = Array.Empty<string>();
        private int page;
        private bool mainMenu = true;
        private bool pauseMenu;
        private bool evidenceOpen;
        private int selectedEvidence = -1;
        private int slotMode;
        private string message = string.Empty;
        private Vector2 evidenceScroll;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle panelStyle;
        private GUIStyle smallStyle;
        private GUIStyle evidenceTitleStyle;
        private GUIStyle evidenceBodyStyle;
        private GUIStyle evidenceCaptionStyle;
        private Texture2D blackTex;
        private Texture2D panelTex;
        private Texture2D buttonTex;
        private Texture2D shelfWoodTex;
        private Texture2D shelfEdgeTex;
        private Texture2D shelfBackTex;
        private Texture2D evidenceCardTex;
        private Texture2D evidenceHoverTex;
        private Texture2D evidenceSelectedTex;
        private Texture2D paperTex;
        private Texture2D paperLineTex;
        private Texture2D inkTex;
        private Texture2D metalTex;
        private Texture2D accentTex;
        private Texture2D photoTex;
        private Font font;
        private SpriteRenderer playerSpriteRenderer;
        private Sprite hotspotHighlightSprite;
        private Sprite playerFrontSprite;
        private Sprite playerBackSprite;
        private Sprite playerLeftSprite;
        private Sprite playerRightSprite;
        private string playerFacing = "back";
        private int lastDialogueAdvanceFrame = -1;
        private string unlockedDreamDoorTarget = "dream1";
        private bool pendingClinicAfterGirlfriend;
        private string pendingPoliceTransferRoom = string.Empty;
        private Vector2 pendingPoliceTransferLocal = Vector2.zero;
        private string pendingPoliceTransferLabel = string.Empty;
        private string pendingDreamTransitionRoom = string.Empty;
        private Vector2 pendingDreamTransitionLocal = Vector2.zero;
        private string pendingDreamTransitionLabel = string.Empty;

        private void Awake()
        {
            if (sceneCamera == null) sceneCamera = Camera.main;
            if (player == null)
            {
                GameObject found = GameObject.Find("Player");
                if (found != null) player = found.transform;
            }
            if (roomSize.x <= 0f || roomSize.y <= 0f) roomSize = new Vector2(StandardRoomWidth, StandardRoomHeight);
            CachePlayerDirectionSprites();
            hotspots.AddRange(FindObjectsByType<SceneHotspot>(FindObjectsSortMode.None));
            NormalizeTwineFlowHotspots();
            EnsureFlowHotspots();
            ConfigureHospitalRuntimePresentation();
            NormalizeSceneHumanScale();
            ApplyStandardCameraScale();
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneAudioController.Ensure();
            MoveTo(currentRoom, new Vector2(-1.15f, -2.25f));
        }

        private void Update()
        {
            if (player == null || sceneCamera == null) return;
            UpdateHotspotFocus();
            if (mainMenu) return;
            if (evidenceOpen)
            {
                if (KeyPressed(KeyCode.Escape)) CloseEvidencePage();
                return;
            }
            if (KeyPressed(KeyCode.Escape))
            {
                pauseMenu = !pauseMenu;
                slotMode = 0;
                message = string.Empty;
                SceneAudioController.Instance?.PlayButton();
                Time.timeScale = pauseMenu || active != null ? 0f : 1f;
                return;
            }
            if (pauseMenu) return;
            if (active == null && KeyPressed(KeyCode.F))
            {
                OpenNextScenePrompt();
                return;
            }
            if (active != null && currentRoom != "unknown")
            {
                if ((MouseClicked() || KeyPressed(KeyCode.Space)) && !ShowingChoices()) TryAdvanceDialogue();
                return;
            }
            if (active != null && (MouseClicked() || KeyPressed(KeyCode.Space)) && !ShowingChoices()) TryAdvanceDialogue();
            Vector2 input = ReadMoveInput();
            if (input.sqrMagnitude > 1f) input.Normalize();
            UpdatePlayerDirection(input);
            Vector3 next = player.position + new Vector3(input.x, input.y, 0f) * (3.2f * Time.unscaledDeltaTime);
            Vector2 origin = GetRoomOrigin(currentRoom);
            next.x = Mathf.Clamp(next.x, origin.x - roomSize.x * 0.5f + 0.8f, origin.x + roomSize.x * 0.5f - 0.8f);
            next.y = Mathf.Clamp(next.y, origin.y - roomSize.y * 0.5f + 0.8f, origin.y + roomSize.y * 0.5f - 0.8f);
            next = ResolveRoomBlocks(player.position, next);
            player.position = next;
            if (input.sqrMagnitude > 0.001f) SceneAudioController.Instance?.PlayFootstep(currentRoom, input);
            if ((KeyPressed(KeyCode.E) || MouseClicked()) && nearest != null) Open(nearest);
        }

        private Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float x = 0f;
                float y = 0f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
                return new Vector2(x, y);
            }
#endif
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }

        private void CachePlayerDirectionSprites()
        {
            if (player == null) return;

            Transform spriteTransform = player.Find("Player Pixel Sprite - Detective");
            playerSpriteRenderer = spriteTransform != null ? spriteTransform.GetComponent<SpriteRenderer>() : player.GetComponentInChildren<SpriteRenderer>(true);
            playerFrontSprite = Resources.Load<Sprite>("ProloguePlayerDirections/player_front");
            playerBackSprite = Resources.Load<Sprite>("ProloguePlayerDirections/player_back");
            playerLeftSprite = Resources.Load<Sprite>("ProloguePlayerDirections/player_left");
            playerRightSprite = Resources.Load<Sprite>("ProloguePlayerDirections/player_right");
            SetPlayerDirection("back", true);
        }

        private void UpdatePlayerDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.001f) return;
            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
            {
                SetPlayerDirection(input.x >= 0f ? "right" : "left");
            }
            else
            {
                SetPlayerDirection(input.y >= 0f ? "back" : "front");
            }
        }

        private void SetPlayerDirection(string direction, bool force = false)
        {
            if (!force && playerFacing == direction) return;
            playerFacing = direction;
            if (playerSpriteRenderer == null) return;

            Sprite nextSprite = playerBackSprite;
            switch (direction)
            {
                case "front":
                    nextSprite = playerFrontSprite;
                    break;
                case "left":
                    nextSprite = playerLeftSprite;
                    break;
                case "right":
                    nextSprite = playerRightSprite;
                    break;
            }
            if (nextSprite == null) return;

            playerSpriteRenderer.sprite = nextSprite;
            playerSpriteRenderer.sortingOrder = 83;
            ApplyPlayerLogicalScale();
        }

        private void ApplyPlayerLogicalScale()
        {
            if (playerSpriteRenderer == null || playerSpriteRenderer.sprite == null || playerSpriteRenderer.sprite.bounds.size.y <= 0f) return;
            float targetHeight = GetStandingHumanHeight();
            float scale = targetHeight / playerSpriteRenderer.sprite.bounds.size.y;
            playerSpriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            playerSpriteRenderer.transform.localPosition = new Vector3(0f, targetHeight * 0.23f, -0.05f);
        }

        private float GetStandingHumanHeight()
        {
            return Mathf.Clamp(roomSize.y * StandingHumanHeightRatio, 1.55f, 2.6f);
        }

        private bool KeyPressed(KeyCode fallbackKey)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (fallbackKey == KeyCode.Escape) return keyboard.escapeKey.wasPressedThisFrame;
                if (fallbackKey == KeyCode.E) return keyboard.eKey.wasPressedThisFrame;
                if (fallbackKey == KeyCode.F) return keyboard.fKey.wasPressedThisFrame;
                if (fallbackKey == KeyCode.Space) return keyboard.spaceKey.wasPressedThisFrame;
            }
#endif
            return Input.GetKeyDown(fallbackKey);
        }

        private bool KeyHeld(KeyCode fallbackKey)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (fallbackKey == KeyCode.Tab) return keyboard.tabKey.isPressed;
                if (fallbackKey == KeyCode.Escape) return keyboard.escapeKey.isPressed;
                if (fallbackKey == KeyCode.E) return keyboard.eKey.isPressed;
                if (fallbackKey == KeyCode.Space) return keyboard.spaceKey.isPressed;
            }
#endif
            return Input.GetKey(fallbackKey);
        }

        private bool MouseClicked()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null) return mouse.leftButton.wasPressedThisFrame;
#endif
            return Input.GetMouseButtonDown(0);
        }

        private Vector3 ReadMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null) return mouse.position.ReadValue();
#endif
            return Input.mousePosition;
        }

        private void UpdateHotspotFocus()
        {
            Vector3 mouseWorld3 = sceneCamera.ScreenToWorldPoint(ReadMousePosition());
            Vector2 mouse = new Vector2(mouseWorld3.x, mouseWorld3.y);
            Vector2 playerPos = player.position;
            nearest = null;
            float best = 999f;
            foreach (SceneHotspot hotspot in hotspots)
            {
                if (hotspot == null) continue;
                if (!IsHotspotAvailable(hotspot)) continue;
                bool activeRoom = IsInCurrentRoom(hotspot.transform.position);
                bool over = activeRoom && Contains(hotspot, mouse);
                if (hotspot.outline != null) hotspot.outline.gameObject.SetActive(ShowRuntimeHotspotOutlines && over);
                float distance = activeRoom ? Vector2.Distance(playerPos, hotspot.transform.position) : 999f;
                if (over)
                {
                    nearest = hotspot;
                    best = 0f;
                }
                else if (distance < 2.0f && distance < best)
                {
                    nearest = hotspot;
                    best = distance;
                }
            }
            UpdateHotspotHighlight();
        }

        private void UpdateHotspotHighlight()
        {
            HideAllHotspotHighlights();
            if (active != null || mainMenu || pauseMenu || evidenceOpen) return;
            if (KeyHeld(KeyCode.Tab))
            {
                foreach (SceneHotspot hotspot in hotspots)
                {
                    if (hotspot == null || !IsHotspotAvailable(hotspot) || !IsInCurrentRoom(hotspot.transform.position)) continue;
                    SetHotspotHighlight(hotspot, true);
                }
                return;
            }
            if (nearest == null) return;
            SetHotspotHighlight(nearest, true);
        }

        private void HideAllHotspotHighlights()
        {
            foreach (SpriteRenderer renderer in hotspotHighlights.Values)
            {
                if (renderer != null) renderer.gameObject.SetActive(false);
            }
        }

        private void SetHotspotHighlight(SceneHotspot hotspot, bool visible)
        {
            SpriteRenderer renderer = EnsureHotspotHighlight(hotspot);
            if (renderer != null) renderer.gameObject.SetActive(visible);
        }

        private SpriteRenderer EnsureHotspotHighlight(SceneHotspot hotspot)
        {
            if (hotspot == null) return null;
            if (hotspotHighlights.TryGetValue(hotspot, out SpriteRenderer existing) && existing != null) return existing;

            BoxCollider2D box = hotspot.GetComponent<BoxCollider2D>();
            if (box == null) return null;
            if (hotspotHighlightSprite == null)
            {
                Texture2D texture = Texture(Color.white);
                hotspotHighlightSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            }

            GameObject highlight = new GameObject("Runtime Item Highlight");
            highlight.hideFlags = HideFlags.DontSave;
            highlight.transform.SetParent(hotspot.transform, false);
            highlight.transform.localPosition = new Vector3(box.offset.x, box.offset.y, -0.08f);
            highlight.transform.localScale = new Vector3(box.size.x + 0.18f, box.size.y + 0.18f, 1f);

            SpriteRenderer renderer = highlight.AddComponent<SpriteRenderer>();
            renderer.sprite = hotspotHighlightSprite;
            renderer.color = new Color(1f, 0.86f, 0.22f, 0.24f);
            renderer.sortingOrder = 98;
            highlight.SetActive(false);
            hotspotHighlights[hotspot] = renderer;
            return renderer;
        }

        private bool Contains(SceneHotspot hotspot, Vector2 point)
        {
            BoxCollider2D box = hotspot.GetComponent<BoxCollider2D>();
            return box != null && box.OverlapPoint(point);
        }

        private bool IsInCurrentRoom(Vector2 point)
        {
            Vector2 origin = GetRoomOrigin(currentRoom);
            return Mathf.Abs(point.x - origin.x) <= roomSize.x * 0.55f && Mathf.Abs(point.y - origin.y) <= roomSize.y * 0.55f;
        }

        private void Open(SceneHotspot hotspot)
        {
            HideAllHotspotHighlights();
            RefreshPoliceTransferHotspot(hotspot);
            RefreshDreamTransitionHotspot(hotspot);
            SceneAudioController.Instance?.PlayInteract();
            active = hotspot;
            pages = BuildPages(hotspot.body);
            page = 0;
            MarkStoryProgress(hotspot.title);
            MarkFirstDayItemSeen(hotspot.title);
            if (!string.IsNullOrWhiteSpace(hotspot.evidence)) AddEvidence(hotspot.evidence);
            Time.timeScale = 0f;
        }

        private void OpenNextScenePrompt()
        {
            if (mainMenu || pauseMenu || evidenceOpen || active != null) return;
            SceneAudioController.Instance?.PlayButton();

            switch (currentRoom)
            {
                case "work":
                    if (!string.IsNullOrWhiteSpace(pendingPoliceTransferRoom))
                    {
                        string destination = string.IsNullOrWhiteSpace(pendingPoliceTransferLabel) ? GetRoomDisplayName(pendingPoliceTransferRoom) : pendingPoliceTransferLabel;
                        OpenTemporaryPrompt(
                            "继续调查",
                            "是否从警局办公室前往下一处调查地点？\n\n目的地：" + destination,
                            "前往" + destination,
                            "你带上记录，从警局办公室前往" + destination + "。",
                            pendingPoliceTransferRoom,
                            pendingPoliceTransferLocal,
                            "暂不前往");
                        return;
                    }

                    if (!HasSeenAllFirstDayItems())
                    {
                        OpenNotice("前往下一场景", "第一天调查还没有完成。\n\n你需要先看完警局里的关键物品：凶器、受害者信息、现场证据/证据链、监控背影。\n\n按 Tab 可以高亮当前房间的可交互物品。");
                        return;
                    }

                    OpenTemporaryPrompt(
                        "前往下一场景",
                        "是否结束第一天调查，回到卧室？\n\n之后需要在卧室通过床入睡，才能进入梦境长廊。",
                        "结束第一天，回到卧室",
                        "你把第一天的材料收拢。调查暂时结束，夜晚从卧室开始。",
                        "bedroom",
                        BedroomEntryPosition,
                        "继续查看警局");
                    return;
                case "bedroom":
                    OpenNotice("前往下一场景", "卧室不能直接跳到下一场景。\n\n你必须走到床边交互，选择入睡，才能进入梦境长廊。");
                    return;
                case "corridor":
                    OpenNotice("前往下一场景", "梦境长廊没有统一的下一场景。\n\n请选择当前夜晚对应的门。按 Tab 可以高亮可交互的门。");
                    return;
                case "unknown":
                    if (!string.IsNullOrWhiteSpace(pendingDreamTransitionRoom))
                    {
                        string destination = string.IsNullOrWhiteSpace(pendingDreamTransitionLabel) ? GetRoomDisplayName(pendingDreamTransitionRoom) : pendingDreamTransitionLabel;
                        OpenTemporaryPrompt(
                            "未知之地",
                            "是否穿过黑墙白塔后的空白，醒来进入下一段？\n\n目的地：" + destination,
                            "醒来，前往" + destination,
                            "白塔后的光吞没视野。你从梦里醒来，下一段调查已经在等你。",
                            pendingDreamTransitionRoom,
                            pendingDreamTransitionLocal,
                            "暂时停留");
                        return;
                    }
                    OpenNotice("前往下一场景", "这里是黑墙白塔的过渡场景。\n\n请点击场景中的“未知之地”交互点继续。");
                    return;
                case "day2scene":
                    if (!day2SceneItemsSeen.Contains("diary"))
                    {
                        OpenNotice("前往下一场景", "案发现场调查还没有完成。\n\n你需要先检查缺失的日记本与失窃痕迹。按 Tab 可以高亮当前房间的可交互物品。");
                        return;
                    }
                    OpenTemporaryPrompt("前往下一场景", "是否结束案发现场调查，回到警局整理并进入证人问询？", "前往证人问询", "你把现场记录带回警局，准备传唤证人。", "interviews", new Vector2(-5.2f, -2.3f), "继续调查现场");
                    return;
                case "interviews":
                    if (!HasAll(witnessItemsSeen, "thief", "girlfriend", "neighbor"))
                    {
                        OpenNotice("前往下一场景", "证人问询还没有完成。\n\n你需要分别问询盗贼、前女友、邻居。三个证词共同构成第二天的调查结论。");
                        return;
                    }
                    OpenTemporaryPrompt("前往下一场景", "是否结束第二天问询，回到卧室？", "结束问询，回到卧室", "口供被放回桌面。夜晚仍然要从卧室开始。", "bedroom", BedroomEntryPosition, "继续问询");
                    return;
                case "clinic":
                    if (!HasAll(clinicItemsSeen, "schedule", "case"))
                    {
                        OpenNotice("前往下一场景", "心理咨询室调查还没有完成。\n\n你需要先查看日程表，再确认同名病例柜。这里是第三天调查的核心转折。");
                        return;
                    }
                    OpenTemporaryPrompt("前往下一场景", "是否结束心理咨询室调查，回警局并前往证物室？", "前往证物室", "日程表和病例把调查推向旧案证物。", "evidenceRoom", new Vector2(-5.2f, -2.3f), "继续调查咨询室");
                    return;
                case "evidenceRoom":
                    if (!HasAll(evidenceRoomItemsSeen, "sedative", "warrant"))
                    {
                        OpenNotice("前往下一场景", "证物室调查还没有完成。\n\n你需要确认丢失的处方安眠药，并处理搜查令。这个选择决定第三夜梦境会先指向朦胧回忆还是家庭餐桌。");
                        return;
                    }
                    OpenTemporaryPrompt("前往下一场景", "是否结束证物室调查，回到卧室？", "回到卧室", "证物室的选择被你带回夜晚。", "bedroom", BedroomEntryPosition, "继续查看证物室");
                    return;
                case "suspicion":
                    if (!HasAll(suspicionItemsSeen, "colleague", "recusal"))
                    {
                        OpenNotice("前往下一场景", "怀疑阶段还没有完成。\n\n你需要查看同事的发现与回避通知。它们说明调查为什么从案件转向你本人。");
                        return;
                    }
                    OpenTemporaryPrompt("前往下一场景", "是否结束怀疑阶段，进入第六天重新调查？", "进入重新调查", "怀疑没有散去，只把你推向更具体的证据。", "renewed", new Vector2(-5.2f, -2.3f), "暂不进入");
                    return;
                case "renewed":
                    if (!HasAll(renewedItemsSeen, "autopsy", "neighbor", "bar"))
                    {
                        OpenNotice("前往下一场景", "第六天重新调查还没有完成。\n\n你需要查看重新尸检、邻居新证词和酒吧监控。三者共同把最终梦境解锁。");
                        return;
                    }
                    OpenTemporaryPrompt("前往下一场景", "是否结束第六天调查，回到卧室？", "回到卧室", "最后的梦仍然要从卧室和长廊开始。", "bedroom", BedroomEntryPosition, "继续调查");
                    return;
                case "memory":
                    if (!HasAll(fullMemoryItemsSeen, "tea", "knife", "seat", "floor"))
                    {
                        OpenNotice("前往下一场景", "完整回忆还没有结束。\n\n你需要依次查看茶壶、拆信刀、受害者座位、被清理过的地面。真相必须由这些物件拼出来。");
                        return;
                    }
                    OpenTemporaryPrompt("前往下一场景", "是否进入天亮前的最后陈述？", "写下最后的陈述", "所有物证都回到同一个名字下面。", "ending", new Vector2(0f, -3.75f), "继续停留");
                    return;
                default:
                    OpenNotice("前往下一场景", "当前房间没有统一的下一场景入口。\n\n请使用场景内的交互物品继续推进。");
                    return;
            }
        }

        private void OpenNotice(string title, string body)
        {
            active = new GameObject("Notice - " + title).AddComponent<SceneHotspot>();
            active.hideFlags = HideFlags.HideAndDontSave;
            active.title = title;
            active.body = body;
            pages = BuildPages(body);
            page = 0;
            Time.timeScale = 0f;
        }

        private void OpenTemporaryPrompt(string title, string body, string yesLabel, string yesResponse, string targetRoom, Vector2 targetLocalPosition, string noLabel)
        {
            active = new GameObject("Prompt - " + title).AddComponent<SceneHotspot>();
            active.hideFlags = HideFlags.HideAndDontSave;
            active.title = title;
            active.body = body;
            active.choiceLabels = new[] { yesLabel, noLabel };
            active.choiceResponses = new[] { yesResponse, "你暂时停下，继续检查当前房间。" };
            active.choiceTargetRooms = new[] { targetRoom, string.Empty };
            active.choiceTargetLocalPositions = new[] { targetLocalPosition, Vector2.zero };
            pages = BuildPages(body);
            page = 0;
            Time.timeScale = 0f;
        }

        private void AdvanceDialogue()
        {
            if (page < pages.Length - 1)
            {
                page++;
                SceneAudioController.Instance?.PlayPage();
                return;
            }
            if (active != null && active.HasChoices && active.choiceLabels.Length == 1)
            {
                ApplyChoice(0);
                return;
            }
            SceneAudioController.Instance?.PlayClose();
            active = null;
            pages = Array.Empty<string>();
            Time.timeScale = pauseMenu || mainMenu ? 0f : 1f;
        }

        private void TryAdvanceDialogue()
        {
            if (lastDialogueAdvanceFrame == Time.frameCount) return;
            lastDialogueAdvanceFrame = Time.frameCount;
            AdvanceDialogue();
        }

        private bool ShowingChoices()
        {
            return active != null && active.HasChoices && active.choiceLabels.Length > 1 && page >= pages.Length - 1;
        }

        private void ApplyChoice(int index)
        {
            if (active == null || active.choiceLabels == null || index >= active.choiceLabels.Length) return;
            SceneAudioController.Instance?.PlayChoice();
            if (currentRoom == "evidenceRoom" && active.title == "搜查令")
            {
                unlockedDreamDoorTarget = index == 0 ? "vagueMemory" : "family";
                evidenceRoomItemsSeen.Add("warrant");
            }
            else if (currentRoom == "renewed" && active.title == "酒吧监控")
            {
                unlockedDreamDoorTarget = "finalDream";
                renewedItemsSeen.Add("bar");
            }
            string ev = GetArray(active.choiceEvidence, index);
            if (!string.IsNullOrWhiteSpace(ev)) AddEvidence(ev);
            string target = GetArray(active.choiceTargetRooms, index);
            if (!string.IsNullOrWhiteSpace(target))
            {
                target = CanonicalTwineTarget(target);
                if (IsDreamCorridorDoor(active) && !CanOpenDreamDoor(target))
                {
                    OpenLockedDreamDoorResponse(active.title);
                    return;
                }

                UpdateDreamDoorLock(active.title, index, target);
                Vector2 local = active.choiceTargetLocalPositions != null && index < active.choiceTargetLocalPositions.Length ? active.choiceTargetLocalPositions[index] : Vector2.zero;
                if (active.title == "走廊尽头的门" && currentRoom == "school" && target == "interviewGirlfriend")
                {
                    pendingClinicAfterGirlfriend = true;
                }
                else if (active.title == "前女友" && currentRoom == "interviewGirlfriend" && pendingClinicAfterGirlfriend)
                {
                    target = "clinic";
                    local = new Vector2(-5.2f, -2.3f);
                    pendingClinicAfterGirlfriend = false;
                }
                bool completingDreamTransition = active.title == "未知之地";
                if (ShouldRouteThroughDreamTransition(currentRoom, target))
                {
                    ScheduleDreamTransition(target, local);
                    active = null;
                    MoveTo("unknown", new Vector2(-1.15f, -2.25f));
                    Time.timeScale = active == null ? 1f : 0f;
                    return;
                }
                bool completingPoliceTransfer = active.title == "继续调查";
                if (ShouldRouteThroughPoliceOffice(currentRoom, target))
                {
                    SchedulePoliceTransfer(target, local);
                    active = null;
                    MoveTo("work", new Vector2(-5.2f, -2.3f));
                    Time.timeScale = active == null ? 1f : 0f;
                    return;
                }
                if (completingDreamTransition) ClearDreamTransition();
                if (completingPoliceTransfer) ClearPoliceTransfer();
                active = null;
                MoveTo(target, local);
                Time.timeScale = active == null ? 1f : 0f;
                return;
            }
            string response = GetArray(active.choiceResponses, index);
            active = new GameObject("Choice Response").AddComponent<SceneHotspot>();
            active.title = "回应";
            active.body = response;
            pages = BuildPages(response);
            page = 0;
        }

        private bool IsDreamCorridorDoor(SceneHotspot hotspot)
        {
            if (hotspot == null || currentRoom != "corridor") return false;
            switch (hotspot.title)
            {
                case "第一夜之门":
                case "第二夜之门":
                case "第三夜之门":
                case "朦胧回忆之门":
                case "最终梦境之门":
                    return true;
                default:
                    return false;
            }
        }

        private bool CanOpenDreamDoor(string target)
        {
            if (string.IsNullOrWhiteSpace(unlockedDreamDoorTarget)) return false;
            return target == unlockedDreamDoorTarget;
        }

        private void OpenLockedDreamDoorResponse(string doorTitle)
        {
            SceneAudioController.Instance?.PlayPage();
            active = new GameObject("Locked Dream Door").AddComponent<SceneHotspot>();
            active.hideFlags = HideFlags.HideAndDontSave;
            active.title = doorTitle;
            active.body = "门把手冰冷，门缝后没有声音。\n\n这不是今晚会打开的门。你需要找到与当前夜晚对应的那一扇。";
            pages = BuildPages(active.body);
            page = 0;
        }

        private void UpdateDreamDoorLock(string sourceTitle, int choiceIndex, string target)
        {
            switch (sourceTitle)
            {
                case "第一天卷宗":
                case "监控背影":
                    unlockedDreamDoorTarget = "dream1";
                    break;
                case "结束问询":
                    unlockedDreamDoorTarget = "school";
                    break;
                case "搜查令":
                    unlockedDreamDoorTarget = choiceIndex == 0 ? "vagueMemory" : "family";
                    break;
                case "酒吧监控":
                    unlockedDreamDoorTarget = "finalDream";
                    break;
                case "凶器":
                case "受害者信息":
                case "现场证据":
                case "证据链":
                    unlockedDreamDoorTarget = "dream1";
                    break;
            }

            if (IsDreamCorridorDoor(active) && target == unlockedDreamDoorTarget) unlockedDreamDoorTarget = string.Empty;
        }

        private void MarkStoryProgress(string title)
        {
            switch (currentRoom)
            {
                case "day2scene":
                    if (title == "缺失的日记本") day2SceneItemsSeen.Add("diary");
                    break;
                case "interviewThief":
                    if (title == "盗贼") witnessItemsSeen.Add("thief");
                    break;
                case "interviewGirlfriend":
                    if (title == "前女友") witnessItemsSeen.Add("girlfriend");
                    break;
                case "interviewNeighbor":
                    if (title == "邻居") witnessItemsSeen.Add("neighbor");
                    break;
                case "clinic":
                    if (title == "日程表") clinicItemsSeen.Add("schedule");
                    if (title == "病例柜") clinicItemsSeen.Add("case");
                    break;
                case "evidenceRoom":
                    if (title == "丢失的安眠药") evidenceRoomItemsSeen.Add("sedative");
                    if (title == "搜查令") evidenceRoomItemsSeen.Add("warrant");
                    break;
                case "suspicion":
                    if (title == "同事的发现") suspicionItemsSeen.Add("colleague");
                    if (title == "回避通知") suspicionItemsSeen.Add("recusal");
                    break;
                case "renewed":
                    if (title == "重新尸检") renewedItemsSeen.Add("autopsy");
                    if (title == "邻居证词") renewedItemsSeen.Add("neighbor");
                    if (title == "酒吧监控")
                    {
                        renewedItemsSeen.Add("bar");
                        unlockedDreamDoorTarget = "finalDream";
                    }
                    break;
                case "memory":
                    if (title == "茶壶") fullMemoryItemsSeen.Add("tea");
                    if (title == "拆信刀") fullMemoryItemsSeen.Add("knife");
                    if (title == "受害者的座位") fullMemoryItemsSeen.Add("seat");
                    if (title == "被清理过的地面") fullMemoryItemsSeen.Add("floor");
                    break;
            }
        }

        private static string GetArray(string[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : string.Empty;
        }

        private void EnsureFlowHotspots()
        {
            EnsureDreamCorridorDoors();
            EnsurePoliceTransferHotspot();
            EnsureBedroomBedHotspot();

            if (!HasHotspot("结束问询"))
            {
                CreateRuntimeHotspot(
                    "结束问询",
                    "E 结束问询",
                    "三份口供被重新放回桌面。活人的说法没有给出完整答案，只把缺口指向同一个地方：夜晚，以及第二天之后仍未被打开的咨询所。\n\n你合上记录，先回警局办公室归档，再结束这一天。",
                    "回到卧室",
                    "调查暂时结束。你先回办公室整理口供，再从那里回到卧室。",
                    "bedroom",
                    BedroomEntryPosition,
                    "第二天问询已结束，线索指向第二夜梦境与第三天咨询所。",
                    GetRoomOrigin("interviews") + new Vector2(6.15f, -1.95f),
                    new Vector2(1.6f, 2.2f));
            }
        }

        private void EnsurePoliceTransferHotspot()
        {
            if (HasHotspot("继续调查")) return;
            CreateRuntimeHotspot(
                "继续调查",
                "E 继续调查",
                "你先回到警局办公室，把刚得到的线索写进记录。下一处调查地点已经标在桌面的便签上。",
                "前往下一调查地点",
                "你从办公室出发，继续推进案件。",
                "work",
                new Vector2(-5.2f, -2.3f),
                string.Empty,
                GetRoomOrigin("work") + new Vector2(4.9f, -2.05f),
                new Vector2(2.3f, 1.7f));
        }

        private void EnsureBedroomBedHotspot()
        {
            Vector2 position = GetRoomOrigin("bedroom") + new Vector2(-3.75f, -1.05f);
            foreach (SceneHotspot hotspot in hotspots)
            {
                if (hotspot != null && hotspot.title == "床")
                {
                    ConfigureBedroomBedHotspot(hotspot, position);
                    return;
                }
            }

            SceneHotspot bed = CreateRuntimeHotspot(
                "床",
                "E 入睡",
                "床单没有温度，枕头中央微微下陷。梦不会直接开始，它只从这里开始。",
                "入睡，进入梦境长廊",
                "雨点与心跳完全重叠。卧室的边界消失，你站到了梦境长廊前。",
                "corridor",
                CorridorEntryPosition,
                string.Empty,
                position,
                new Vector2(3.05f, 1.85f));
            ConfigureBedroomBedHotspot(bed, position);
        }

        private static void ConfigureBedroomBedHotspot(SceneHotspot hotspot, Vector2 position)
        {
            hotspot.gameObject.SetActive(true);
            hotspot.transform.position = new Vector3(position.x, position.y, hotspot.transform.position.z);
            hotspot.title = "床";
            hotspot.prompt = "E 入睡";
            hotspot.choiceLabels = new[] { "入睡，进入梦境长廊" };
            hotspot.choiceResponses = new[] { "雨点与心跳完全重叠。卧室的边界消失，你站到了梦境长廊前。" };
            hotspot.choiceTargetRooms = new[] { "corridor" };
            hotspot.choiceTargetLocalPositions = new[] { CorridorEntryPosition };

            BoxCollider2D box = hotspot.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.enabled = true;
                box.isTrigger = true;
                box.offset = Vector2.zero;
                box.size = new Vector2(3.05f, 1.85f);
            }

            if (hotspot.outline != null) hotspot.outline.gameObject.SetActive(false);
        }

        private bool IsHotspotAvailable(SceneHotspot hotspot)
        {
            if (hotspot == null) return false;
            if (hotspot.title == "继续调查") return currentRoom == "work" && !string.IsNullOrWhiteSpace(pendingPoliceTransferRoom);
            return true;
        }

        private void RefreshPoliceTransferHotspot(SceneHotspot hotspot)
        {
            if (hotspot == null || hotspot.title != "继续调查") return;
            string destination = string.IsNullOrWhiteSpace(pendingPoliceTransferLabel) ? GetRoomDisplayName(pendingPoliceTransferRoom) : pendingPoliceTransferLabel;
            hotspot.body = "你先回到警局办公室，把上一处线索写进记录。\n\n目的地：" + destination + "\n\n下一步：从办公室出发，继续调查。";
            hotspot.choiceLabels = new[] { "前往" + destination };
            hotspot.choiceResponses = new[] { "你带上记录，从警局办公室前往" + destination + "。" };
            hotspot.choiceTargetRooms = new[] { pendingPoliceTransferRoom };
            hotspot.choiceTargetLocalPositions = new[] { pendingPoliceTransferLocal };
        }

        private void RefreshDreamTransitionHotspot(SceneHotspot hotspot)
        {
            if (hotspot == null || hotspot.title != "未知之地") return;
            if (string.IsNullOrWhiteSpace(pendingDreamTransitionRoom))
            {
                hotspot.body = "黑墙在远处合拢，白塔像一根没有影子的针立在地面上。\n\n你还没有从任何梦境醒来。";
                hotspot.choiceLabels = new[] { "醒来" };
                hotspot.choiceResponses = new[] { "白塔后的光把你推回病房。" };
                hotspot.choiceTargetRooms = new[] { "hospital" };
                hotspot.choiceTargetLocalPositions = new[] { new Vector2(-4.7f, -2.15f) };
                return;
            }

            string destination = string.IsNullOrWhiteSpace(pendingDreamTransitionLabel) ? GetRoomDisplayName(pendingDreamTransitionRoom) : pendingDreamTransitionLabel;
            hotspot.body = "梦境没有立刻结束。黑墙切断了身后的房间，白塔在前方沉默地立着。\n\n目的地：" + destination + "\n\n下一步：穿过白塔后的空白，醒来进入下一段调查。";
            hotspot.choiceLabels = new[] { "醒来，前往" + destination };
            hotspot.choiceResponses = new[] { "白塔后的光吞没视野。你从梦里醒来，下一段调查已经在等你。" };
            hotspot.choiceTargetRooms = new[] { pendingDreamTransitionRoom };
            hotspot.choiceTargetLocalPositions = new[] { pendingDreamTransitionLocal };
        }

        private void SchedulePoliceTransfer(string targetRoom, Vector2 targetLocalPosition)
        {
            pendingPoliceTransferRoom = targetRoom;
            pendingPoliceTransferLocal = targetLocalPosition;
            pendingPoliceTransferLabel = GetRoomDisplayName(targetRoom);
        }

        private void ClearPoliceTransfer()
        {
            pendingPoliceTransferRoom = string.Empty;
            pendingPoliceTransferLocal = Vector2.zero;
            pendingPoliceTransferLabel = string.Empty;
        }

        private void ScheduleDreamTransition(string targetRoom, Vector2 targetLocalPosition)
        {
            pendingDreamTransitionRoom = targetRoom;
            pendingDreamTransitionLocal = targetLocalPosition;
            pendingDreamTransitionLabel = GetRoomDisplayName(targetRoom);
        }

        private void ClearDreamTransition()
        {
            pendingDreamTransitionRoom = string.Empty;
            pendingDreamTransitionLocal = Vector2.zero;
            pendingDreamTransitionLabel = string.Empty;
        }

        private void MarkFirstDayItemSeen(string title)
        {
            switch (title)
            {
                case "凶器":
                    firstDayItemsSeen.Add("weapon");
                    break;
                case "受害者信息":
                    firstDayItemsSeen.Add("victim");
                    break;
                case "现场证据":
                case "证据链":
                    firstDayItemsSeen.Add("scene");
                    break;
                case "监控背影":
                    firstDayItemsSeen.Add("camera");
                    break;
            }
        }

        private bool HasSeenAllFirstDayItems()
        {
            return firstDayItemsSeen.Contains("weapon")
                && firstDayItemsSeen.Contains("victim")
                && firstDayItemsSeen.Contains("scene")
                && firstDayItemsSeen.Contains("camera");
        }

        private static bool HasAll(HashSet<string> seen, params string[] required)
        {
            foreach (string item in required)
            {
                if (!seen.Contains(item)) return false;
            }
            return true;
        }

        private static bool ShouldRouteThroughDreamTransition(string sourceRoom, string targetRoom)
        {
            sourceRoom = CanonicalTwineTarget(sourceRoom);
            targetRoom = CanonicalTwineTarget(targetRoom);
            if (string.IsNullOrWhiteSpace(targetRoom)) return false;
            if (sourceRoom == targetRoom) return false;
            if (sourceRoom == "unknown" || targetRoom == "unknown") return false;
            if (!IsDreamRoom(sourceRoom)) return false;
            return !IsDreamRoom(targetRoom) || targetRoom == "memory";
        }

        private static bool IsDreamRoom(string roomId)
        {
            switch (CanonicalTwineTarget(roomId))
            {
                case "dream1":
                case "school":
                case "family":
                case "vagueMemory":
                case "finalDream":
                case "memory":
                    return true;
                default:
                    return false;
            }
        }

        private static bool ShouldRouteThroughPoliceOffice(string sourceRoom, string targetRoom)
        {
            sourceRoom = CanonicalTwineTarget(sourceRoom);
            targetRoom = CanonicalTwineTarget(targetRoom);
            if (string.IsNullOrWhiteSpace(targetRoom)) return false;
            if (sourceRoom == targetRoom) return false;
            if (!IsDayInvestigationRoom(sourceRoom)) return false;
            if (sourceRoom == "work") return false;
            if (targetRoom == "work") return false;
            return true;
        }

        private static bool IsDayInvestigationRoom(string roomId)
        {
            switch (roomId)
            {
                case "work":
                case "day2scene":
                case "interviews":
                case "interviewThief":
                case "interviewGirlfriend":
                case "interviewNeighbor":
                case "clinic":
                case "evidenceRoom":
                case "suspicion":
                case "renewed":
                    return true;
                default:
                    return false;
            }
        }

        private static string GetRoomDisplayName(string roomId)
        {
            switch (CanonicalTwineTarget(roomId))
            {
                case "work": return "警局办公室";
                case "day2scene": return "案发现场";
                case "interviews": return "证人问询室";
                case "interviewThief": return "盗贼问询";
                case "interviewGirlfriend": return "前女友问询";
                case "interviewNeighbor": return "邻居问询";
                case "clinic": return "心理咨询室";
                case "evidenceRoom": return "证物室";
                case "suspicion": return "警局怀疑阶段";
                case "renewed": return "重新调查";
                case "hospital": return "病房";
                case "memory": return "完整回忆";
                case "ending": return "天亮前";
                case "bedroom": return "卧室";
                default: return string.IsNullOrWhiteSpace(roomId) ? "下一调查地点" : roomId;
            }
        }

        private static readonly Vector2 BedroomEntryPosition = new Vector2(-4.8f, -2.3f);
        private static readonly Vector2 CorridorEntryPosition = new Vector2(-6.2f, -2.2f);

        private void EnsureDreamCorridorDoors()
        {
            Vector2 origin = GetRoomOrigin("corridor");
            CreateDreamDoorIfMissing("第一夜之门", origin + new Vector2(-4.8f, -1.15f), "第一夜：灰白客厅", "dream1", new Vector2(-5.2f, -2.3f));
            CreateDreamDoorIfMissing("第二夜之门", origin + new Vector2(-2.4f, -1.15f), "第二夜：教室", "school", new Vector2(-5.2f, -2.3f));
            CreateDreamDoorIfMissing("第三夜之门", origin + new Vector2(0f, -1.15f), "第三夜：餐桌", "family", new Vector2(-5.4f, -2.3f));
            CreateDreamDoorIfMissing("朦胧回忆之门", origin + new Vector2(2.4f, -1.15f), "第三夜：朦胧回忆", "vagueMemory", new Vector2(-4.8f, -2.2f));
            CreateDreamDoorIfMissing("最终梦境之门", origin + new Vector2(4.8f, -1.15f), "第六夜：最终梦境", "finalDream", new Vector2(-4.6f, -2.1f));
        }

        private void CreateDreamDoorIfMissing(string title, Vector2 position, string label, string targetRoom, Vector2 targetLocalPosition)
        {
            if (HasHotspot(title)) return;
            CreateRuntimeHotspot(
                title,
                "E 开门",
                "门后没有走廊，只有一段被分开的梦。",
                label,
                "你推开门。卧室、雨声和走廊一起向后退去。",
                targetRoom,
                targetLocalPosition,
                string.Empty,
                position,
                new Vector2(1.15f, 2.3f));
        }

        private bool HasHotspot(string title)
        {
            foreach (SceneHotspot hotspot in hotspots)
            {
                if (hotspot != null && hotspot.title == title) return true;
            }
            return false;
        }

        private SceneHotspot CreateRuntimeHotspot(string title, string prompt, string body, string choiceLabel, string choiceResponse, string targetRoom, Vector2 targetLocalPosition, string evidenceText, Vector2 position, Vector2 size)
        {
            GameObject obj = new GameObject("Runtime Hotspot - " + title);
            obj.hideFlags = HideFlags.DontSave;
            obj.transform.position = new Vector3(position.x, position.y, 0f);

            BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = size;

            SceneHotspot hotspot = obj.AddComponent<SceneHotspot>();
            hotspot.title = title;
            hotspot.prompt = prompt;
            hotspot.body = body;
            hotspot.evidence = evidenceText;
            hotspot.choiceLabels = new[] { choiceLabel };
            hotspot.choiceResponses = new[] { choiceResponse };
            hotspot.choiceTargetRooms = new[] { targetRoom };
            hotspot.choiceTargetLocalPositions = new[] { targetLocalPosition };
            hotspots.Add(hotspot);
            return hotspot;
        }

        private void NormalizeTwineFlowHotspots()
        {
            foreach (SceneHotspot hotspot in hotspots)
            {
                if (hotspot == null) continue;

                if (hotspot.choiceTargetRooms != null)
                {
                    for (int i = 0; i < hotspot.choiceTargetRooms.Length; i++)
                    {
                        hotspot.choiceTargetRooms[i] = CanonicalTwineTarget(hotspot.choiceTargetRooms[i]);
                    }
                }

                switch (hotspot.title)
                {
                    case "病房的门":
                        hotspot.title = "出院";
                        hotspot.prompt = "E 出院";
                        hotspot.body = "你确认自己是一名警探。医院没有给出答案，只把你送回现实。\n\n一周后，你回到工作岗位。";
                        hotspot.choiceLabels = new[] { "回到警局工作" };
                        hotspot.choiceResponses = new[] { "白色病房被关在身后。你带着空白的记忆回到卷宗前。" };
                        hotspot.choiceTargetRooms = new[] { "work" };
                        hotspot.choiceTargetLocalPositions = new[] { new Vector2(-5.2f, -2.3f) };
                        break;
                    case "医生":
                        hotspot.choiceLabels = new[] { "结束交谈" };
                        hotspot.choiceResponses = new[] { "医生退后一步，示意你先确认床头柜上的病历，再从病房门离开。" };
                        hotspot.choiceTargetRooms = Array.Empty<string>();
                        hotspot.choiceTargetLocalPositions = Array.Empty<Vector2>();
                        break;
                    case "整理后的卷宗":
                        hotspot.title = "第一天卷宗";
                        hotspot.prompt = "E 研读卷宗";
                        ConfigureFirstDayCaseFile(hotspot);
                        break;
                    case "卷宗":
                        hotspot.title = "第一天卷宗";
                        hotspot.prompt = "E 研读卷宗";
                        hotspot.choiceLabels = new[] { "重新按第一天调查" };
                        hotspot.choiceResponses = new[] { "你把过早串联的结论拆开，重新从第一天卷宗开始。" };
                        hotspot.choiceTargetRooms = new[] { "work" };
                        hotspot.choiceTargetLocalPositions = new[] { new Vector2(-5.2f, -2.3f) };
                        break;
                    case "第一天卷宗":
                        ConfigureFirstDayCaseFile(hotspot);
                        break;
                    case "凶器":
                        RemoveSceneAdvanceChoices(hotspot, "尸检和缺失的刀具被记入卷宗。\n\n下一步：继续查看警局里的其他关键物品。看完后按 F 决定是否进入下一场景。");
                        break;
                    case "受害者信息":
                        RemoveSceneAdvanceChoices(hotspot, "受害者信息被记入卷宗。\n\n下一步：继续查看警局里的其他关键物品。看完后按 F 决定是否进入下一场景。");
                        break;
                    case "现场证据":
                    case "证据链":
                        RemoveSceneAdvanceChoices(hotspot, "现场证据被记入卷宗。\n\n下一步：继续查看警局里的其他关键物品。看完后按 F 决定是否进入下一场景。");
                        break;
                    case "监控背影":
                        RemoveSceneAdvanceChoices(hotspot, "监控背影被记入卷宗。\n\n第一天调查是否结束，由你按 F 确认；夜晚不会直接开始，必须先回到卧室。");
                        break;
                    case "缺失的日记本":
                        hotspot.body = "卧室被翻找过，抽屉和衣柜都留下了被人急切打开的痕迹。\n\n贵重物品确实不见了。手机也不在房间里。最奇怪的是日记本：据前期记录，死者有长期书写日记的习惯，可书桌、床头柜、抽屉里都没有。\n\n如果只是入室盗窃，日记本没有价值；如果是为了隐瞒某种关系，它的消失反而比财物更重要。\n\n你把这一点记进现场记录。下一步应回警局，传唤和失窃、关系、争吵有关的人。";
                        hotspot.evidence = "案发现场：死者日记本、手机与贵重物品缺失；日记本的消失不像普通盗窃。";
                        RemoveSceneAdvanceChoices(hotspot, "案发现场记录完成后，按 F 回警局中转并进入证人问询。");
                        break;
                    case "日程表":
                        hotspot.body = "咨询所的日程表保存得非常克制：姓名缩写、时间、地点、极短备注，像受害者刻意不给旁人留下过多私人信息。\n\n其中几行标注为“住所会面”。前女友的说法在这里得到印证：受害者确实会为了保护咨询者隐私，把部分交流放到家中。\n\n你继续往下看，发现一个与你姓名缩写一致的记录。日期在案发前两天。\n\n同事靠在门边，随口打趣说这也许只是巧合。你笑了一下，但手指已经停在病例柜的方向。";
                        hotspot.evidence = "咨询所日程表：主角姓名缩写出现在案发前两天的会面记录中。";
                        hotspot.choiceLabels = Array.Empty<string>();
                        hotspot.choiceResponses = Array.Empty<string>();
                        hotspot.choiceTargetRooms = Array.Empty<string>();
                        hotspot.choiceTargetLocalPositions = Array.Empty<Vector2>();
                        break;
                    case "病例柜":
                        hotspot.body = "你把同事支开。理由很普通：去确认走廊另一侧的访客登记，或者帮你联系鉴证科。\n\n抽屉滑开时没有发出声音，像这只柜子早已习惯保守秘密。你找到两个相同缩写的记录，其中一个夹着照片。\n\n照片上的人是你。\n\n记录没有写明完整姓名，只写着你曾反复提及一件过去案件。受害者在“秘密”这个词上画了圈，旁边还有一句未完成的备注：如果继续回避，可能需要外部介入。\n\n你把纸页放回原位，动作快得近乎本能。你不知道自己是在保护调查，还是在保护自己。";
                        hotspot.evidence = "同名病例：主角曾接受受害者咨询；受害者在“秘密”一词上画圈。";
                        hotspot.choiceLabels = new[] { "支开同事查看", "按捺好奇放回" };
                        hotspot.choiceResponses = new[]
                        {
                            "你看见照片，确认同名记录指向你自己。这个秘密不再只是梦中的词。",
                            "你没有继续翻看，但你已经知道柜子里有东西在等你。"
                        };
                        hotspot.choiceTargetRooms = Array.Empty<string>();
                        hotspot.choiceTargetLocalPositions = Array.Empty<Vector2>();
                        break;
                    case "走廊尽头的门":
                        if (hotspot.choiceTargetRooms != null && hotspot.choiceTargetRooms.Length == 1 && hotspot.choiceTargetRooms[0] == "clinic")
                        {
                            hotspot.choiceLabels = new[] { "醒来问询前女友" };
                            hotspot.choiceResponses = new[] { "你从第二夜教室醒来。日记本线索把你带向前女友的问询。" };
                            hotspot.choiceTargetRooms = new[] { "interviewGirlfriend" };
                            hotspot.choiceTargetLocalPositions = new[] { new Vector2(-5.2f, -2.3f) };
                        }
                        break;
                    case "前女友":
                        if (Mathf.Abs(hotspot.transform.position.x - GetRoomOrigin("interviewGirlfriend").x) < roomSize.x * 0.5f)
                        {
                            hotspot.body = "前女友的第一句话不是辩解，而是反问：“你们终于想起来问我了？”\n\n她承认不久前刚和受害者分手，原因是性格不合。那些社交媒体上的极端话语，她说只是为了发泄和博取眼球，从来没有准备付诸行动。\n\n真正有用的是另一部分：受害者有记日记的习惯，也会把部分咨询安排写进一本工作日程表。为了保护来访者隐私，他偶尔允许一些人在私人时间登门咨询。\n\n她提到，受害者曾把卧室重新做过隔音处理，因为家中谈话不能让邻居听见太多。这个细节让案发当晚的争吵变得更加异常：如果声音能传出去，那场争吵一定离客厅或门口很近。\n\n她最后说，自己曾送过一些贵重物品，也确实说过“想追回来”，但她不会用撬锁这种方式。她看起来愤怒，却不像在隐瞒一场杀人。";
                            hotspot.evidence = "前女友证词：受害者有日记和工作日程表；会安排私人住所咨询；卧室做过隔音。";
                            SetSingleChoice(
                                hotspot,
                                "结束问询",
                                "前女友提到另一本作为日程表使用的记录。你先结束这段问询。",
                                "interviews",
                                new Vector2(-5.2f, -2.3f));
                        }
                        break;
                    case "盗贼":
                        if (Mathf.Abs(hotspot.transform.position.x - GetRoomOrigin("interviewThief").x) < roomSize.x * 0.5f)
                        {
                            hotspot.body = "盗贼坐下时先看了看门，像已经习惯每一间问询室都通向同一个结果。\n\n他承认自己过去两次因盗窃入狱，也承认命案发生前不久出入过这栋公寓。但他说那不是踩点，而是去帮邻居处理生活杂事。\n\n邻居腿脚受过伤，偶尔会花钱请他搬东西、买药、修一点小东西。案发当天，他没有进入受害者家中，也没有见过那本日记。\n\n当你提到门上的记号，他的表情变得烦躁：“那种记号谁都能学。你们想找一个最像小偷的人，这很容易。”\n\n他与案件的联系并没有消失，但它开始从“凶手”变成“误导证据”。如果盗窃痕迹被人利用，那么真正懂得利用它的人未必是盗贼。";
                            hotspot.evidence = "盗贼证词：曾出入公寓是为照顾邻居；入室盗窃痕迹可能被人利用。";
                            SetSingleChoice(hotspot, "回到问询室", "盗贼的口供让证据链第一次出现断口。", "interviews", new Vector2(-5.2f, -2.3f));
                        }
                        break;
                    case "邻居":
                        if (Mathf.Abs(hotspot.transform.position.x - GetRoomOrigin("interviewNeighbor").x) < roomSize.x * 0.5f)
                        {
                            hotspot.body = "邻居坐下后很久没有说话。他不是不知道，而是不想再被司法系统卷进去。\n\n过去一场民事诉讼几乎拖垮了他的生活，最后也没有给他一个他能接受的公正结果。从那以后，他学会了把门关紧，把声音调低，把所有麻烦挡在门外。\n\n你提到盗贼，他承认对方确实帮过自己。你提到案发夜，他的手指在桌面上停了一下。\n\n他说那晚听见过争吵，是两个男性。一个声音属于受害者，另一个他不认识。他隐约听见争吵和“秘密”有关，但不清楚秘密是什么。\n\n当你追问声音特征，他回避了目光：“很熟悉。但我当时觉得这想法太荒唐。”\n\n这句话没有直接指向任何人，却把案件从盗窃推向了第三人在场。";
                            hotspot.evidence = "邻居证词：案发夜有两个男性争吵，内容涉及秘密；另一个声音让他觉得熟悉。";
                            SetSingleChoice(hotspot, "回到问询室", "邻居没有说出名字，但证词已经排除了单纯盗窃作案。", "interviews", new Vector2(-5.2f, -2.3f));
                        }
                        break;
                    case "搜查令":
                        if (hotspot.choiceLabels != null && hotspot.choiceLabels.Length >= 2)
                        {
                            hotspot.body = "证物室里，丢失的安眠药把旧案和当前案件接在一起。\n\n搜查令申请已经摆在桌面。你知道它的方向：排查受害者隐秘咨询者，调取病例柜，确认案发前两天与受害者见过面的那个人。\n\n你可以尝试阻挠，让调查先从朦胧回忆滑过；也可以不再回避，让它更快撞向家庭餐桌之后的完整线索。\n\n无论选择哪一个，证物室都不会直接进入梦境。你仍然必须回到卧室，从床上进入梦境长廊。";
                            hotspot.evidence = "证物室：搜查令将排查受害者的隐秘咨询者，主角与受害者的关系即将暴露。";
                            hotspot.choiceLabels = new[] { "尝试阻挠搜查令", "不再回避搜查令" };
                            hotspot.choiceResponses = new[]
                            {
                                "你试图让申请在流程中慢下来。今晚的梦会先以朦胧回忆出现。",
                                "你没有再挡住它。今晚的梦会更快回到家庭餐桌。"
                            };
                            hotspot.choiceTargetRooms = Array.Empty<string>();
                            hotspot.choiceTargetLocalPositions = Array.Empty<Vector2>();
                        }
                        break;
                    case "丢失的安眠药":
                        hotspot.body = "证物室记录显示，一瓶处方安眠药不见了。\n\n它并不属于这起心理咨询师遇害案，而来自你过去经手的一桩旧案。那桩案子结案得很快，证据链干净，嫌疑人也被顺利送进监狱。\n\n现在这瓶药从证物室消失，时间却与受害者死亡前后能够对上。\n\n如果凶手使用它让受害者失去反抗能力，那么当前案子的凶器就不只是一把刀。它还包括权限、旧案、证物室和一个熟悉流程的人。\n\n你第一次意识到，真正危险的不是别人怀疑你，而是这条线索本身太懂你。";
                        hotspot.evidence = "证物室：旧案处方安眠药丢失，可能与受害者体内镇静剂痕迹相关。";
                        RemoveSceneAdvanceChoices(hotspot, "继续查看搜查令。看完证物室关键物品后按 F 回到卧室。");
                        break;
                    case "同事的发现":
                        hotspot.body = "同事的调查结果被放在桌上。纸张边缘有折痕，说明它已经被反复读过。\n\n他没有直接指责你，只把病例柜里的信息摆出来：受害者曾长期接触你，而你从未在调查中提起这段关系。\n\n更糟的是，那份记录并不是普通咨询。它提到你对某桩过去案件表现出强烈痛苦，却拒绝说明具体原因。受害者在“秘密”旁边画了圈。\n\n这不是决定你有罪的证据，却足以让你不再适合继续负责此案。\n\n房间里没有人说话。沉默本身已经完成了投票。";
                        hotspot.evidence = "同事发现：主角与受害者存在未披露的咨询关系。";
                        RemoveSceneAdvanceChoices(hotspot, "继续查看回避通知。看完怀疑阶段关键物品后按 F 进入第六天重新调查。");
                        break;
                    case "回避通知":
                        hotspot.body = "回避通知写得非常正式，正式到几乎没有情绪。\n\n你被要求离开调查组。新的负责人会接手案件，重新核查现场、证词和证物室记录。\n\n过去你总是站在桌子的另一侧，用同样的格式要求别人配合调查。现在那种格式转向了你，像一面没有镜框的镜子。\n\n警局里并非所有人都相信你有问题。有人仍然记得你过去的破案率，有人相信你只是失忆后的判断失误。但制度不会因为信任而停止运转。\n\n它终于开始调查你。";
                        hotspot.evidence = "回避通知：主角因未披露咨询关系被调离调查组。";
                        RemoveSceneAdvanceChoices(hotspot, "怀疑阶段信息已记录。按 F 进入第六天重新调查。");
                        break;
                    case "重新尸检":
                        hotspot.body = "重新尸检报告比第一次更短，也更致命。\n\n法医在受害者体内发现镇静剂残留。剂量不足以单独致死，却足以让一个成年人反应迟缓、判断变慢、难以及时抵抗。\n\n这解释了现场矛盾：为什么死者有多处刀伤，却没有留下足够完整的反抗痕迹；为什么所谓搏斗看起来更像被人事后整理。\n\n报告末尾写着：需要与证物室丢失药物进行比对。\n\n你看见那句话时，感觉整张纸都在向你靠近。";
                        hotspot.evidence = "重新尸检：受害者体内发现镇静剂残留。";
                        RemoveSceneAdvanceChoices(hotspot, "继续查看邻居新证词和酒吧监控。三项都看完后按 F 回卧室。");
                        break;
                    case "邻居证词":
                        hotspot.body = "新的负责人再次找到邻居。这一次，邻居没有再把所有话吞回去。\n\n他知道自己已经被卷进案件，也知道刀具指纹让自己无法继续置身事外。他终于承认，那晚争吵的另一个声音与盗贼不符。\n\n他形容那个声音很冷静，压得很低，像一个习惯控制局面的人在失控前最后一次维持体面。\n\n他说自己当时觉得那个声音像你，但这个想法太荒唐，所以没有说。\n\n现在荒唐感消失了。留下来的只是证词。";
                        hotspot.evidence = "邻居新证词：案发夜另一个男性声音像主角。";
                        RemoveSceneAdvanceChoices(hotspot, "继续查看重新尸检和酒吧监控。三项都看完后按 F 回卧室。");
                        break;
                    case "酒吧监控":
                        hotspot.body = "监控一路追踪那个被忽略的背影。\n\n他先回家换衣服，再去了一个酒吧。那间酒吧正是你失忆前最后出现过的地方。\n\n画面质量很差，脸始终没有拍清。但走路姿态、停顿的位置、进门前摸口袋的动作，都与你过分相似。\n\n新的负责人没有直接说出结论，只让人把你控制起来。你在熟悉的城市、熟悉的流程里，第一次以嫌疑人的身份度过夜晚。\n\n最终梦境已经被打开，但它仍然要从卧室、床和梦境长廊开始。";
                        hotspot.evidence = "酒吧监控：与主角相似的背影回家换衣后前往酒吧，连接到失忆前夜。";
                        hotspot.choiceLabels = Array.Empty<string>();
                        hotspot.choiceResponses = Array.Empty<string>();
                        hotspot.choiceTargetRooms = Array.Empty<string>();
                        hotspot.choiceTargetLocalPositions = Array.Empty<Vector2>();
                        break;
                    case "床":
                        ConfigureBedroomBedHotspot(hotspot, GetRoomOrigin("bedroom") + new Vector2(-3.75f, -1.05f));
                        hotspot.choiceLabels = new[] { "入睡，进入梦境长廊" };
                        hotspot.choiceResponses = new[] { "雨点与心跳完全重叠。卧室的边界消失，你站到了梦境长廊前。" };
                        hotspot.choiceTargetRooms = new[] { "corridor" };
                        hotspot.choiceTargetLocalPositions = new[] { CorridorEntryPosition };
                        break;
                    case "镜":
                    case "灰白客厅":
                        hotspot.choiceTargetRooms = new[] { "work" };
                        hotspot.choiceTargetLocalPositions = new[] { new Vector2(-5.2f, -2.3f) };
                        break;
                    case "镜中的问题":
                        hotspot.choiceTargetRooms = new[] { "suspicion" };
                        hotspot.choiceTargetLocalPositions = new[] { new Vector2(-5.0f, -2.2f) };
                        break;
                    case "模糊的人影":
                        hotspot.body = LoadNarrative("MirrorProjectionTalk");
                        hotspot.prompt = "E 面对人影";
                        hotspot.evidence = "餐桌梦中的人影指出：主角与案件的关系不是旁观者。";
                        hotspot.choiceLabels = new[] { "醒来面对怀疑" };
                        hotspot.choiceResponses = new[] { "那些声音没有消失，只是从餐桌后退回现实。" };
                        hotspot.choiceTargetRooms = new[] { "suspicion" };
                        hotspot.choiceTargetLocalPositions = new[] { new Vector2(-5.0f, -2.2f) };
                        break;
                    case "茶壶":
                        if (Mathf.Abs(hotspot.transform.position.x - GetRoomOrigin("memory").x) < roomSize.x * 0.5f)
                        {
                            hotspot.body = "茶壶端坐在桌面中央，壶身线条优雅，釉色沉静。\n\n你确实喜爱这种审美：克制、不张扬、看起来比里面盛着的东西更体面。可壶里不是好茶，只是廉价袋泡红茶，泡得太久，颜色浓到发黑。\n\n那晚你在里面动过手脚。你告诉自己这只是为了让谈话更容易进行，为了让对方冷静一点。\n\n但你早就知道接下来要做什么。\n\n茶壶之后被你摔碎。碎片不是失控的结果，而是你销毁证据、伪装搏斗的一部分。";
                            hotspot.evidence = "完整回忆：主角在茶壶中投入安眠药，之后摔碎茶壶销毁痕迹。";
                            RemoveSceneAdvanceChoices(hotspot, "继续查看拆信刀、受害者座位和被清理过的地面。");
                        }
                        break;
                    case "被清理过的地面":
                        hotspot.body = "地面被清理过，但不是完全干净。\n\n真正熟练的人不会把所有痕迹抹去，那样反而显眼。他只会留下足够多、足够合理、足够能让别人得出错误结论的痕迹。\n\n碎片的位置避开了最自然的飞散方向。拖拽痕被重新盖住，血迹边缘有一处过分平滑。你几乎能看见自己蹲在这里，一点点把现场整理成“入室盗窃后发生搏斗”的样子。\n\n手机、日记本、拆信刀和那把真正的凶器都被带走。你不是忘了它们，你是把它们从现实里拿开，又让酒精从记忆里把自己拿开。\n\n完整回忆已经拼合。下一步不是再找证据，而是决定是否写下最后的陈述。";
                        hotspot.evidence = "完整回忆：主角清理现场，带走日记本、手机、拆信刀和凶器，伪装成入室盗窃后的打斗。";
                        hotspot.choiceLabels = Array.Empty<string>();
                        hotspot.choiceResponses = Array.Empty<string>();
                        hotspot.choiceTargetRooms = Array.Empty<string>();
                        hotspot.choiceTargetLocalPositions = Array.Empty<Vector2>();
                        break;
                    case "拆信刀":
                        hotspot.body = "拆信刀放在桌边，位置熟悉得像一件本该属于你的东西。\n\n它很华丽，线条锋利，带着你喜欢的克制装饰。你想起自己曾把它送给受害者，像送出一件无害的礼物，也像把某种自我形象放进他的生活。\n\n那晚它离你太近。受害者因为茶里的药变得迟缓，你的愤怒却越来越清醒。\n\n它不是厨房里缺失的那把刀，却与最后的刺入动作重叠。你终于明白为什么失物清单里没有它：你亲手把它从现场拿走了。";
                        hotspot.evidence = "完整回忆：拆信刀曾由主角赠予受害者，并被主角带离现场。";
                        RemoveSceneAdvanceChoices(hotspot, "继续查看茶壶、受害者座位和被清理过的地面。");
                        break;
                    case "受害者的座位":
                        hotspot.body = "受害者坐在这里，声音没有起伏。\n\n他并不是用愤怒逼你认罪，而是用一种近乎残酷的平静说明事实：你过去为了声誉，在不完整的证据链里补上过不该补的环。\n\n其中一桩旧案牵连到他的患者。那名患者的家人被送进监狱，而受害者从细节里意识到，那条证据链可能被人为修整过。\n\n他给过你机会。他希望你自首，希望你公开那些你称为“必要灰色地带”的罪。\n\n你坐在对面，听见“名声不是你的一切”。那句话像一把真正的刀。";
                        hotspot.evidence = "完整回忆：受害者准备揭露主角在旧案中伪造证据。";
                        RemoveSceneAdvanceChoices(hotspot, "继续查看茶壶、拆信刀和被清理过的地面。");
                        break;
                }
            }
        }

        private static void SetSingleChoice(SceneHotspot hotspot, string label, string response, string targetRoom, Vector2 targetLocalPosition)
        {
            hotspot.choiceLabels = new[] { label };
            hotspot.choiceResponses = new[] { response };
            hotspot.choiceTargetRooms = new[] { targetRoom };
            hotspot.choiceTargetLocalPositions = new[] { targetLocalPosition };
        }

        private static void RemoveSceneAdvanceChoices(SceneHotspot hotspot, string bodySuffix)
        {
            if (!string.IsNullOrWhiteSpace(bodySuffix) && (string.IsNullOrWhiteSpace(hotspot.body) || !hotspot.body.Contains("按 F")))
            {
                hotspot.body = (hotspot.body ?? string.Empty).TrimEnd() + "\n\n" + bodySuffix;
            }
            hotspot.choiceLabels = Array.Empty<string>();
            hotspot.choiceResponses = Array.Empty<string>();
            hotspot.choiceEvidence = Array.Empty<string>();
            hotspot.choiceTargetRooms = Array.Empty<string>();
            hotspot.choiceTargetLocalPositions = Array.Empty<Vector2>();
        }

        private static void ConfigureFirstDayCaseFile(SceneHotspot hotspot)
        {
            hotspot.choiceLabels = new[]
            {
                "查看凶器",
                "查看受害者信息",
                "查看入室痕迹",
                "查看搏斗痕迹",
                "查看监控背影"
            };
            hotspot.choiceResponses = new[]
            {
                "卷宗首先指向尸检和缺失刀具。",
                "卷宗首先指向受害者身份。",
                "卷宗首先指向入室痕迹和失物。",
                "卷宗首先指向客厅中的搏斗痕迹。",
                "卷宗最后指向那道与你相似的监控背影。"
            };
            hotspot.choiceTargetRooms = new[] { "work", "work", "work", "work", "work" };
            hotspot.choiceTargetLocalPositions = new[]
            {
                new Vector2(-2.0f, -2.4f),
                new Vector2(-0.4f, -2.4f),
                new Vector2(1.25f, -2.4f),
                new Vector2(1.25f, -2.4f),
                new Vector2(-3.65f, 1.0f)
            };
        }

        private static string CanonicalTwineTarget(string roomId)
        {
            switch (roomId)
            {
                case "police":
                case "briefing":
                    return "work";
                case "mirror":
                    return "dream1";
                case "mirror2":
                    return "suspicion";
                case "crime":
                case "missing":
                case "relation":
                    return "day2scene";
                default:
                    return roomId;
            }
        }

        private void MoveTo(string roomId, Vector2 local)
        {
            roomId = CanonicalTwineTarget(roomId);
            bool changedRoom = currentRoom != roomId;
            currentRoom = roomId;
            Vector2 origin = GetRoomOrigin(roomId);
            ApplyPlayerLogicalScale();
            NormalizeSceneHumanScale();
            if (player != null) player.position = new Vector3(origin.x + local.x, origin.y + local.y, 0f);
            if (sceneCamera != null) sceneCamera.transform.position = new Vector3(origin.x, origin.y, -10f);
            ApplyStandardCameraScale();
            if (changedRoom) SceneAudioController.Instance?.PlayTransition();
            SceneAudioController.Instance?.SetRoom(roomId);
            if (!mainMenu) OpenRoomIntro(roomId);
        }

        private void ApplyStandardCameraScale()
        {
            if (sceneCamera == null) return;
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = StandardRoomHeight * 0.5f;
        }

        private Vector3 ResolveRoomBlocks(Vector3 current, Vector3 requested)
        {
            if (DisableRoomBlocksTemporarily) return requested;
            if (currentRoom != "hospital"
                && currentRoom != "clinic"
                && currentRoom != "police"
                && currentRoom != "bedroom"
                && currentRoom != "school"
                && currentRoom != "family") return requested;

            Vector3 horizontal = new Vector3(requested.x, current.y, requested.z);
            Vector3 vertical = new Vector3(horizontal.x, requested.y, requested.z);
            if (IsInsideRoomBlock(horizontal)) horizontal.x = current.x;
            if (IsInsideRoomBlock(vertical)) vertical.y = current.y;
            return vertical;
        }

        private bool IsInsideRoomBlock(Vector3 position)
        {
            if (currentRoom == "clinic") return IsInsideClinicBlock(position);
            if (currentRoom == "police") return IsInsidePoliceBlock(position);
            if (currentRoom == "bedroom") return IsInsideBedroomBlock(position);
            if (currentRoom == "school") return IsInsideSchoolBlock(position);
            if (currentRoom == "family") return IsInsideFamilyBlock(position);
            return IsInsideHospitalBlock(position);
        }

        private static bool IsInsideHospitalBlock(Vector3 position)
        {
            Vector2 p = position;
            return InRect(p, new Vector2(34.55f, 0.55f), new Vector2(1.55f, 2.9f)) // monitor cart
                || InRect(p, new Vector2(36.35f, -0.45f), new Vector2(1.45f, 1.9f)) // bedside cabinet
                || InRect(p, new Vector2(38.0f, -1.38f), new Vector2(3.9f, 3.25f)) // bed
                || InRect(p, new Vector2(40.45f, -0.45f), new Vector2(0.95f, 3.75f)) // IV stand
                || InRect(p, new Vector2(42.1f, -1.05f), new Vector2(1.45f, 3.45f)) // doctor
                || InRect(p, new Vector2(43.55f, -0.1f), new Vector2(1.85f, 3.85f)); // medicine cabinet
        }

        private static bool IsInsideClinicBlock(Vector3 position)
        {
            Vector2 p = position;
            return InRect(p, new Vector2(754.15f, 1.45f), new Vector2(4.05f, 2.95f)) // left file cabinet
                || InRect(p, new Vector2(759.45f, -0.65f), new Vector2(4.7f, 1.55f)) // consultation desk
                || InRect(p, new Vector2(759.1f, -2.65f), new Vector2(2.55f, 1.65f)) // visitor chair
                || InRect(p, new Vector2(759.5f, 0.85f), new Vector2(1.55f, 1.15f)) // desk chair
                || InRect(p, new Vector2(764.0f, 0.7f), new Vector2(1.25f, 2.25f)) // plant
                || InRect(p, new Vector2(765.65f, 1.35f), new Vector2(2.0f, 2.85f)) // right bookcase
                || InRect(p, new Vector2(763.8f, -1.25f), new Vector2(0.9f, 1.15f)) // side table
                || InRect(p, new Vector2(765.25f, -1.65f), new Vector2(3.3f, 2.75f)); // sofa
        }

        private static bool IsInsidePoliceBlock(Vector3 position)
        {
            Vector2 p = position;
            return InRect(p, new Vector2(74.45f, 1.25f), new Vector2(1.8f, 3.6f)) // left file cabinets
                || InRect(p, new Vector2(85.65f, 1.25f), new Vector2(1.8f, 3.6f)) // right file cabinets
                || InRect(p, new Vector2(80.0f, -2.25f), new Vector2(4.85f, 2.05f)) // foreground desk
                || InRect(p, new Vector2(80.0f, -3.35f), new Vector2(1.45f, 1.35f)) // desk chair
                || InRect(p, new Vector2(80.2f, 2.35f), new Vector2(5.4f, 2.2f)) // evidence board
                || InRect(p, new Vector2(86.25f, -2.7f), new Vector2(1.35f, 1.4f)); // trash and boxes
        }

        private static bool IsInsideBedroomBlock(Vector3 position)
        {
            Vector2 p = position;
            return InRect(p, new Vector2(115.0f, 0.6f), new Vector2(2.8f, 2.65f)) // desk and left storage
                || InRect(p, new Vector2(116.0f, -1.75f), new Vector2(2.2f, 1.9f)) // chair and floor clutter
                || InRect(p, new Vector2(122.25f, -0.75f), new Vector2(1.45f, 1.45f)) // nightstand
                || InRect(p, new Vector2(124.35f, -1.35f), new Vector2(3.85f, 3.2f)) // bed
                || InRect(p, new Vector2(126.55f, 0.15f), new Vector2(1.15f, 2.35f)) // mirror
                || InRect(p, new Vector2(126.6f, -2.85f), new Vector2(1.3f, 2.25f)); // door-side clutter
        }

        private static bool IsInsideSchoolBlock(Vector3 position)
        {
            Vector2 p = position;
            return InRect(p, new Vector2(400.0f, 0.85f), new Vector2(4.2f, 1.45f)) // teacher desk and roll book
                || InRect(p, new Vector2(396.95f, -0.75f), new Vector2(2.05f, 1.45f)) // first row left
                || InRect(p, new Vector2(400.0f, -0.85f), new Vector2(2.1f, 1.5f)) // first row center
                || InRect(p, new Vector2(403.05f, -0.75f), new Vector2(2.05f, 1.45f)) // first row right
                || InRect(p, new Vector2(396.95f, -2.35f), new Vector2(2.1f, 1.65f)) // second row left
                || InRect(p, new Vector2(400.0f, -2.55f), new Vector2(2.1f, 1.75f)) // second row center
                || InRect(p, new Vector2(403.05f, -2.35f), new Vector2(2.1f, 1.65f)) // second row right
                || InRect(p, new Vector2(406.75f, 0.25f), new Vector2(1.25f, 3.6f)) // right doorway mirror
                || InRect(p, new Vector2(393.25f, 0.55f), new Vector2(1.25f, 3.0f)); // left window wall
        }

        private static bool IsInsideFamilyBlock(Vector3 position)
        {
            Vector2 p = position;
            return InRect(p, new Vector2(440.0f, -1.15f), new Vector2(4.35f, 1.85f)) // dining table
                || InRect(p, new Vector2(438.0f, -0.65f), new Vector2(1.15f, 1.95f)) // left seated figure
                || InRect(p, new Vector2(442.0f, -0.65f), new Vector2(1.15f, 1.95f)) // right seated figure
                || InRect(p, new Vector2(440.0f, -2.25f), new Vector2(1.35f, 1.55f)) // foreground seated figure
                || InRect(p, new Vector2(433.75f, 0.55f), new Vector2(1.4f, 2.6f)); // plant
        }

        private static bool InRect(Vector2 point, Vector2 center, Vector2 size)
        {
            return Mathf.Abs(point.x - center.x) <= size.x * 0.5f
                && Mathf.Abs(point.y - center.y) <= size.y * 0.5f;
        }

        private void ConfigureHospitalRuntimePresentation()
        {
            HideHospitalPropRenderers();
            ResizeHospitalHotspots("病历", new Vector2(36.55f, 0.7f), new Vector2(1.15f, 0.95f));
            ResizeHospitalHotspots("医生", new Vector2(42.1f, -0.25f), new Vector2(1.35f, 3.2f));
            ConfigureHospitalExitHotspots();
        }

        private void HideHospitalPropRenderers()
        {
            string[] names =
            {
                "logical heart monitor",
                "logical hospital bed",
                "logical hospital bedside cabinet",
                "logical hospital bedside medical record",
                "logical medicine cabinet",
                "logical doctor",
                "logical hospital records cabinet",
                "hospital quiet iv stand",
                "hospital lamp",
                "bedside reading lamp",
                "hospital wall pipe",
                "hospital empty visitor chair",
                "hospital untouched water cup",
                "hospital box",
                "Pixel Hospital Bed",
                "Hospital Bed Visible Replacement",
                "Hospital Bed Block Model",
                "Pixel Hospital Bed Shadow",
                "hospital bed cold floor shadow"
            };

            HashSet<string> hiddenNames = new HashSet<string>(names);
            foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || renderer.gameObject.name == "logical hospital room background") continue;
                bool oldHospitalLayer = renderer.gameObject.name.Contains("hospital") || renderer.gameObject.name.Contains("Hospital");
                bool exitDoorLayer = renderer.gameObject.name.Contains("exit door") || renderer.gameObject.name.Contains("Police Door");
                if (exitDoorLayer) renderer.enabled = true;
                else if (hiddenNames.Contains(renderer.gameObject.name) || oldHospitalLayer) renderer.enabled = false;
            }
        }

        private void NormalizeSceneHumanScale()
        {
            foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || renderer == playerSpriteRenderer || !IsStandingHumanRenderer(renderer.gameObject.name)) continue;
                ScaleRendererToHeight(renderer, GetStandingHumanHeight());
            }
        }

        private static bool IsStandingHumanRenderer(string objectName)
        {
            return objectName == "另一个你"
                || objectName == "看不清脸的人"
                || objectName.Contains("Doctor Pixel Sprite")
                || objectName.Contains("Faceless Person")
                || objectName.Contains("Standing Figure");
        }

        private static void ScaleRendererToHeight(SpriteRenderer renderer, float targetHeight)
        {
            if (renderer.sprite == null || renderer.sprite.bounds.size.y <= 0f) return;
            Vector3 scale = renderer.transform.localScale;
            float signX = scale.x < 0f ? -1f : 1f;
            float nextScale = targetHeight / renderer.sprite.bounds.size.y;
            renderer.transform.localScale = new Vector3(signX * nextScale, nextScale, scale.z);
        }

        private void ConfigureHospitalExitHotspots()
        {
            bool configuredAny = false;
            foreach (SceneHotspot hotspot in hotspots)
            {
                if (hotspot == null || hotspot.title != "出院") continue;
                ConfigureHospitalExitHotspot(hotspot);
                configuredAny = true;
            }

            if (!configuredAny)
            {
                SceneHotspot exit = CreateRuntimeHotspot(
                    "出院",
                    "E 出院",
                    "你确认自己还能行动。医院没有给出答案，只把你送回现实。\n\n一周后，你回到工作岗位。",
                    "回到警局工作",
                    "白色病房被关在身后。你带着空白的记忆回到卷宗前。",
                    "work",
                    new Vector2(-5.2f, -2.3f),
                    string.Empty,
                    new Vector2(43.55f, -0.1f),
                    new Vector2(2.4f, 4.2f));
                ConfigureHospitalExitHotspot(exit);
            }
        }

        private void ConfigureHospitalExitHotspot(SceneHotspot hotspot)
        {
            hotspot.title = "出院";
            hotspot.prompt = "E 出院";
            hotspot.body = "你确认自己还能行动。医院没有给出答案，只把你送回现实。\n\n一周后，你回到工作岗位。";
            hotspot.choiceLabels = new[] { "回到警局工作" };
            hotspot.choiceResponses = new[] { "白色病房被关在身后。你带着空白的记忆回到卷宗前。" };
            hotspot.choiceTargetRooms = new[] { "work" };
            hotspot.choiceTargetLocalPositions = new[] { new Vector2(-5.2f, -2.3f) };
            hotspot.transform.position = new Vector3(43.55f, -0.1f, hotspot.transform.position.z);

            BoxCollider2D box = hotspot.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.enabled = true;
                box.isTrigger = true;
                box.size = new Vector2(2.4f, 4.2f);
                box.offset = Vector2.zero;
            }
            if (hotspot.outline != null) hotspot.outline.gameObject.SetActive(false);
        }

        private void ResizeHospitalHotspots(string hotspotTitle, Vector2 position, Vector2 size)
        {
            foreach (SceneHotspot hotspot in hotspots)
            {
                if (hotspot == null || hotspot.title != hotspotTitle) continue;
                hotspot.transform.position = position;
                BoxCollider2D box = hotspot.GetComponent<BoxCollider2D>();
                if (box != null) box.size = size;
                if (hotspot.outline != null) hotspot.outline.gameObject.SetActive(false);
            }
        }

        private Vector2 GetRoomOrigin(string roomId)
        {
            if (roomIds != null && roomOrigins != null)
            {
                for (int i = 0; i < roomIds.Length && i < roomOrigins.Length; i++)
                {
                    if (roomIds[i] == roomId) return roomOrigins[i];
                }
            }
            switch (roomId)
            {
                case "bedroom": return new Vector2(120f, 0f);
                case "corridor": return new Vector2(160f, 0f);
                case "mirror": return new Vector2(200f, 0f);
                case "school": return new Vector2(400f, 0f);
                case "family": return new Vector2(440f, 0f);
                case "mirror2": return new Vector2(480f, 0f);
                case "memory": return new Vector2(520f, 0f);
                case "ending": return new Vector2(560f, 0f);
                case "work": return new Vector2(600f, 0f);
                case "dream1": return new Vector2(640f, 0f);
                case "day2scene": return new Vector2(680f, 0f);
                case "interviews": return new Vector2(720f, 0f);
                case "clinic": return new Vector2(760f, 0f);
                case "evidenceRoom": return new Vector2(800f, 0f);
                case "vagueMemory": return new Vector2(840f, 0f);
                case "suspicion": return new Vector2(880f, 0f);
                case "renewed": return new Vector2(920f, 0f);
                case "finalDream": return new Vector2(960f, 0f);
                case "interviewThief": return new Vector2(1000f, 0f);
                case "interviewGirlfriend": return new Vector2(1040f, 0f);
                case "interviewNeighbor": return new Vector2(1080f, 0f);
            }
            return Vector2.zero;
        }

        private void AddEvidence(string item)
        {
            if (string.IsNullOrWhiteSpace(item) || evidence.Contains(item)) return;
            evidence.Add(item);
            SceneAudioController.Instance?.PlayEvidence();
        }

        private void OpenRoomIntro(string roomId)
        {
            if (roomIntrosShown.Contains(roomId)) return;
            RoomIntro intro = GetRoomIntro(roomId);
            if (string.IsNullOrWhiteSpace(intro.body)) return;

            roomIntrosShown.Add(roomId);
            active = new GameObject("Room Intro - " + roomId).AddComponent<SceneHotspot>();
            active.hideFlags = HideFlags.HideAndDontSave;
            active.title = intro.title;
            active.body = intro.body;
            pages = BuildPages(intro.body);
            page = 0;
            if (!string.IsNullOrWhiteSpace(intro.evidence)) AddEvidence(intro.evidence);
            Time.timeScale = 0f;
        }

        private static RoomIntro GetRoomIntro(string roomId)
        {
            switch (roomId)
            {
                case "unknown":
                    return default;
                case "hospital":
                    return new RoomIntro("病房", LoadNarrative("HospitalRecord") + "\n\n下一步：床头柜上的病历已经放在你右侧，查看它；随后和医生交谈，再从病房门出院，回到第一天工作流程。", "病历姓名被涂黑，身份信息被系统性回避。");
                case "police":
                    return new RoomIntro("警局档案室", "冷白的灯照在档案桌上。卷宗和证据链被分成两叠，像有人替你提前摆好了问题。\n\n下一步：先查看卷宗，再查看证据链。");
                case "bedroom":
                    return new RoomIntro("卧室", "夜色压低了房间，雨声贴着窗户滑落。每天调查结束后，你都会先回到这里。梦不会直接开始，它只从床上开始。\n\n下一步：走到床边入睡，进入梦境长廊。");
                case "corridor":
                    return new RoomIntro("梦境长廊", "你站在重复的走廊里。每一扇门都对应一段梦境，像把同一场案件拆成不同的夜晚。\n\n下一步：选择一扇门进入对应梦境。");
                case "mirror":
                    return new RoomIntro("灰白客厅", "玻璃后的房间安静得不自然。沙发、茶几、茶壶和那个人都像被灰尘固定在同一秒里。\n\n下一步：调查茶几附近的客厅，再和看不清脸的人交谈。");
                case "briefing":
                    return new RoomIntro("警局调查室", "整理后的卷宗被单独放在桌上。它不再只是材料，而像三个岔口。\n\n下一步：查看桌上的卷宗，并选择现实调查方向。");
                case "crime":
                    return new RoomIntro("现实客厅：空间痕迹", "现实客厅比梦境更克制。地面痕迹、家具位置和过分整齐的混乱彼此照应。\n\n下一步：调查地面痕迹，再查看桌面线索。");
                case "missing":
                    return new RoomIntro("现实客厅：失物与凶器", "这里缺少的不只是财物。桌边的空位、抽屉的方向和清单上的沉默，都在回避同一个问题。\n\n下一步：调查桌边压痕，再核对房间里的缺失物。");
                case "relation":
                    return new RoomIntro("现实客厅：受害者关系", "客厅不是心理咨询室，但它保存了那些越界的私人会面。关系被涂黑后，家具和文件反而开始替人作证。\n\n下一步：查看会面记录和关系线索。");
                case "school":
                    return new RoomIntro("第二夜：教室", LoadNarrative("SecondNightSchoolIntro"));
                case "family":
                    return new RoomIntro("第三夜：餐桌", LoadNarrative("FamilyDinnerDream"), "梦中的餐刀第一次以清晰形状出现。");
                case "memory":
                    return new RoomIntro("完整回忆", LoadNarrative("FullMemoryReturn"), "受害者曾掌握主角伪造旧案线索的证据。");
                case "ending":
                    return new RoomIntro("天亮前", LoadNarrative("FinalConfession"));
                case "work":
                    return new RoomIntro("第一天：回归工作", LoadNarrative("TwineWorkIntro"), "主角被重新安排到心理咨询师遇害案。");
                case "dream1":
                    return new RoomIntro("第一夜：被截断的争吵", LoadNarrative("TwineDayOneDream"));
                case "day2scene":
                    return new RoomIntro("第二天：案发现场", LoadNarrative("TwineDayTwoScene"), "现场的盗窃痕迹过于完整。");
                case "interviews":
                    return new RoomIntro("第二天：证人问询", LoadNarrative("TwineInterviews"));
                case "interviewThief":
                    return new RoomIntro("第二天：盗贼问询", "灯把桌面压成一个小小的审讯圈。盗贼坐在对面，话语总是绕开真正的案发时刻。");
                case "interviewGirlfriend":
                    return new RoomIntro("第二天：前女友问询", "前女友坐在灯下。她没有直接回答关系，只把线索推向咨询所和日程表。");
                case "interviewNeighbor":
                    return new RoomIntro("第二天：邻居问询", "邻居坐在灯下。她听见过争吵，却把关键部分留在沉默里。");
                case "clinic":
                    return new RoomIntro("第三天：咨询所", LoadNarrative("TwineClinic"), "受害者日程表中出现主角姓名缩写。");
                case "evidenceRoom":
                    return new RoomIntro("第三天夜里：证物室", LoadNarrative("TwineEvidenceRoom"), "旧案证物中的处方安眠药丢失。");
                case "vagueMemory":
                    return new RoomIntro("第三夜：朦胧回忆", LoadNarrative("TwineVagueMemory"), "梦中出现茶壶破裂，但关键动作仍被遮断。");
                case "suspicion":
                    return new RoomIntro("第四天到第五天：怀疑", LoadNarrative("TwineSuspicion"), "同事确认主角与受害者存在咨询关系。");
                case "renewed":
                    return new RoomIntro("第六天：重新调查", LoadNarrative("TwineRenewedInvestigation"), "镇静剂、邻居证词和酒吧监控共同指向主角。");
                case "finalDream":
                    return new RoomIntro("第六夜：最终梦境", LoadNarrative("TwineFinalDream"));
                default:
                    return default;
            }
        }

        private static string LoadNarrative(string name)
        {
            TextAsset asset = Resources.Load<TextAsset>("Narrative/" + name);
            return asset == null ? string.Empty : asset.text.Trim();
        }

        private static string[] BuildPages(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return new[] { string.Empty };
            List<string> result = new List<string>();
            StringBuilder builder = new StringBuilder();
            foreach (string raw in body.Replace("\r", string.Empty).Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0)
                {
                    Flush();
                    continue;
                }
                if (builder.Length + line.Length > 64) Flush();
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(line);
            }
            Flush();
            return result.Count == 0 ? new[] { body } : result.ToArray();
            void Flush()
            {
                if (builder.Length > 0)
                {
                    result.Add(builder.ToString());
                    builder.Clear();
                }
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (mainMenu)
            {
                DrawMainMenu();
                return;
            }
            if (pauseMenu)
            {
                DrawPauseMenu();
                return;
            }
            DrawEvidenceButton();
            if (evidenceOpen)
            {
                DrawEvidencePage();
                return;
            }
            DrawPrompt();
            if (active != null) DrawDialogue();
            HandleDialoguePanelClick();
        }

        private void HandleDialoguePanelClick()
        {
            if (active == null || ShowingChoices()) return;
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.MouseDown || evt.button != 0) return;
            if (!GetDialogueArea().Contains(evt.mousePosition)) return;

            TryAdvanceDialogue();
            evt.Use();
        }

        private void DrawMainMenu()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);
            float width = Mathf.Min(520f, Screen.width - 56f);
            Rect area = new Rect((Screen.width - width) * 0.5f, (Screen.height - 430f) * 0.5f, width, 430f);
            GUI.Label(new Rect(area.x, area.y + 6f, area.width, 82f), "标题", titleStyle);
            GUI.Label(new Rect(area.x, area.y + 92f, area.width, 34f), "title", smallStyle);
            if (GUI.Button(new Rect(area.x, area.y + 156f, area.width, 54f), "开始新游戏", buttonStyle))
            {
                SceneAudioController.Instance?.PlayButton();
                mainMenu = false;
                pauseMenu = false;
                active = null;
                evidence.Clear();
                roomIntrosShown.Clear();
                firstDayItemsSeen.Clear();
                day2SceneItemsSeen.Clear();
                witnessItemsSeen.Clear();
                clinicItemsSeen.Clear();
                evidenceRoomItemsSeen.Clear();
                suspicionItemsSeen.Clear();
                renewedItemsSeen.Clear();
                fullMemoryItemsSeen.Clear();
                unlockedDreamDoorTarget = "dream1";
                pendingClinicAfterGirlfriend = false;
                ClearPoliceTransfer();
                ClearDreamTransition();
                MoveTo("unknown", new Vector2(-1.15f, -2.25f));
                Time.timeScale = active == null ? 1f : 0f;
            }
            GUI.enabled = HasSave(0);
            if (GUI.Button(new Rect(area.x, area.y + 224f, area.width, 54f), "继续已有存档", buttonStyle)) { SceneAudioController.Instance?.PlayButton(); Load(0); }
            GUI.enabled = true;
            if (GUI.Button(new Rect(area.x, area.y + 292f, area.width, 54f), "退出", buttonStyle)) { SceneAudioController.Instance?.PlayButton(); Application.Quit(); }
            GUI.Label(new Rect(area.x, area.y + 372f, area.width, 34f), message, smallStyle);
        }

        private void DrawPauseMenu()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, panelStyle);
            Rect area = new Rect((Screen.width - 860f) * 0.5f, (Screen.height - 590f) * 0.5f, 860f, 590f);
            GUI.Box(area, GUIContent.none, panelStyle);
            GUI.Label(new Rect(area.x + 28f, area.y + 22f, 340f, 52f), "暂停", titleStyle);
            float x = area.x + 28f;
            float y = area.y + 96f;
            if (GUI.Button(new Rect(x, y, 220f, 52f), "返回游戏", buttonStyle)) { SceneAudioController.Instance?.PlayButton(); pauseMenu = false; Time.timeScale = active == null ? 1f : 0f; }
            if (GUI.Button(new Rect(x, y + 66f, 220f, 52f), "存档", buttonStyle)) { SceneAudioController.Instance?.PlayButton(); slotMode = 1; }
            if (GUI.Button(new Rect(x, y + 132f, 220f, 52f), "加载", buttonStyle)) { SceneAudioController.Instance?.PlayButton(); slotMode = 2; }
            if (GUI.Button(new Rect(x, y + 198f, 220f, 52f), "回到主菜单", buttonStyle)) { SceneAudioController.Instance?.PlayButton(); pauseMenu = false; mainMenu = true; Time.timeScale = 0f; }
            Rect slots = new Rect(area.x + 286f, area.y + 102f, 520f, 360f);
            if (slotMode == 0) GUI.Label(slots, "选择左侧的“存档”或“加载”。", bodyStyle);
            else
            {
                GUI.Label(new Rect(slots.x, slots.y - 50f, slots.width, 40f), slotMode == 1 ? "选择存档槽" : "选择读取槽", titleStyle);
                for (int i = 0; i < 5; i++)
                {
                    float rowY = slots.y + i * 66f;
                    GUI.Label(new Rect(slots.x, rowY, 360f, 40f), SlotLabel(i), bodyStyle);
                    if (slotMode == 1)
                    {
                        if (GUI.Button(new Rect(slots.x + 388f, rowY, 104f, 44f), "存档", buttonStyle)) { SceneAudioController.Instance?.PlayButton(); Save(i); message = "已保存到存档 " + (i + 1); }
                    }
                    else
                    {
                        GUI.enabled = HasSave(i);
                        if (GUI.Button(new Rect(slots.x + 388f, rowY, 104f, 44f), "读取", buttonStyle)) { SceneAudioController.Instance?.PlayButton(); Load(i); }
                        GUI.enabled = true;
                    }
                }
            }
        }

        private void DrawEvidenceButton()
        {
            Rect tab = new Rect(Screen.width - 206f, 22f, 172f, 46f);
            if (GUI.Button(tab, "证据", buttonStyle)) OpenEvidencePage();
        }

        private void OpenEvidencePage()
        {
            SceneAudioController.Instance?.PlayButton();
            evidenceOpen = true;
            selectedEvidence = evidence.Count > 0 ? Mathf.Clamp(selectedEvidence, 0, evidence.Count - 1) : -1;
            Time.timeScale = 0f;
        }

        private void CloseEvidencePage()
        {
            SceneAudioController.Instance?.PlayClose();
            evidenceOpen = false;
            Time.timeScale = pauseMenu || mainMenu || active != null ? 0f : 1f;
        }

        private void DrawEvidencePage()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);
            Rect pageArea = new Rect(28f, 24f, Screen.width - 56f, Screen.height - 48f);
            GUI.Box(pageArea, GUIContent.none, panelStyle);

            GUI.Label(new Rect(pageArea.x + 30f, pageArea.y + 18f, 360f, 52f), "证据陈列架", evidenceTitleStyle);
            if (GUI.Button(new Rect(pageArea.xMax - 134f, pageArea.y + 18f, 96f, 44f), "返回", buttonStyle)) CloseEvidencePage();

            float detailWidth = Mathf.Clamp(pageArea.width * 0.34f, 260f, 390f);
            Rect shelfArea = new Rect(pageArea.x + 26f, pageArea.y + 86f, pageArea.width - detailWidth - 44f, pageArea.height - 114f);
            Rect detailArea = new Rect(shelfArea.xMax + 18f, shelfArea.y, detailWidth, shelfArea.height);
            DrawEvidenceShelf(shelfArea);
            DrawEvidenceDetail(detailArea);
        }

        private void DrawEvidenceShelf(Rect area)
        {
            GUI.DrawTexture(area, shelfBackTex);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 10f), shelfEdgeTex);
            GUI.DrawTexture(new Rect(area.x, area.yMax - 10f, area.width, 10f), shelfEdgeTex);
            GUI.DrawTexture(new Rect(area.x, area.y, 10f, area.height), shelfEdgeTex);
            GUI.DrawTexture(new Rect(area.xMax - 10f, area.y, 10f, area.height), shelfEdgeTex);

            Rect view = new Rect(area.x + 18f, area.y + 18f, area.width - 36f, area.height - 36f);
            const int columns = 3;
            float columnWidth = view.width / columns;
            float rowHeight = 168f;
            int rows = Mathf.Max(1, Mathf.CeilToInt(evidence.Count / (float)columns));
            Rect content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(view.height, rows * rowHeight + 12f));

            evidenceScroll = GUI.BeginScrollView(view, evidenceScroll, content);
            if (evidence.Count == 0)
            {
                GUI.DrawTexture(new Rect(20f, 28f, content.width - 40f, 68f), evidenceCardTex);
                GUI.Label(new Rect(34f, 48f, content.width - 68f, 28f), "尚未记录证据。", evidenceBodyStyle);
            }

            for (int row = 0; row < rows; row++)
            {
                float shelfY = row * rowHeight + 116f;
                GUI.DrawTexture(new Rect(6f, shelfY, content.width - 12f, 16f), shelfWoodTex);
                GUI.DrawTexture(new Rect(6f, shelfY + 14f, content.width - 12f, 6f), shelfEdgeTex);
            }

            for (int i = 0; i < evidence.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                Rect slot = new Rect(column * columnWidth + 14f, row * rowHeight + 14f, columnWidth - 28f, 130f);
                bool hover = slot.Contains(Event.current.mousePosition);
                bool selected = selectedEvidence == i;
                DrawEvidenceIcon(slot, evidence[i], hover, selected);
                if (GUI.Button(slot, GUIContent.none, GUIStyle.none))
                {
                    SceneAudioController.Instance?.PlayButton();
                    selectedEvidence = i;
                }
            }
            GUI.EndScrollView();
        }

        private void DrawEvidenceDetail(Rect area)
        {
            GUI.Box(area, GUIContent.none, panelStyle);
            GUI.Label(new Rect(area.x + 20f, area.y + 18f, area.width - 40f, 44f), selectedEvidence >= 0 && selectedEvidence < evidence.Count ? EvidenceShortTitle(evidence[selectedEvidence]) : "未选择", evidenceTitleStyle);
            Rect note = new Rect(area.x + 20f, area.y + 80f, area.width - 40f, area.height - 102f);
            GUI.DrawTexture(note, paperTex);
            GUI.DrawTexture(new Rect(note.x + 14f, note.y + 24f, note.width - 28f, 3f), paperLineTex);
            GUI.DrawTexture(new Rect(note.x + 14f, note.y + 54f, note.width - 28f, 2f), paperLineTex);
            string detail = selectedEvidence >= 0 && selectedEvidence < evidence.Count ? evidence[selectedEvidence] : " ";
            GUI.Label(new Rect(note.x + 18f, note.y + 76f, note.width - 36f, note.height - 94f), detail, evidenceBodyStyle);
        }

        private void DrawEvidenceIcon(Rect rect, string item, bool hover, bool selected)
        {
            GUI.DrawTexture(rect, selected ? evidenceSelectedTex : hover ? evidenceHoverTex : evidenceCardTex);
            GUI.DrawTexture(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, 3f), paperLineTex);
            GUI.DrawTexture(new Rect(rect.x + 6f, rect.yMax - 28f, rect.width - 12f, 2f), paperLineTex);

            Rect icon = new Rect(rect.x + rect.width * 0.5f - 34f, rect.y + 22f, 68f, 62f);
            if (item.Contains("茶壶"))
            {
                GUI.DrawTexture(new Rect(icon.x + 10f, icon.y + 18f, 34f, 24f), accentTex);
                GUI.DrawTexture(new Rect(icon.x + 17f, icon.y + 8f, 20f, 14f), accentTex);
                GUI.DrawTexture(new Rect(icon.x + 2f, icon.y + 25f, 12f, 8f), accentTex);
                GUI.DrawTexture(new Rect(icon.x + 42f, icon.y + 24f, 10f, 12f), accentTex);
                GUI.DrawTexture(new Rect(icon.x + 22f, icon.y + 4f, 8f, 6f), inkTex);
            }
            else if (item.Contains("拆信刀") || item.Contains("凶器") || item.Contains("缺失"))
            {
                GUI.DrawTexture(new Rect(icon.x + 12f, icon.y + 23f, 36f, 8f), metalTex);
                GUI.DrawTexture(new Rect(icon.x + 42f, icon.y + 20f, 10f, 14f), paperTex);
                GUI.DrawTexture(new Rect(icon.x + 6f, icon.y + 20f, 10f, 14f), inkTex);
            }
            else if (item.Contains("病历") || item.Contains("姓名"))
            {
                GUI.DrawTexture(new Rect(icon.x + 10f, icon.y + 4f, 36f, 44f), paperTex);
                GUI.DrawTexture(new Rect(icon.x + 16f, icon.y + 14f, 24f, 4f), paperLineTex);
                GUI.DrawTexture(new Rect(icon.x + 16f, icon.y + 24f, 24f, 6f), inkTex);
                GUI.DrawTexture(new Rect(icon.x + 16f, icon.y + 36f, 18f, 3f), paperLineTex);
            }
            else if (item.Contains("关系") || item.Contains("咨询"))
            {
                GUI.DrawTexture(new Rect(icon.x + 8f, icon.y + 8f, 18f, 24f), photoTex);
                GUI.DrawTexture(new Rect(icon.x + 30f, icon.y + 8f, 18f, 24f), photoTex);
                GUI.DrawTexture(new Rect(icon.x + 18f, icon.y + 32f, 22f, 4f), accentTex);
                GUI.DrawTexture(new Rect(icon.x + 26f, icon.y + 16f, 4f, 18f), accentTex);
            }
            else if (item.Contains("梦") || item.Contains("灰白"))
            {
                GUI.DrawTexture(new Rect(icon.x + 8f, icon.y + 4f, 40f, 44f), photoTex);
                GUI.DrawTexture(new Rect(icon.x + 16f, icon.y + 12f, 24f, 28f), paperLineTex);
                GUI.DrawTexture(new Rect(icon.x + 24f, icon.y + 18f, 8f, 14f), inkTex);
            }
            else
            {
                GUI.DrawTexture(new Rect(icon.x + 10f, icon.y + 4f, 36f, 44f), paperTex);
                GUI.DrawTexture(new Rect(icon.x + 16f, icon.y + 14f, 24f, 4f), paperLineTex);
                GUI.DrawTexture(new Rect(icon.x + 16f, icon.y + 25f, 24f, 4f), paperLineTex);
                GUI.DrawTexture(new Rect(icon.x + 16f, icon.y + 36f, 18f, 4f), paperLineTex);
            }

            GUI.Label(new Rect(rect.x + 6f, rect.yMax - 32f, rect.width - 12f, 26f), EvidenceShortTitle(item), evidenceCaptionStyle);
        }

        private static string EvidenceShortTitle(string item)
        {
            if (item.Contains("茶壶")) return "茶壶";
            if (item.Contains("病历") || item.Contains("姓名")) return "病历";
            if (item.Contains("证据链")) return "证据链";
            if (item.Contains("拆信刀") || item.Contains("凶器")) return "缺失物";
            if (item.Contains("混乱") || item.Contains("现场")) return "现场痕迹";
            if (item.Contains("关系") || item.Contains("咨询")) return "关系记录";
            if (item.Contains("梦") || item.Contains("灰白")) return "梦境片段";
            return item.Length <= 6 ? item : item.Substring(0, 6);
        }

        private void DrawPrompt()
        {
            if (nearest == null || active != null) return;
            Rect rect = new Rect(24f, Screen.height - 76f, 460f, 52f);
            GUI.Box(rect, GUIContent.none, panelStyle);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 11f, rect.width - 32f, 30f), nearest.prompt + " - " + nearest.title, bodyStyle);
        }

        private void DrawDialogue()
        {
            Rect area = GetDialogueArea();
            float height = area.height;
            GUI.Box(area, GUIContent.none, panelStyle);
            GUI.Label(new Rect(area.x + 34f, area.y + 26f, area.width - 68f, 52f), active.title, titleStyle);
            GUI.Label(new Rect(area.x + 36f, area.y + 94f, area.width - 72f, height - 198f), pages.Length == 0 ? string.Empty : pages[Mathf.Clamp(page, 0, pages.Length - 1)], bodyStyle);
            GUI.Label(new Rect(area.x + 36f, area.y + height - 52f, area.width - 72f, 32f), (page + 1) + "/" + Mathf.Max(1, pages.Length) + "  点击鼠标进入下一句", smallStyle);
            if (ShowingChoices())
            {
                for (int i = 0; i < active.choiceLabels.Length; i++)
                {
                    float buttonWidth = (area.width - 88f) / active.choiceLabels.Length;
                    Rect button = new Rect(area.x + 36f + i * (buttonWidth + 8f), area.y + height - 132f, buttonWidth, 64f);
                    if (GUI.Button(button, active.choiceLabels[i], buttonStyle)) ApplyChoice(i);
                }
            }
        }

        private Rect GetDialogueArea()
        {
            float width = Mathf.Min(840f, Screen.width - 56f);
            float height = ShowingChoices() ? 496f : 410f;
            return new Rect(Screen.width - width - 32f, Screen.height - height - 32f, width, height);
        }

        private void Save(int slot)
        {
            string prefix = "MC2DScene_" + slot + "_";
            PlayerPrefs.SetInt(prefix + "has", 1);
            PlayerPrefs.SetString(prefix + "room", currentRoom);
            Vector2 origin = GetRoomOrigin(currentRoom);
            PlayerPrefs.SetFloat(prefix + "x", player.position.x - origin.x);
            PlayerPrefs.SetFloat(prefix + "y", player.position.y - origin.y);
            PlayerPrefs.SetString(prefix + "evidence", string.Join("|", evidence));
            PlayerPrefs.SetString(prefix + "roomIntros", string.Join("|", roomIntrosShown));
            PlayerPrefs.SetString(prefix + "firstDayItemsSeen", string.Join("|", firstDayItemsSeen));
            PlayerPrefs.SetString(prefix + "day2SceneItemsSeen", string.Join("|", day2SceneItemsSeen));
            PlayerPrefs.SetString(prefix + "witnessItemsSeen", string.Join("|", witnessItemsSeen));
            PlayerPrefs.SetString(prefix + "clinicItemsSeen", string.Join("|", clinicItemsSeen));
            PlayerPrefs.SetString(prefix + "evidenceRoomItemsSeen", string.Join("|", evidenceRoomItemsSeen));
            PlayerPrefs.SetString(prefix + "suspicionItemsSeen", string.Join("|", suspicionItemsSeen));
            PlayerPrefs.SetString(prefix + "renewedItemsSeen", string.Join("|", renewedItemsSeen));
            PlayerPrefs.SetString(prefix + "fullMemoryItemsSeen", string.Join("|", fullMemoryItemsSeen));
            PlayerPrefs.SetString(prefix + "dreamDoor", unlockedDreamDoorTarget);
            PlayerPrefs.SetInt(prefix + "pendingClinicAfterGirlfriend", pendingClinicAfterGirlfriend ? 1 : 0);
            PlayerPrefs.SetString(prefix + "pendingPoliceTransferRoom", pendingPoliceTransferRoom);
            PlayerPrefs.SetFloat(prefix + "pendingPoliceTransferX", pendingPoliceTransferLocal.x);
            PlayerPrefs.SetFloat(prefix + "pendingPoliceTransferY", pendingPoliceTransferLocal.y);
            PlayerPrefs.SetString(prefix + "pendingPoliceTransferLabel", pendingPoliceTransferLabel);
            PlayerPrefs.SetString(prefix + "pendingDreamTransitionRoom", pendingDreamTransitionRoom);
            PlayerPrefs.SetFloat(prefix + "pendingDreamTransitionX", pendingDreamTransitionLocal.x);
            PlayerPrefs.SetFloat(prefix + "pendingDreamTransitionY", pendingDreamTransitionLocal.y);
            PlayerPrefs.SetString(prefix + "pendingDreamTransitionLabel", pendingDreamTransitionLabel);
            PlayerPrefs.SetString(prefix + "time", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            PlayerPrefs.Save();
        }

        private void Load(int slot)
        {
            if (!HasSave(slot)) { message = "没有可读取的存档"; return; }
            string prefix = "MC2DScene_" + slot + "_";
            evidence.Clear();
            foreach (string item in PlayerPrefs.GetString(prefix + "evidence", string.Empty).Split('|')) if (!string.IsNullOrWhiteSpace(item)) evidence.Add(item);
            roomIntrosShown.Clear();
            foreach (string roomId in PlayerPrefs.GetString(prefix + "roomIntros", string.Empty).Split('|')) if (!string.IsNullOrWhiteSpace(roomId)) roomIntrosShown.Add(roomId);
            firstDayItemsSeen.Clear();
            foreach (string item in PlayerPrefs.GetString(prefix + "firstDayItemsSeen", string.Empty).Split('|')) if (!string.IsNullOrWhiteSpace(item)) firstDayItemsSeen.Add(item);
            LoadSet(prefix + "day2SceneItemsSeen", day2SceneItemsSeen);
            LoadSet(prefix + "witnessItemsSeen", witnessItemsSeen);
            LoadSet(prefix + "clinicItemsSeen", clinicItemsSeen);
            LoadSet(prefix + "evidenceRoomItemsSeen", evidenceRoomItemsSeen);
            LoadSet(prefix + "suspicionItemsSeen", suspicionItemsSeen);
            LoadSet(prefix + "renewedItemsSeen", renewedItemsSeen);
            LoadSet(prefix + "fullMemoryItemsSeen", fullMemoryItemsSeen);
            unlockedDreamDoorTarget = PlayerPrefs.GetString(prefix + "dreamDoor", "dream1");
            pendingClinicAfterGirlfriend = PlayerPrefs.GetInt(prefix + "pendingClinicAfterGirlfriend", 0) == 1;
            pendingPoliceTransferRoom = CanonicalTwineTarget(PlayerPrefs.GetString(prefix + "pendingPoliceTransferRoom", string.Empty));
            pendingPoliceTransferLocal = new Vector2(PlayerPrefs.GetFloat(prefix + "pendingPoliceTransferX", 0f), PlayerPrefs.GetFloat(prefix + "pendingPoliceTransferY", 0f));
            pendingPoliceTransferLabel = PlayerPrefs.GetString(prefix + "pendingPoliceTransferLabel", string.Empty);
            pendingDreamTransitionRoom = CanonicalTwineTarget(PlayerPrefs.GetString(prefix + "pendingDreamTransitionRoom", string.Empty));
            pendingDreamTransitionLocal = new Vector2(PlayerPrefs.GetFloat(prefix + "pendingDreamTransitionX", 0f), PlayerPrefs.GetFloat(prefix + "pendingDreamTransitionY", 0f));
            pendingDreamTransitionLabel = PlayerPrefs.GetString(prefix + "pendingDreamTransitionLabel", string.Empty);
            mainMenu = false;
            pauseMenu = false;
            active = null;
            MoveTo(PlayerPrefs.GetString(prefix + "room", "unknown"), new Vector2(PlayerPrefs.GetFloat(prefix + "x", -5.2f), PlayerPrefs.GetFloat(prefix + "y", -2.3f)));
            Time.timeScale = active == null ? 1f : 0f;
        }

        private static bool HasSave(int slot) => PlayerPrefs.GetInt("MC2DScene_" + slot + "_has", 0) == 1;
        private static string SlotLabel(int slot) => HasSave(slot) ? "存档 " + (slot + 1) + "：" + PlayerPrefs.GetString("MC2DScene_" + slot + "_time", string.Empty) : "存档 " + (slot + 1) + "：空";

        private static void LoadSet(string key, HashSet<string> target)
        {
            target.Clear();
            foreach (string item in PlayerPrefs.GetString(key, string.Empty).Split('|'))
            {
                if (!string.IsNullOrWhiteSpace(item)) target.Add(item);
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            blackTex = Texture(Color.black);
            panelTex = Texture(new Color32(10, 12, 14, 224));
            buttonTex = Texture(new Color32(34, 38, 42, 245));
            shelfWoodTex = Texture(new Color32(92, 62, 39, 255));
            shelfEdgeTex = Texture(new Color32(42, 27, 20, 255));
            shelfBackTex = Texture(new Color32(58, 43, 34, 255));
            evidenceCardTex = Texture(new Color32(188, 171, 135, 255));
            evidenceHoverTex = Texture(new Color32(228, 208, 143, 255));
            evidenceSelectedTex = Texture(new Color32(174, 128, 66, 255));
            paperTex = Texture(new Color32(226, 216, 188, 255));
            paperLineTex = Texture(new Color32(128, 111, 84, 255));
            inkTex = Texture(new Color32(24, 25, 25, 255));
            metalTex = Texture(new Color32(178, 184, 181, 255));
            accentTex = Texture(new Color32(139, 53, 43, 255));
            photoTex = Texture(new Color32(149, 153, 145, 255));
            titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 47, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color32(240, 240, 232, 255) }, wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 30, alignment = TextAnchor.UpperLeft, normal = { textColor = new Color32(226, 226, 218, 255) }, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 24, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color32(150, 158, 162, 255) }, wordWrap = true };
            panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTex } };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 30, alignment = TextAnchor.MiddleCenter, normal = { background = buttonTex, textColor = new Color32(232, 234, 232, 255) }, hover = { background = buttonTex, textColor = Color.white }, wordWrap = true };
            evidenceTitleStyle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleLeft, fontSize = 44 };
            evidenceBodyStyle = new GUIStyle(bodyStyle) { normal = { textColor = new Color32(44, 38, 30, 255) }, fontSize = 29 };
            evidenceCaptionStyle = new GUIStyle(smallStyle) { normal = { textColor = new Color32(34, 30, 25, 255) }, fontSize = 24 };
        }

        private static Texture2D Texture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.filterMode = FilterMode.Point;
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private readonly struct RoomIntro
        {
            public readonly string title;
            public readonly string body;
            public readonly string evidence;

            public RoomIntro(string title, string body, string evidence = "")
            {
                this.title = title;
                this.body = body;
                this.evidence = evidence;
            }
        }
    }
}
