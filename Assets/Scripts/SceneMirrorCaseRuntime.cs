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
                if (fallbackKey == KeyCode.Space) return keyboard.spaceKey.wasPressedThisFrame;
            }
#endif
            return Input.GetKeyDown(fallbackKey);
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
            if (nearest == null || active != null || mainMenu || pauseMenu || evidenceOpen) return;
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
            SceneAudioController.Instance?.PlayInteract();
            active = hotspot;
            pages = BuildPages(hotspot.body);
            page = 0;
            if (!string.IsNullOrWhiteSpace(hotspot.evidence)) AddEvidence(hotspot.evidence);
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
            string ev = GetArray(active.choiceEvidence, index);
            if (!string.IsNullOrWhiteSpace(ev)) AddEvidence(ev);
            string target = GetArray(active.choiceTargetRooms, index);
            if (!string.IsNullOrWhiteSpace(target))
            {
                target = CanonicalTwineTarget(target);
                Vector2 local = active.choiceTargetLocalPositions != null && index < active.choiceTargetLocalPositions.Length ? active.choiceTargetLocalPositions[index] : Vector2.zero;
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

        private static string GetArray(string[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : string.Empty;
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
                        hotspot.choiceTargetRooms = new[] { "work" };
                        hotspot.choiceTargetLocalPositions = new[] { new Vector2(-5.2f, -2.3f) };
                        break;
                    case "卷宗":
                    case "整理后的卷宗":
                        hotspot.title = "第一天卷宗";
                        hotspot.prompt = "E 研读卷宗";
                        hotspot.choiceLabels = new[] { "重新按第一天调查" };
                        hotspot.choiceResponses = new[] { "你把过早串联的结论拆开，重新从第一天卷宗开始。" };
                        hotspot.choiceTargetRooms = new[] { "work" };
                        hotspot.choiceTargetLocalPositions = new[] { new Vector2(-5.2f, -2.3f) };
                        break;
                    case "床":
                        hotspot.choiceTargetRooms = new[] { "dream1" };
                        hotspot.choiceTargetLocalPositions = new[] { new Vector2(-4.9f, -2.2f) };
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
                }
            }
        }

        private static string CanonicalTwineTarget(string roomId)
        {
            switch (roomId)
            {
                case "police":
                case "briefing":
                    return "work";
                case "bedroom":
                case "corridor":
                case "mirror":
                    return "dream1";
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
            ResizeHospitalHotspot("病历", new Vector2(36.55f, 0.7f), new Vector2(1.15f, 0.95f));
            ResizeHospitalHotspot("医生", new Vector2(42.1f, -0.25f), new Vector2(1.35f, 3.2f));
            ResizeHospitalHotspot("出院", new Vector2(43.55f, -0.1f), new Vector2(1.85f, 3.85f));
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
                "logical hospital exit door",
                "Pixel Hospital Police Door",
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
                if (hiddenNames.Contains(renderer.gameObject.name) || oldHospitalLayer) renderer.enabled = false;
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

        private void ResizeHospitalHotspot(string hotspotTitle, Vector2 position, Vector2 size)
        {
            foreach (SceneHotspot hotspot in hotspots)
            {
                if (hotspot == null || hotspot.title != hotspotTitle) continue;
                hotspot.transform.position = position;
                BoxCollider2D box = hotspot.GetComponent<BoxCollider2D>();
                if (box != null) box.size = size;
                if (hotspot.outline != null) hotspot.outline.gameObject.SetActive(false);
                return;
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
                    return new RoomIntro("卧室", "夜色压低了房间，雨声贴着窗户滑落。床、床头柜和桌面都保持着过分安静的秩序。\n\n下一步：查看床头柜上的病历，然后回到床边入睡。");
                case "corridor":
                    return new RoomIntro("第一夜走廊", "你站在重复的走廊里。门牌号像坏掉的记忆，一遍遍把同一个数字推回眼前。\n\n下一步：先倾听走廊，再走向尽头那扇门。");
                case "mirror":
                    return new RoomIntro("灰白客厅", "玻璃后的房间安静得不自然。沙发、茶几、茶壶和那个人都像被灰尘固定在同一秒里。\n\n下一步：调查茶几附近的客厅，再和看不清脸的人交谈。");
                case "briefing":
                    return new RoomIntro("警局调查室", "整理后的卷宗被单独放在桌上。它不再只是材料，而像三个岔口。\n\n下一步：查看桌上的卷宗，并选择现实调查方向。");
                case "crime":
                    return new RoomIntro("现实客厅：空间痕迹", "现实客厅比梦境更克制。地面痕迹、家具位置和过分整齐的混乱彼此照应。\n\n下一步：调查地面痕迹，再查看桌面线索。");
                case "missing":
                    return new RoomIntro("现实客厅：失物与凶器", "这里缺少的不只是财物。桌边的空位、抽屉的方向和清单上的沉默，都在回避同一个问题。\n\n下一步：调查桌边压痕，再核对房间里的缺失物。");
                case "relation":
                    return new RoomIntro("现实客厅：受害者关系", "客厅像咨询室，又像案发现场。关系被涂黑后，家具和文件反而开始替人作证。\n\n下一步：查看会面记录和关系线索。");
                case "school":
                    return new RoomIntro("第二夜：教室", LoadNarrative("SecondNightSchoolIntro"));
                case "family":
                    return new RoomIntro("第三夜：餐桌", LoadNarrative("FamilyDinnerDream"), "梦中的餐刀第一次以清晰形状出现。");
                case "mirror2":
                    return new RoomIntro("镜中房间", LoadNarrative("MirrorProjectionTalk"), "镜像指出：你与案件的关系不是旁观者。");
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
            mainMenu = false;
            pauseMenu = false;
            active = null;
            MoveTo(PlayerPrefs.GetString(prefix + "room", "unknown"), new Vector2(PlayerPrefs.GetFloat(prefix + "x", -5.2f), PlayerPrefs.GetFloat(prefix + "y", -2.3f)));
            Time.timeScale = active == null ? 1f : 0f;
        }

        private static bool HasSave(int slot) => PlayerPrefs.GetInt("MC2DScene_" + slot + "_has", 0) == 1;
        private static string SlotLabel(int slot) => HasSave(slot) ? "存档 " + (slot + 1) + "：" + PlayerPrefs.GetString("MC2DScene_" + slot + "_time", string.Empty) : "存档 " + (slot + 1) + "：空";

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
