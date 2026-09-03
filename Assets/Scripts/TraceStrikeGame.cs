using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NHN.TraceStrike
{
    public sealed class TraceStrikeGame : MonoBehaviour
    {
        private const float ReferenceWidth = 1080f;
        private const float ReferenceHeight = 1920f;
        private const float DesktopReferenceWidth = 1920f;
        private const float DesktopReferenceHeight = 1080f;
        private const int LegacyFieldSize = 11;
        private const float LegacyDesktopGridSize = 820f;
        private const float LegacyMobileGridSize = 900f;
        private const float SwipeThreshold = 55f;
        private const int StandaloneWidth = 1600;
        private const int StandaloneHeight = 900;
        private const float PortraitAspect = 9f / 16f;
        private const float LandscapeAspect = 16f / 9f;
        private const float TutorialCellSize = 180f;
        private const float TutorialPlayerSizeRatio = 0.68f;
        private const float BattlePlayerSizeRatio = 0.78f;
        private const float TargetedWarningSeconds = 0.65f;
        private const float StandardTileOpacity = 0.68f;
        private const bool UseIsometricArena = false;
        private const bool UseExtraTileDepth = false;
        private const float BattleCameraZoom = 2.05f;
        private const float BattleCameraFollowSpeed = 10f;
        private const float BattlePlayerMoveSeconds = 0.11f;
        private const float DesktopMinimapSize = 280f;
        private const float MinimapCellSize = 14f;
        private const float TitleInitialRadius = 0.115f;
        private const float TitleRevealSeconds = 1.35f;
        private const string BestClearTimeKey = "TraceStrike.BestClearTime";

        private static readonly Color Background = Hex("101525");
        private static readonly Color ArenaVoid = Hex("020306");
        private static readonly Color ArenaTile = Hex("090B10");
        private static readonly Color ArenaTileLift = Hex("17131A");
        private static readonly Color ArenaTileTextureTint = Hex("FFF3DE");
        private static readonly Color CenterDamageTileTint = Hex("FFC078");
        private static readonly Color ArenaBorderGlow = Hex("FF7138");
        private static readonly Color Panel = Hex("182038");
        private static readonly Color PanelLight = Hex("222D4B");
        private static readonly Color Floor = Hex("263653");
        private static readonly Color FloorEdge = Hex("344867");
        private static readonly Color Trail = Hex("26D9FF");
        private static readonly Color TrailHot = Hex("FFF06A");
        private static readonly Color StartColor = Hex("3EF0A8");
        private static readonly Color EndColor = Hex("FFB743");
        private static readonly Color Danger = Hex("FF4B69");
        private static readonly Color White = Hex("F4F8FF");
        private static readonly Color Muted = Hex("9EACC7");
        private static readonly string[] HubCharacterNames =
        {
            "아이리스 · 트레이스 워리어",
            "세라프 · 펄스 러너",
            "녹스 · 바이러스 헌터",
            "브론 · 크리스털 브레이커"
        };
        private static readonly string[] HubCharacterDescriptions =
        {
            "균형형 추적 전투원 · 기본 경로 공격",
            "청록 기동형 전투원 · 빠른 경로 제어",
            "보라 분석형 전투원 · 바이러스 표식 강화",
            "황금 돌파형 전투원 · 수정 파괴 특화"
        };
        private static readonly Color[] HubCharacterTints =
        {
            White,
            Hex("70F3E4"),
            Hex("CF86FF"),
            Hex("FFD36A")
        };

        private readonly TrailFieldModel model = new TrailFieldModel();
        private readonly HubWorldModel hubModel = new HubWorldModel();
        private readonly Image[,] arenaGroundTiles = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly RectTransform[,] mainTileDepthRoots = new RectTransform[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Image[,] mainTileDepthImages = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly RectTransform[,] mainTileDropShadows = new RectTransform[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Image[,] mainTiles = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Outline[,] mainTileOutlines = new Outline[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Text[,] tileLabels = new Text[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Image[,] minimapTiles = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly RectTransform[,] attackWarningVisuals = new RectTransform[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Image[,] attackWarningFillImages = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Image[,] endpointMarkerImages = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly RectTransform[,] specialItemVisuals = new RectTransform[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Image[,] specialItemImages = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Image[,] specialItemIconImages = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Text[,] specialItemLabels = new Text[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly RectTransform[,] hubObjectVisuals = new RectTransform[HubWorldModel.Size, HubWorldModel.Size];
        private readonly Image[,] hubObjectImages = new Image[HubWorldModel.Size, HubWorldModel.Size];
        private readonly Image[,] hubObjectIconImages = new Image[HubWorldModel.Size, HubWorldModel.Size];
        private readonly Text[,] hubObjectLabels = new Text[HubWorldModel.Size, HubWorldModel.Size];
        private readonly HashSet<Vector2Int> warnedCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> targetedCells = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, SpecialTileType> specialTiles = new Dictionary<Vector2Int, SpecialTileType>();
        private readonly List<Vector2Int> crystalCells = new List<Vector2Int>();
        private readonly Dictionary<Vector2Int, int> crystalWarningCounts = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, int> crystalFiringCounts = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, Dictionary<int, float>> crystalTelegraphProgress =
            new Dictionary<Vector2Int, Dictionary<int, float>>();
        private readonly RectTransform[] crystalVisuals = new RectTransform[CrystalRules.CrystalCount];
        private readonly float[] crystalAttackTimers = new float[CrystalRules.CrystalCount];
        private readonly HashSet<Vector2Int> tutorialTrail = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> tutorialTrailOrder = new List<Vector2Int>();
        private readonly List<RectTransform> ambientParticles = new List<RectTransform>();
        private readonly List<Image> ambientParticleImages = new List<Image>();
        private readonly int[,] floorTileVariantIndices =
            new int[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly System.Random floorTileRandom = new System.Random();

        private Font gameFont;
        private RectTransform mainGrid;
        private RectTransform objectiveArrow;
        private float objectiveArrowAngle;
        private bool objectiveArrowInitialized;
        private RectTransform minimapGrid;
        private RectTransform minimapRoot;
        private RectTransform minimapPlayer;
        private RectTransform mainPlayer;
        private RectTransform effectsLayer;
        private RectTransform directionPadArea;
        private RectTransform attackSlash;
        private RectTransform arenaBossCore;
        private RectTransform arenaBossHealthRoot;
        private RectTransform phaseOverlayRoot;
        private RectTransform phasePageLeft;
        private RectTransform phasePageRight;
        private RectTransform hubCampZone;
        private RectTransform hubStageLane;
        private RectTransform bossHud;
        private RectTransform interactionPanelRoot;
        private RectTransform titleScreen;
        private Image titleBlindImage;
        private Material titleBlindMaterial;
        private CanvasGroup titleForegroundGroup;
        private Text titleInputHint;
        private Image interactionRingImage;
        private Image interactionIconImage;
        private Text interactionTitleText;
        private Text interactionBodyText;
        private Sprite bossPortraitSprite;
        private Image mainPlayerImage;
        private Image bossHealthFill;
        private Image fieldFrame;
        private Text bossNameText;
        private Text bossHealthText;
        private Text stageText;
        private Text playerHealthText;
        private Text powerText;
        private Text statusText;
        private Text comboText;
        private Text shapeText;
        private Text damagePopup;
        private Text phaseBanner;
        private Text fieldTitleText;
        private Text bestRecordText;
        private Text bestRatingText;
        private CanvasGroup damagePopupGroup;
        private CanvasGroup attackSlashGroup;
        private CanvasGroup phaseBannerGroup;
        private AudioSource audioSource;
        private AudioSource musicSource;
        private AudioClip templeMusic;
        private Camera uiCamera;
        private AudioClip moveSfx;
        private AudioClip startSfx;
        private AudioClip blockedSfx;
        private AudioClip resetSfx;
        private AudioClip attackSfx;
        private AudioClip hitSfx;
        private AudioClip victorySfx;
        private AudioClip warningSfx;
        private AudioClip laserSfx;
        private AudioClip explosionSfx;
        private AudioClip deathSfx;
        private AudioClip phaseTwoSfx;
        private AudioClip targetLockSfx;
        private Sprite startMarkerSprite;
        private Sprite endMarkerSprite;
        private Sprite powerIconSprite;
        private Sprite amplifyIconSprite;
        private Sprite mudIconSprite;
        private Sprite curseIconSprite;
        private Sprite campfireSprite;
        private Sprite playerCharacterSprite;
        private Sprite floorTileSprite;
        private Sprite[] floorTileSprites;
        private Sprite golemBaseTileSprite;
        private Sprite golemEdgeTileSprite;

        private Vector2 pressPosition;
        private bool pointerDown;
        private bool keyboardDirectionLatched;
        private bool inputLocked;
        private bool playerDead;
        private bool gameCleared;
        private bool movementFrozen;
        private bool hazardFiring;
        private bool targetedFiring;
        private bool phaseTwoActive;
        private bool crystalsRelocated;
        private bool tutorialActive;
        private bool tutorialTransitioning;
        private bool titleActive;
        private bool titleRevealing;
        private bool hubActive;
        private bool desktopLayout;
        private bool stageTimerRunning;
        private bool bossPhaseSkipped;
        private int bossAttackCount;
        private int glyphPatternIndex;
        private int diamondUseCount;
        private int patternVersion;
        private int crystalLayoutVersion;
        private int tutorialStep;
        private int tutorialVersion;
        private int stage;
        private int currentFieldSize = TrailFieldModel.MaxSize;
        private int round;
        private int bossHealth;
        private int bossMaxHealth;
        private int nextAttackFlatBonus;
        private int movementFreezeVersion;
        private int crystalTelegraphSequence;
        private float nextAttackMultiplier = 1f;
        private float stageStartRealtime;
        private float hazardTelegraphProgress;
        private float targetedTelegraphProgress;
        private Color activeCharacterTint = White;
        private Vector2Int tutorialPlayer;
        private Vector2Int playerFacing = Vector2Int.up;
        private Vector2 battleCameraTarget;
        private Vector2 battlePlayerVisualPosition;
        private Vector2 battlePlayerMoveFrom;
        private Vector2 battlePlayerMoveTarget;
        private Vector2 battlePlayerNudge;
        private float battlePlayerMoveTime;
        private Vector2 battleCameraPosition;
        private Vector2 battleCameraShake;
        private int fieldShakeVersion;
        private float mainCellSize;
        private bool battleCameraInitialized;
        private float ActiveTutorialCellSize => desktopLayout ? 132f : TutorialCellSize;
        private int appliedScreenWidth = -1;
        private int appliedScreenHeight = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<TraceStrikeGame>() != null)
            {
                return;
            }

            var host = new GameObject("Trace Strike - Game Root");
            host.AddComponent<TraceStrikeGame>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            desktopLayout = !Application.isMobilePlatform;
            if (!desktopLayout)
            {
                Screen.orientation = ScreenOrientation.Portrait;
            }
#if UNITY_STANDALONE && !UNITY_EDITOR
            Screen.SetResolution(StandaloneWidth, StandaloneHeight, FullScreenMode.Windowed, 60);
#endif
            gameFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 48);

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.72f;
            audioSource.spatialBlend = 0f;
            audioSource.priority = 32;
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.volume = 0.2f;
            musicSource.spatialBlend = 0f;
            musicSource.priority = 96;
            BuildSoundBank();
            musicSource.clip = templeMusic;
            musicSource.Play();

            BuildInterface();
            RefreshFixedAspect();
            string[] launchArguments = System.Environment.GetCommandLineArgs();
            bool captureMode = System.Array.IndexOf(launchArguments, "-capturePath") >= 0;
            bool captureTutorial = System.Array.IndexOf(launchArguments, "-captureTutorial") >= 0;
            bool captureTitle = System.Array.IndexOf(launchArguments, "-captureTitle") >= 0;
            bool captureHub = System.Array.IndexOf(launchArguments, "-captureHub") >= 0;
            if (captureMode && captureTitle)
            {
                PrepareTitleScreen();
            }
            else if (captureMode && captureHub)
            {
                StartHub();
            }
            else if (captureMode && captureTutorial)
            {
                StartTutorial();
            }
            else if (captureMode)
            {
                StartStage(0);
            }
            else
            {
                PrepareTitleScreen();
            }
            StartCoroutine(BossPatternLoop());
            StartCoroutine(CrystalPatternLoop());
            StartCoroutine(CaptureOnCommandLine());
        }

        private void OnDestroy()
        {
            if (titleBlindMaterial != null)
            {
                Destroy(titleBlindMaterial);
            }
        }

        private void Update()
        {
            RefreshFixedAspect();
            AnimateVisuals();
            if (titleActive)
            {
                HandleTitleInput();
                return;
            }
            if (hubActive)
            {
                if (!inputLocked && !movementFrozen)
                {
                    ReadKeyboard();
                    ReadPointer();
                }
                return;
            }
            if (HandleSkipInput())
            {
                return;
            }
            if (inputLocked || movementFrozen)
            {
                return;
            }

            ReadKeyboard();
            ReadPointer();
        }

        private void RefreshFixedAspect()
        {
            if (uiCamera == null || Screen.width <= 0 || Screen.height <= 0 ||
                (appliedScreenWidth == Screen.width && appliedScreenHeight == Screen.height))
            {
                return;
            }

            appliedScreenWidth = Screen.width;
            appliedScreenHeight = Screen.height;
            float screenAspect = (float)Screen.width / Screen.height;
            uiCamera.rect = CalculateViewport(screenAspect, Application.isMobilePlatform);
        }

        public static Rect CalculateViewport(float screenAspect, bool fillScreen)
        {
            Rect viewport = new Rect(0f, 0f, 1f, 1f);
            if (fillScreen || screenAspect <= 0f)
            {
                return viewport;
            }
            float targetAspect = fillScreen ? screenAspect : LandscapeAspect;
            if (screenAspect > targetAspect)
            {
                viewport.width = targetAspect / screenAspect;
                viewport.x = (1f - viewport.width) * 0.5f;
            }
            else if (screenAspect < targetAspect)
            {
                viewport.height = screenAspect / targetAspect;
                viewport.y = (1f - viewport.height) * 0.5f;
            }
            return viewport;
        }

        private void ReadKeyboard()
        {
            if (!TryReadSingleKeyboardDirection(out Vector2Int direction))
            {
                return;
            }

            Move(direction);
        }

        private bool TryReadSingleKeyboardDirection(out Vector2Int direction)
        {
            direction = Vector2Int.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                keyboardDirectionLatched = false;
                return false;
            }

            bool upHeld = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
            bool rightHeld = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
            bool downHeld = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
            bool leftHeld = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
            int heldDirectionCount = (upHeld ? 1 : 0) + (rightHeld ? 1 : 0) +
                (downHeld ? 1 : 0) + (leftHeld ? 1 : 0);

            if (keyboardDirectionLatched)
            {
                if (heldDirectionCount == 0)
                {
                    keyboardDirectionLatched = false;
                }
                return false;
            }

            // Multiple directions cancel each other. The player must release all
            // direction keys before another movement input can be accepted.
            if (heldDirectionCount != 1)
            {
                keyboardDirectionLatched = heldDirectionCount > 1;
                return false;
            }

            bool upPressed = keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
            bool rightPressed = keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;
            bool downPressed = keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame;
            bool leftPressed = keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
            if (!upPressed && !rightPressed && !downPressed && !leftPressed)
            {
                return false;
            }

            keyboardDirectionLatched = true;
            direction = upHeld ? Vector2Int.up :
                rightHeld ? Vector2Int.right :
                downHeld ? Vector2Int.down : Vector2Int.left;
            return true;
        }

        private void ReadPointer()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                if (directionPadArea != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(
                        directionPadArea, pointer.position.ReadValue(), uiCamera))
                {
                    pointerDown = false;
                    return;
                }
                pointerDown = true;
                pressPosition = pointer.position.ReadValue();
            }

            if (!pointerDown || !pointer.press.wasReleasedThisFrame)
            {
                return;
            }

            pointerDown = false;
            if (directionPadArea != null &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    directionPadArea, pointer.position.ReadValue(), uiCamera))
            {
                return;
            }
            Vector2 delta = pointer.position.ReadValue() - pressPosition;
            if (delta.magnitude < SwipeThreshold)
            {
                return;
            }

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                Move(delta.x > 0f ? Vector2Int.right : Vector2Int.left);
            else
                Move(delta.y > 0f ? Vector2Int.up : Vector2Int.down);
        }

        private void Move(Vector2Int direction)
        {
            if (direction != Vector2Int.zero)
            {
                playerFacing = direction;
            }
            if (hubActive)
            {
                HubMove(direction);
                return;
            }
            if (tutorialActive)
            {
                TutorialMove(direction);
                return;
            }
            bool blockedByCrystal = model.IsBlocked(model.Player + direction);
            MoveResult result = model.TryMove(direction);
            switch (result)
            {
                case MoveResult.Blocked:
                    statusText.text = blockedByCrystal
                        ? "공격 수정은 통과할 수 없는 벽입니다"
                        : "필드 밖으로는 이동할 수 없어요";
                    StartCoroutine(FlashFrame(Danger));
                    StartCoroutine(PunchPlayer(true));
                    PlaySfx(blockedSfx);
                    break;
                case MoveResult.Moved:
                    statusText.text = "초록색 START 타일로 돌아가세요";
                    SpawnStepParticles(Muted);
                    StartCoroutine(PunchPlayer(false));
                    PlaySfx(moveSfx, 0.82f);
                    break;
                case MoveResult.TrailStarted:
                    statusText.text = "경로 기록 시작! 주황색 END를 향하세요";
                    SpawnStepParticles(StartColor);
                    StartCoroutine(PunchPlayer(false));
                    PlaySfx(startSfx);
                    break;
                case MoveResult.TrailExtended:
                    statusText.text = "경로를 길게 만들수록 공격력이 증가합니다";
                    SpawnStepParticles(Trail);
                    StartCoroutine(PunchPlayer(false));
                    PlaySfx(moveSfx, Mathf.Min(1.55f, 0.95f + model.Trail.Count * 0.035f));
                    break;
                case MoveResult.TrailReset:
                    statusText.text = "경로 중복! 기록이 초기화되었습니다";
                    StartCoroutine(FlashFrame(Danger));
                    SpawnBurst(model.Player, Danger, 14);
                    StartCoroutine(PunchPlayer(true));
                    PlaySfx(resetSfx);
                    break;
                case MoveResult.AttackReady:
                    SpawnStepParticles(EndColor);
                    StartCoroutine(ExecuteAttack());
                    break;
            }

            if (result != MoveResult.Blocked && !playerDead)
            {
                ResolveSpecialTile(model.Player);
            }

            RefreshBoard();
        }

        private void HubMove(Vector2Int direction)
        {
            HubMoveResult result = hubModel.TryMove(direction, out HubObjectData interactedObject);
            switch (result)
            {
                case HubMoveResult.Blocked:
                    StartCoroutine(PunchPlayer(true));
                    PlaySfx(blockedSfx);
                    break;
                case HubMoveResult.Moved:
                    SpawnBurst(hubModel.Player, activeCharacterTint, 5);
                    StartCoroutine(PunchPlayer(false));
                    PlaySfx(moveSfx, 0.9f);
                    break;
                case HubMoveResult.Previewed:
                    PlaySfx(targetLockSfx, 1.1f, 0.65f);
                    break;
                case HubMoveResult.CharacterChanged:
                    activeCharacterTint = HubCharacterTints[hubModel.CurrentCharacter];
                    mainPlayerImage.color = activeCharacterTint;
                    SpawnBurst(hubModel.Player, activeCharacterTint, 24);
                    StartCoroutine(PunchPlayer(false));
                    PlaySfx(phaseTwoSfx, 1.3f, 0.75f);
                    break;
                case HubMoveResult.StageSelected:
                    hubActive = false;
                    HideHubInteractionPanel();
                    PlaySfx(startSfx, 0.88f, 1f);
                    StartStage(interactedObject.Index);
                    return;
                case HubMoveResult.StageLocked:
                    PlaySfx(blockedSfx, 0.72f, 0.85f);
                    break;
            }

            RefreshHubBoard();
        }

        private void StartHub()
        {
            hubActive = true;
            tutorialActive = false;
            tutorialTransitioning = false;
            phaseTwoActive = false;
            gameCleared = false;
            playerDead = false;
            inputLocked = false;
            movementFrozen = false;
            stageTimerRunning = false;
            warnedCells.Clear();
            targetedCells.Clear();
            crystalCells.Clear();
            crystalWarningCounts.Clear();
            crystalFiringCounts.Clear();
            specialTiles.Clear();
            hubModel.Reset(hubModel.CurrentCharacter);
            RestoreMainGridLayout();
            battleCameraInitialized = false;
            mainGrid.anchoredPosition = Vector2.zero;
            activeCharacterTint = HubCharacterTints[hubModel.CurrentCharacter];
            mainPlayer.sizeDelta = Vector2.one * (mainCellSize * BattlePlayerSizeRatio);
            mainPlayer.localScale = Vector3.one;
            mainPlayerImage.color = activeCharacterTint;
            SetWorldBossVisible(false);
            RefreshCrystalVisuals();
            HideHubInteractionPanel();
            RefreshHubBoard();
        }

        private void RefreshHubBoard()
        {
            if (!hubActive)
            {
                return;
            }

            if (hubCampZone != null) hubCampZone.gameObject.SetActive(true);
            if (hubStageLane != null) hubStageLane.gameObject.SetActive(true);

            for (int y = 0; y < HubWorldModel.Size; y++)
            {
                for (int x = 0; x < HubWorldModel.Size; x++)
                {
                    Image tile = mainTiles[x, y];
                    arenaGroundTiles[x, y].gameObject.SetActive(false);
                    mainTileDepthRoots[x, y].gameObject.SetActive(UseExtraTileDepth);
                    tile.gameObject.SetActive(true);
                    Color color = Color.Lerp(ArenaTile, ArenaTileLift, (x + y) % 2 == 0 ? 0.28f : 0.08f);
                    color.a = 0.68f;
                    tile.color = color;
                    SetTileDepthColor(x, y, color);
                    Outline tileOutline = mainTileOutlines[x, y];
                    if (tileOutline != null)
                    {
                        bool edge = x == 0 || y == 0 || x == HubWorldModel.Size - 1 || y == HubWorldModel.Size - 1;
                        tileOutline.effectColor = edge ? Hex("5B3021") : Hex("25202A");
                        tileOutline.effectDistance = edge ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
                    }

                    specialItemVisuals[x, y].gameObject.SetActive(false);
                    endpointMarkerImages[x, y].gameObject.SetActive(false);
                    tileLabels[x, y].text = string.Empty;
                    attackWarningVisuals[x, y].gameObject.SetActive(false);
                    hubObjectVisuals[x, y].gameObject.SetActive(false);
                }
            }

            foreach (KeyValuePair<Vector2Int, HubObjectData> entry in hubModel.Objects)
            {
                Vector2Int cell = entry.Key;
                HubObjectData data = entry.Value;
                RectTransform visual = hubObjectVisuals[cell.x, cell.y];
                Image back = hubObjectImages[cell.x, cell.y];
                Image icon = hubObjectIconImages[cell.x, cell.y];
                Text label = hubObjectLabels[cell.x, cell.y];
                Outline outline = visual.GetComponent<Outline>();
                visual.gameObject.SetActive(true);
                visual.anchoredPosition = GridPosition(cell.x, cell.y, mainCellSize);
                label.text = string.Empty;

                switch (data.Type)
                {
                    case HubObjectType.Campfire:
                        back.color = Hex("24140E");
                        outline.effectColor = Hex("FF8A32");
                        icon.sprite = campfireSprite;
                        icon.color = White;
                        break;
                    case HubObjectType.Character:
                        Color tint = HubCharacterTints[data.Index];
                        back.color = new Color(tint.r * 0.16f, tint.g * 0.16f, tint.b * 0.16f, 0.94f);
                        outline.effectColor = tint;
                        icon.sprite = playerCharacterSprite;
                        icon.color = tint;
                        break;
                    default:
                        bool available = data.Index == 0;
                        back.color = available ? Hex("260B12") : Hex("111117");
                        outline.effectColor = available ? Danger : Hex("5D5968");
                        icon.sprite = bossPortraitSprite != null
                            ? bossPortraitSprite
                            : LoadPixelSprite("Art/red_attack_crystal", 64f);
                        icon.color = available ? White : Hex("625E68");
                        label.text = available ? "I" : "LOCK";
                        label.color = available ? Danger : Muted;
                        break;
                }
            }

            for (int i = 0; i < crystalVisuals.Length; i++)
            {
                if (crystalVisuals[i] != null)
                {
                    crystalVisuals[i].gameObject.SetActive(false);
                }
            }

            mainPlayer.anchoredPosition = GridPosition(hubModel.Player.x, hubModel.Player.y, mainCellSize);
            mainPlayer.sizeDelta = Vector2.one * (mainCellSize * BattlePlayerSizeRatio);
            mainPlayerImage.color = activeCharacterTint;
            mainPlayer.SetAsLastSibling();
            RefreshHubInteractionPanel();
        }

        private void RefreshHubInteractionPanel()
        {
            if (interactionPanelRoot == null || titleActive ||
                !hubModel.TryGetFocusedObject(out _, out HubObjectData data))
            {
                HideHubInteractionPanel();
                return;
            }

            interactionPanelRoot.gameObject.SetActive(true);
            interactionPanelRoot.SetAsLastSibling();
            switch (data.Type)
            {
                case HubObjectType.Campfire:
                    interactionTitleText.text = "원탁의 모닥불";
                    interactionBodyText.text = "메인 허브의 중심 · 주변 캐릭터와 스테이지 장치를 확인하세요";
                    interactionIconImage.sprite = campfireSprite;
                    interactionIconImage.color = White;
                    interactionRingImage.color = Hex("FF8A32");
                    break;
                case HubObjectType.Character:
                    interactionTitleText.text = "캐릭터 변경 · " + HubCharacterNames[data.Index];
                    interactionBodyText.text = HubCharacterDescriptions[data.Index] +
                        " · 한 번 더 충돌하면 교체";
                    interactionIconImage.sprite = playerCharacterSprite;
                    interactionIconImage.color = HubCharacterTints[data.Index];
                    interactionRingImage.color = HubCharacterTints[data.Index];
                    break;
                default:
                    bool available = data.Index == 0;
                    interactionTitleText.text = available
                        ? "STAGE 01 · 크림슨 골렘"
                        : "STAGE " + (data.Index + 1).ToString("00") + " · 잠김";
                    interactionBodyText.text = available
                        ? "광물 증식형 바이러스 · 레이저와 수정 격자 공격 · 한 번 더 충돌하면 입장"
                        : "후속 바이러스 데이터가 아직 해제되지 않았습니다";
                    interactionIconImage.sprite = bossPortraitSprite != null
                        ? bossPortraitSprite
                        : LoadPixelSprite("Art/red_attack_crystal", 64f);
                    interactionIconImage.color = available ? White : Muted;
                    interactionRingImage.color = available ? Danger : Muted;
                    break;
            }
        }

        private void HideHubInteractionPanel()
        {
            if (interactionPanelRoot != null)
            {
                interactionPanelRoot.gameObject.SetActive(false);
            }
        }

        private void PrepareTitleScreen()
        {
            StartStage(0);
            var center = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);
            model.TryPlacePlayer(center);
            battleCameraInitialized = false;
            GenerateSpecialTiles();
            specialTiles.Remove(center);
            specialTiles.Remove(center + Vector2Int.up);
            specialTiles.Remove(center + Vector2Int.right);
            specialTiles.Remove(center + Vector2Int.down);
            specialTiles.Remove(center + Vector2Int.left);
            RefreshBoard();
            ShowTitleScreen();
        }

        private void ShowTitleScreen()
        {
            titleActive = true;
            titleRevealing = false;
            inputLocked = true;
            movementFrozen = false;
            stageTimerRunning = false;
            if (titleScreen != null)
            {
                titleScreen.gameObject.SetActive(true);
                titleScreen.SetAsLastSibling();
            }
            if (titleForegroundGroup != null)
            {
                titleForegroundGroup.alpha = 1f;
            }
            SetTitleBlindRadius(TitleInitialRadius);
            Canvas.ForceUpdateCanvases();
            UpdateTitleBlindFocus();
        }

        private void HandleTitleInput()
        {
            if (titleRevealing)
            {
                return;
            }

            UpdateTitleBlindFocus();
            if (titleInputHint != null)
            {
                Color hintColor = titleInputHint.color;
                hintColor.a = 0.72f + (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.14f;
                titleInputHint.color = hintColor;
            }

            if (TryReadSingleKeyboardDirection(out Vector2Int direction))
            {
                StartCoroutine(RevealTitleScreen(direction));
            }
        }

        private IEnumerator RevealTitleScreen(Vector2Int firstDirection)
        {
            if (!titleActive || titleRevealing)
            {
                yield break;
            }

            titleRevealing = true;
            Move(firstDirection);
            PlaySfx(startSfx, 1.08f, 0.9f);
            UpdateTitleBlindFocus();

            Vector2 center = GetTitleBlindCenter();
            float targetRadius = CalculateTitleRevealRadius(center, GetTitleScreenAspect());
            float elapsed = 0f;
            while (elapsed < TitleRevealSeconds)
            {
                elapsed = Mathf.Min(TitleRevealSeconds, elapsed + Time.unscaledDeltaTime);
                float normalized = elapsed / TitleRevealSeconds;
                float eased = 1f - Mathf.Pow(1f - normalized, 3f);
                SetTitleBlindRadius(Mathf.Lerp(TitleInitialRadius, targetRadius, eased));
                UpdateTitleBlindFocus();
                if (titleForegroundGroup != null)
                {
                    titleForegroundGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01(normalized / 0.55f));
                }
                yield return null;
            }

            titleActive = false;
            titleRevealing = false;
            if (titleScreen != null)
            {
                titleScreen.gameObject.SetActive(false);
            }
            inputLocked = false;
            stageTimerRunning = true;
            stageStartRealtime = Time.realtimeSinceStartup;
            statusText.text = "START 타일을 찾아 경로 공격을 시작하세요";
        }

        private void UpdateTitleBlindFocus()
        {
            if (titleBlindMaterial == null || mainPlayer == null || uiCamera == null)
            {
                return;
            }

            Vector2 center = GetTitleBlindCenter();
            titleBlindMaterial.SetVector("_Center", new Vector4(center.x, center.y, 0f, 0f));
            titleBlindMaterial.SetFloat("_Aspect", GetTitleScreenAspect());
        }

        private Vector2 GetTitleBlindCenter()
        {
            if (mainPlayer == null || uiCamera == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return new Vector2(0.5f, 0.36f);
            }

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, mainPlayer.position);
            return new Vector2(
                Mathf.Clamp01(screenPoint.x / Screen.width),
                Mathf.Clamp01(screenPoint.y / Screen.height));
        }

        private float GetTitleScreenAspect()
        {
            return Screen.height > 0 ? (float)Screen.width / Screen.height : LandscapeAspect;
        }

        private void SetTitleBlindRadius(float radius)
        {
            if (titleBlindMaterial != null)
            {
                titleBlindMaterial.SetFloat("_Radius", radius);
            }
        }

        public static float CalculateTitleRevealRadius(Vector2 center, float aspect)
        {
            aspect = Mathf.Max(0.01f, aspect);
            float radius = 0f;
            Vector2[] corners =
            {
                Vector2.zero,
                Vector2.right,
                Vector2.up,
                Vector2.one
            };
            foreach (Vector2 corner in corners)
            {
                Vector2 delta = corner - center;
                delta.x *= aspect;
                radius = Mathf.Max(radius, delta.magnitude);
            }
            return radius + 0.08f;
        }

        private void UpdateTitleRecord()
        {
            float bestTime = PlayerPrefs.GetFloat(BestClearTimeKey, -1f);
            if (bestRecordText == null || bestRatingText == null)
            {
                return;
            }

            if (bestTime <= 0f)
            {
                bestRecordText.text = "기록 없음";
                bestRatingText.text = "RANK  —";
                bestRatingText.color = Muted;
                return;
            }

            string rating = GetClearRating(bestTime);
            bestRecordText.text = FormatClearTime(bestTime);
            bestRatingText.text = "RANK  " + rating;
            bestRatingText.color = GetRatingColor(rating);
        }

        private string CompleteStageTimer()
        {
            if (!stageTimerRunning || bossPhaseSkipped)
            {
                stageTimerRunning = false;
                return string.Empty;
            }

            float clearTime = Mathf.Max(0.01f, Time.realtimeSinceStartup - stageStartRealtime);
            stageTimerRunning = false;
            float previousBest = PlayerPrefs.GetFloat(BestClearTimeKey, -1f);
            bool newBest = previousBest <= 0f || clearTime < previousBest;
            if (newBest)
            {
                PlayerPrefs.SetFloat(BestClearTimeKey, clearTime);
                PlayerPrefs.Save();
            }

            return " · " + FormatClearTime(clearTime) + " · RANK " + GetClearRating(clearTime) +
                (newBest ? " · NEW BEST" : string.Empty);
        }

        private static string FormatClearTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remaining = seconds - minutes * 60f;
            return minutes.ToString("00") + ":" + remaining.ToString("00.00");
        }

        private static string GetClearRating(float seconds)
        {
            if (seconds < 120f) return "S";
            if (seconds < 180f) return "A";
            if (seconds < 240f) return "B";
            if (seconds < 360f) return "C";
            return "D";
        }

        private static Color GetRatingColor(string rating)
        {
            switch (rating)
            {
                case "S": return TrailHot;
                case "A": return StartColor;
                case "B": return Trail;
                case "C": return EndColor;
                default: return Muted;
            }
        }

        private void StartTutorial()
        {
            titleActive = false;
            if (titleScreen != null)
            {
                titleScreen.gameObject.SetActive(false);
            }
            tutorialVersion++;
            tutorialActive = true;
            tutorialTransitioning = false;
            tutorialStep = 0;
            tutorialPlayer = TutorialRules.Start;
            tutorialTrail.Clear();
            tutorialTrailOrder.Clear();
            tutorialTrail.Add(TutorialRules.Start);
            tutorialTrailOrder.Add(TutorialRules.Start);
            inputLocked = false;
            movementFrozen = false;
            playerDead = false;
            phaseTwoActive = false;
            battleCameraInitialized = false;
            mainGrid.anchoredPosition = Vector2.zero;
            mainPlayer.sizeDelta = Vector2.one * (ActiveTutorialCellSize * TutorialPlayerSizeRatio);
            mainPlayer.localScale = Vector3.one;
            crystalCells.Clear();
            specialTiles.Clear();
            RefreshCrystalVisuals();
            SetWorldBossVisible(false);

            stageText.text = "TUTORIAL";
            bossNameText.text = "전투 훈련";
            playerHealthText.text = "♥  HP 1";
            shapeText.text = "ESC · 즉시 SKIP";
            shapeText.color = Muted;
            fieldTitleText.text = "TRAINING GRID 4×4";
            bossHealthFill.color = Trail;
            statusText.text = "기본 공격 — S에서 E까지 겹치지 않게 경로를 연결하세요";
            UpdateTutorialInstruction();
            RefreshTutorialBoard();
        }

        private void TutorialMove(Vector2Int direction)
        {
            if (tutorialTransitioning)
            {
                return;
            }

            Vector2Int target = tutorialPlayer + direction;
            if (!TutorialRules.IsInside(target))
            {
                statusText.text = "4×4 훈련장 밖으로는 이동할 수 없습니다";
                PlaySfx(blockedSfx);
                StartCoroutine(FlashFrame(Danger));
                return;
            }

            tutorialPlayer = target;
            PlaySfx(moveSfx, 1.05f);
            if (tutorialStep == 0)
            {
                if (tutorialTrail.Contains(target))
                {
                    tutorialTrail.Clear();
                    tutorialTrailOrder.Clear();
                    if (target == TutorialRules.Start)
                    {
                        tutorialTrail.Add(target);
                        tutorialTrailOrder.Add(target);
                        statusText.text = "S에서 경로 기록을 다시 시작합니다";
                    }
                    else
                    {
                        statusText.text = "경로 중복 — S 타일로 돌아가 다시 시작하세요";
                    }
                    PlaySfx(resetSfx);
                }
                else if (tutorialTrail.Count == 0)
                {
                    if (target == TutorialRules.Start)
                    {
                        tutorialTrail.Add(target);
                        tutorialTrailOrder.Add(target);
                        statusText.text = "경로 기록 시작 — E를 향하세요";
                    }
                    else
                    {
                        statusText.text = "먼저 S 타일을 밟아야 합니다";
                    }
                }
                else
                {
                    tutorialTrail.Add(target);
                    tutorialTrailOrder.Add(target);
                    statusText.text = "연결 중 — 같은 칸을 다시 밟지 마세요";
                    if (target == TutorialRules.End)
                    {
                        StartCoroutine(CompleteTutorialAttack());
                    }
                }
            }
            else
            {
                int specialStep = tutorialStep - 1;
                if (specialStep < TutorialRules.SpecialStepCount &&
                    target == TutorialRules.GetSpecialTarget(specialStep))
                {
                    StartCoroutine(CompleteTutorialSpecial(specialStep));
                }
            }
            RefreshTutorialBoard();
        }

        private IEnumerator CompleteTutorialAttack()
        {
            int version = tutorialVersion;
            tutorialTransitioning = true;
            inputLocked = true;
            statusText.text = "공격 성공 — 연결한 칸이 많을수록 실제 공격 피해가 증가합니다";
            PlaySfx(attackSfx);
            yield return StartCoroutine(AnimateTutorialSlash());
            if (!tutorialActive || version != tutorialVersion)
            {
                yield break;
            }
            SpawnBurst(TutorialRules.End, TrailHot, 28);
            PlaySfx(hitSfx);
            yield return new WaitForSeconds(0.45f);
            if (!tutorialActive || version != tutorialVersion)
            {
                yield break;
            }

            tutorialStep = 1;
            tutorialTransitioning = false;
            inputLocked = false;
            UpdateTutorialInstruction();
            RefreshTutorialBoard();
        }

        private IEnumerator CompleteTutorialSpecial(int specialStep)
        {
            int version = tutorialVersion;
            tutorialTransitioning = true;
            SpecialTileType type = TutorialRules.GetSpecialType(specialStep);
            Color color = GetSpecialTileColor(type);

            SpawnBurst(tutorialPlayer, color, 20);
            StartCoroutine(FlashFrame(color));

            switch (type)
            {
                case SpecialTileType.Power:
                    statusText.text = "노란색 + — 다음 공격에 고정 피해 25가 추가됩니다";
                    PlaySfx(startSfx, 1.35f);
                    break;
                case SpecialTileType.Amplify:
                    statusText.text = "청록색 ◆ — 다음 공격 피해가 1.35배가 됩니다";
                    PlaySfx(startSfx, 1.55f);
                    break;
                case SpecialTileType.Mud:
                    movementFrozen = true;
                    mainPlayerImage.color = color;
                    statusText.text = "갈색 ≈ — 보스 시간은 흐르고 이동만 1초 정지합니다";
                    PlaySfx(blockedSfx, 0.75f);
                    yield return new WaitForSeconds(SpecialTileRules.MudLockSeconds);
                    if (!tutorialActive || version != tutorialVersion)
                    {
                        yield break;
                    }
                    movementFrozen = false;
                    mainPlayerImage.color = White;
                    break;
                case SpecialTileType.Curse:
                    statusText.text = "보라색 ▼ — 다음 공격 피해가 0.65배로 감소합니다";
                    PlaySfx(resetSfx, 0.82f);
                    break;
            }

            yield return new WaitForSeconds(0.55f);
            if (!tutorialActive || version != tutorialVersion)
            {
                yield break;
            }

            if (specialStep == TutorialRules.SpecialStepCount - 1)
            {
                statusText.text = "보라색 발판 체험 완료 — 크림슨 골렘 보스전으로 이동합니다";
                yield return StartCoroutine(FinishTutorial(false));
                yield break;
            }

            tutorialStep++;
            tutorialTransitioning = false;
            UpdateTutorialInstruction();
            RefreshTutorialBoard();
        }

        private IEnumerator FinishTutorial(bool skipped)
        {
            if (!tutorialActive)
            {
                yield break;
            }
            tutorialVersion++;
            tutorialActive = false;
            tutorialTransitioning = true;
            inputLocked = true;
            movementFrozen = false;
            mainPlayer.localRotation = Quaternion.identity;
            mainPlayer.localScale = Vector3.one;
            mainPlayerImage.color = White;
            playerHealthText.color = StartColor;

            phaseBanner.text = skipped ? "TUTORIAL SKIPPED" : "TRAINING COMPLETE\nCRIMSON GOLEM";
            phaseBanner.color = skipped ? Muted : Danger;
            phaseBannerGroup.alpha = 1f;
            phaseBanner.rectTransform.localScale = Vector3.one;
            yield return new WaitForSeconds(skipped ? 0.35f : 0.9f);
            phaseBannerGroup.alpha = 0f;
            StartStage(0);
        }

        private bool HandleSkipInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return false;
            }

            if (tutorialActive)
            {
                tutorialVersion++;
                tutorialActive = false;
                tutorialTransitioning = false;
                StartStage(0);
                return true;
            }

            if (inputLocked || playerDead || gameCleared)
            {
                return false;
            }

            StartCoroutine(SkipBossPhase());
            return true;
        }

        private IEnumerator SkipBossPhase()
        {
            inputLocked = true;
            bossPhaseSkipped = true;
            movementFrozen = false;
            movementFreezeVersion++;
            patternVersion++;
            warnedCells.Clear();
            targetedCells.Clear();
            hazardFiring = false;
            targetedFiring = false;
            hazardTelegraphProgress = 0f;
            targetedTelegraphProgress = 0f;
            crystalWarningCounts.Clear();
            crystalFiringCounts.Clear();
            crystalTelegraphProgress.Clear();

            if (!phaseTwoActive)
            {
                bossHealth = 0;
                bossHealthFill.fillAmount = 0f;
                bossHealthText.text = "0 / " + bossMaxHealth;
                statusText.text = "ESC — 1페이즈 스킵";
                RefreshBoard();
                yield return StartCoroutine(EnterPhaseTwo());
                inputLocked = false;
                statusText.text = "2페이즈 시작 — ESC로 2페이즈도 스킵할 수 있습니다";
                yield break;
            }

            bossHealth = 0;
            bossHealthFill.fillAmount = 0f;
            bossHealthText.text = "0 / " + bossMaxHealth;
            gameCleared = true;
            bossPhaseSkipped = true;
            stageTimerRunning = false;
            crystalLayoutVersion++;
            crystalCells.Clear();
            RefreshCrystalVisuals();
            statusText.text = "ESC — 2페이즈 스킵 · STAGE CLEAR";
            PlaySfx(victorySfx);
            RefreshBoard();
        }

        private void UpdateTutorialInstruction()
        {
            int totalSteps = TutorialRules.SpecialStepCount + 1;
            bossHealthFill.fillAmount = (tutorialStep + 1f) / totalSteps;
            bossHealthText.text = "STEP " + (tutorialStep + 1) + " / " + totalSteps;
            if (tutorialStep == 0)
            {
                comboText.text = "기본 공격 · S → E 연결";
                powerText.text = "같은 칸을 다시 밟으면 경로가 초기화됩니다";
                powerText.color = Trail;
                return;
            }

            int specialStep = tutorialStep - 1;
            SpecialTileType type = TutorialRules.GetSpecialType(specialStep);
            switch (type)
            {
                case SpecialTileType.Power:
                    comboText.text = "노란색 + 타일을 밟으세요";
                    powerText.text = "이득 · 다음 공격 피해 +25";
                    break;
                case SpecialTileType.Amplify:
                    comboText.text = "청록색 ◆ 타일을 밟으세요";
                    powerText.text = "이득 · 다음 공격 피해 ×1.35";
                    break;
                case SpecialTileType.Mud:
                    comboText.text = "갈색 ≈ 타일을 밟으세요";
                    powerText.text = "손해 · 플레이어 이동 1초 정지";
                    break;
                case SpecialTileType.Curse:
                    comboText.text = "마지막 보라색 ▼ 타일을 밟으세요";
                    powerText.text = "손해 · 피해 ×0.65 · 체험 후 보스전 전환";
                    break;
                default:
                    comboText.text = "특수 발판 체험";
                    powerText.text = "표시된 발판으로 이동하세요";
                    break;
            }
            powerText.color = SpecialTileRules.IsBeneficial(type) ? TrailHot : Danger;
            statusText.text = "표시된 특수 타일까지 이동해 효과를 직접 확인하세요";
        }

        private void RefreshTutorialBoard()
        {
            HideHubWorldVisuals();
            int activeSpecialStep = tutorialStep - 1;
            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    bool active = x < TutorialRules.Size && y < TutorialRules.Size;
                    arenaGroundTiles[x, y].gameObject.SetActive(false);
                    mainTileDepthRoots[x, y].gameObject.SetActive(active && UseExtraTileDepth);
                    mainTiles[x, y].gameObject.SetActive(active);
                    if (!active)
                    {
                        continue;
                    }

                    var cell = new Vector2Int(x, y);
                    LayoutTileDepth(x, y, ActiveTutorialCellSize, TutorialGridPosition(cell));
                    RectTransform ground = arenaGroundTiles[x, y].rectTransform;
                    ground.sizeDelta = Vector2.one * (ActiveTutorialCellSize + (desktopLayout ? 18f : 14f));
                    ground.anchoredPosition = TutorialGridPosition(cell);
                    RectTransform tile = mainTiles[x, y].rectTransform;
                    tile.sizeDelta = Vector2.one * (ActiveTutorialCellSize - 7f);
                    tile.anchoredPosition = TutorialGridPosition(cell);
                    Color color = GetFloorColor(x, y);
                    string marker = string.Empty;
                    bool showSpecialItem = false;
                    SpecialTileType shownSpecialType = SpecialTileType.Power;
                    if (tutorialStep == 0 && tutorialTrail.Contains(cell)) color = Trail;
                    if (tutorialStep == 0 && cell == TutorialRules.Start)
                    {
                        color = StartColor;
                        marker = startMarkerSprite == null ? "S" : string.Empty;
                    }
                    if (tutorialStep == 0 && cell == TutorialRules.End)
                    {
                        color = EndColor;
                        marker = endMarkerSprite == null ? "E" : string.Empty;
                    }
                    if (activeSpecialStep >= 0 && activeSpecialStep < TutorialRules.SpecialStepCount &&
                        cell == TutorialRules.GetSpecialTarget(activeSpecialStep))
                    {
                        SpecialTileType type = TutorialRules.GetSpecialType(activeSpecialStep);
                        showSpecialItem = true;
                        shownSpecialType = type;
                        marker = string.Empty;
                        tileLabels[x, y].color = Background;
                    }
                    else
                    {
                        tileLabels[x, y].color = Background;
                    }
                    color.a = tutorialStep == 0 && tutorialTrail.Contains(cell) ? 1f : StandardTileOpacity;
                    mainTiles[x, y].color = color;
                    SetTileDepthColor(x, y, color);
                    SetSpecialItemVisual(x, y, showSpecialItem, shownSpecialType, ActiveTutorialCellSize * 0.34f);
                    SetEndpointMarkerVisual(x, y,
                        tutorialStep == 0 && cell == TutorialRules.Start,
                        tutorialStep == 0 && cell == TutorialRules.End,
                        ActiveTutorialCellSize * 0.64f);
                    tileLabels[x, y].fontSize = 24;
                    tileLabels[x, y].text = marker;
                }
            }
            mainPlayer.anchoredPosition = TutorialGridPosition(tutorialPlayer);
            mainPlayer.SetAsLastSibling();
            RefreshMinimap();
        }

        private Vector2 TutorialGridPosition(Vector2Int cell)
        {
            float center = (TutorialRules.Size - 1) * 0.5f;
            return new Vector2((cell.x - center) * ActiveTutorialCellSize,
                (cell.y - center) * ActiveTutorialCellSize);
        }

        private IEnumerator AnimateTutorialSlash()
        {
            int version = tutorialVersion;
            Vector2 from = TutorialGridPosition(TutorialRules.Start);
            Vector2 to = TutorialGridPosition(TutorialRules.End);
            Vector2 delta = to - from;
            attackSlash.anchoredPosition = (from + to) * 0.5f;
            attackSlash.sizeDelta = new Vector2(delta.magnitude + ActiveTutorialCellSize, 26f);
            attackSlash.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            attackSlash.SetAsLastSibling();
            for (float t = 0f; t < 1f && tutorialActive && version == tutorialVersion; t += Time.deltaTime * 7f)
            {
                float visible = Mathf.Sin(t * Mathf.PI);
                attackSlash.localScale = new Vector3(Mathf.Clamp01(t * 3f), 1f + visible * 2f, 1f);
                attackSlashGroup.alpha = visible;
                yield return null;
            }
            attackSlashGroup.alpha = 0f;
            attackSlash.localScale = Vector3.one;
        }

        private void ResolveSpecialTile(Vector2Int cell)
        {
            if (!specialTiles.TryGetValue(cell, out SpecialTileType type))
            {
                return;
            }

            specialTiles.Remove(cell);
            Color effectColor = GetSpecialTileColor(type);
            SpawnBurst(cell, effectColor, SpecialTileRules.IsBeneficial(type) ? 22 : 16);
            StartCoroutine(FlashFrame(effectColor));

            switch (type)
            {
                case SpecialTileType.Power:
                    nextAttackFlatBonus += SpecialTileRules.PowerFlatBonus;
                    statusText.text = "POWER 획득 — 다음 공격 피해 +" + SpecialTileRules.PowerFlatBonus;
                    PlaySfx(startSfx, 1.35f);
                    break;
                case SpecialTileType.Amplify:
                    nextAttackMultiplier *= SpecialTileRules.AmplifyMultiplier;
                    statusText.text = "AMPLIFY 획득 — 다음 공격 피해 ×1.35";
                    PlaySfx(startSfx, 1.55f);
                    break;
                case SpecialTileType.Mud:
                    StartCoroutine(ApplyMovementLock());
                    PlaySfx(blockedSfx, 0.72f);
                    break;
                case SpecialTileType.Curse:
                    nextAttackMultiplier *= SpecialTileRules.CurseMultiplier;
                    statusText.text = "CURSE — 다음 공격 피해가 35% 감소합니다";
                    PlaySfx(resetSfx, 0.78f);
                    break;
            }

            UpdatePowerRuleText();
        }

        private IEnumerator ApplyMovementLock()
        {
            movementFreezeVersion++;
            int version = movementFreezeVersion;
            movementFrozen = true;
            statusText.text = "MUD — 1초 동안 이동할 수 없습니다";
            if (mainPlayerImage != null)
            {
                mainPlayerImage.color = GetSpecialTileColor(SpecialTileType.Mud);
            }

            yield return new WaitForSeconds(SpecialTileRules.MudLockSeconds);
            if (version != movementFreezeVersion || playerDead)
            {
                yield break;
            }

            movementFrozen = false;
            if (mainPlayerImage != null)
            {
                mainPlayerImage.color = White;
            }
            statusText.text = "이동 봉인 해제 — 보스 공격을 계속 피하세요";
        }

        private void UpdatePowerRuleText()
        {
            if (powerText == null)
            {
                return;
            }

            if (nextAttackFlatBonus > 0 || !Mathf.Approximately(nextAttackMultiplier, 1f))
            {
                powerText.text = "보유 효과  피해 +" + nextAttackFlatBonus + "  ×" + nextAttackMultiplier.ToString("0.00");
                powerText.color = nextAttackMultiplier < 1f ? Danger : TrailHot;
            }
            else
            {
                powerText.text = "특수 아이템  + 피해+25  ◆ ×1.35  |  ≈ 정지  ▼ 약화";
                powerText.color = EndColor;
            }

            // Battle items are disabled, so the persistent HUD explains the
            // position-based trail damage instead of obsolete pickup effects.
            powerText.text = "타일 피해  외곽 1  ·  중앙 3×3 구역 2";
            powerText.color = CenterDamageTileTint;
        }

        private static Color GetSpecialTileColor(SpecialTileType type)
        {
            switch (type)
            {
                case SpecialTileType.Power: return Hex("FFD24A");
                case SpecialTileType.Amplify: return Hex("42E7F5");
                case SpecialTileType.Mud: return Hex("A9693E");
                case SpecialTileType.Curse: return Hex("9D5CFF");
                case SpecialTileType.Spike: return Hex("FF3156");
                default: return White;
            }
        }

        private static string GetSpecialTileMarker(SpecialTileType type)
        {
            switch (type)
            {
                case SpecialTileType.Power: return "+";
                case SpecialTileType.Amplify: return "◆";
                case SpecialTileType.Mud: return "≈";
                case SpecialTileType.Curse: return "▼";
                case SpecialTileType.Spike: return "×";
                default: return string.Empty;
            }
        }

        private IEnumerator ExecuteAttack()
        {
            inputLocked = true;
            int damage = CalculateDamage(model.Trail);
            nextAttackFlatBonus = 0;
            nextAttackMultiplier = 1f;
            UpdatePowerRuleText();
            statusText.text = "TRACE COMPLETE — 궤적 공격 발동!";
            PlaySfx(attackSfx);
            StartCoroutine(AnimateAttackSlash());

            for (int i = 0; i < model.Trail.Count; i++)
            {
                Vector2Int cell = model.Trail[i];
                if (cell != model.Start && cell != model.End)
                {
                    mainTiles[cell.x, cell.y].color = TrailHot;
                }
                if (i % 2 == 0)
                {
                    PlaySfx(moveSfx, Mathf.Min(1.75f, 1.1f + i * 0.035f), 0.45f);
                }
                yield return new WaitForSeconds(0.025f);
            }

            SpawnBurst(model.End, TrailHot, 28);
            bossHealth = Mathf.Max(0, bossHealth - damage);
            bossHealthFill.fillAmount = (float)bossHealth / bossMaxHealth;
            bossHealthText.text = bossHealth + " / " + bossMaxHealth;
            UpdatePhaseLabel();
            damagePopup.text = "-" + damage;
            StartCoroutine(ShowDamagePopup());
            StartCoroutine(ShakeHud());
            PlaySfx(hitSfx);
            yield return StartCoroutine(FlashFrame(TrailHot));
            yield return new WaitForSeconds(0.45f);

            if (phaseTwoActive && !crystalsRelocated && bossHealth > 0 && bossHealth <= bossMaxHealth / 2)
            {
                RelocateCrystals();
            }

            if (bossHealth <= 0 && !phaseTwoActive)
            {
                yield return StartCoroutine(EnterPhaseTwo());
            }

            if (bossHealth <= 0)
            {
                gameCleared = true;
                patternVersion++;
                warnedCells.Clear();
                targetedCells.Clear();
                hazardFiring = false;
                targetedFiring = false;
                hazardTelegraphProgress = 0f;
                targetedTelegraphProgress = 0f;
                crystalWarningCounts.Clear();
                crystalFiringCounts.Clear();
                crystalTelegraphProgress.Clear();
                statusText.text = "STAGE CLEAR — 크림슨 골렘 격파!" + CompleteStageTimer();
                PlaySfx(victorySfx);
                RefreshBoard();
            }
            else
            {
                round++;
                model.BeginRound(round, false);
                GenerateSpecialTiles();
            statusText.text = "현재 위치 유지 — 새 START/END 지점이 생성되었습니다";
                RefreshBoard();
            }

            inputLocked = gameCleared;
        }

        private void StartStage(int nextStage, bool preservePlayer = false)
        {
            titleActive = false;
            hubActive = false;
            HideHubInteractionPanel();
            if (titleScreen != null)
            {
                titleScreen.gameObject.SetActive(false);
            }
            tutorialActive = false;
            tutorialTransitioning = false;
            stage = nextStage;
            round = 0;
            bossMaxHealth = BossPatternRules.PhaseMaxHealth(false);
            bossHealth = bossMaxHealth;
            bossAttackCount = 0;
            glyphPatternIndex = 0;
            diamondUseCount = 0;
            patternVersion++;
            playerDead = false;
            gameCleared = false;
            bossPhaseSkipped = false;
            stageTimerRunning = true;
            stageStartRealtime = Time.realtimeSinceStartup;
            inputLocked = false;
            movementFrozen = false;
            playerFacing = Vector2Int.up;
            movementFreezeVersion++;
            nextAttackFlatBonus = 0;
            nextAttackMultiplier = 1f;
            hazardFiring = false;
            targetedFiring = false;
            phaseTwoActive = false;
            crystalsRelocated = false;
            crystalLayoutVersion++;
            crystalCells.Clear();
            crystalWarningCounts.Clear();
            crystalFiringCounts.Clear();
            crystalTelegraphProgress.Clear();
            warnedCells.Clear();
            targetedCells.Clear();
            hazardTelegraphProgress = 0f;
            targetedTelegraphProgress = 0f;
            battleCameraInitialized = false;
            RestoreMainGridLayout();
            RandomizeFloorTileLayout();
            ApplyFloorTileLayout();
            currentFieldSize = BossPatternRules.FieldSizeForBoss(nextStage);
            model.CreateField(nextStage, currentFieldSize);
            model.SetBlockedCells(crystalCells);
            model.BeginRound(round);
            GenerateSpecialTiles();
            RefreshCrystalVisuals();
            SetWorldBossVisible(true);

            bossNameText.text = "크림슨 골렘";
            stageText.text = "STAGE 01";
            playerHealthText.text = "♥  HP 1";
            playerHealthText.color = StartColor;
            fieldTitleText.text = "IVY TEMPLE";
            bossHealthFill.color = Danger;
            phaseBanner.color = Danger;
            mainPlayer.localRotation = Quaternion.identity;
            mainPlayer.sizeDelta = Vector2.one * (mainCellSize * BattlePlayerSizeRatio);
            mainPlayer.localScale = Vector3.one;
            mainPlayerImage.color = activeCharacterTint;
            if (phaseBannerGroup != null)
            {
                phaseBannerGroup.alpha = 0f;
            }
            bossHealthFill.fillAmount = 1f;
            bossHealthText.text = bossHealth + " / " + bossMaxHealth;
            UpdatePhaseLabel();
            statusText.text = "START에서 출발 — 보스의 붉은 공격 예고를 피하세요";
            UpdatePowerRuleText();
            RefreshBoard();
        }

        private void SetWorldBossVisible(bool visible)
        {
            if (arenaBossCore != null)
            {
                arenaBossCore.gameObject.SetActive(visible);
            }
            if (arenaBossHealthRoot != null)
            {
                arenaBossHealthRoot.gameObject.SetActive(visible);
            }
        }

        private void RestoreMainGridLayout()
        {
            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    RectTransform ground = arenaGroundTiles[x, y].rectTransform;
                    ground.sizeDelta = Vector2.one * (mainCellSize + (desktopLayout ? 18f : 14f));
                    ground.anchoredPosition = GridPosition(x, y, mainCellSize);
                    LayoutTileDepth(x, y, mainCellSize, GridPosition(x, y, mainCellSize));
                    RectTransform tile = mainTiles[x, y].rectTransform;
                    tile.sizeDelta = Vector2.one * (UseIsometricArena
                        ? mainCellSize + 1f
                        : mainCellSize - (desktopLayout ? 7f : 5f));
                    tile.anchoredPosition = GridPosition(x, y, mainCellSize);
                    tileLabels[x, y].fontSize = 24;
                }
            }
        }

        private void LayoutTileDepth(int x, int y, float cellSize, Vector2 position)
        {
            RectTransform root = mainTileDepthRoots[x, y];
            Image depthImage = mainTileDepthImages[x, y];
            RectTransform shadow = mainTileDropShadows[x, y];
            if (root == null || depthImage == null || shadow == null)
            {
                return;
            }

            float gap = desktopLayout ? 7f : 5f;
            float faceSize = cellSize - gap;
            root.anchoredPosition = position;
            if (UseIsometricArena)
            {
                faceSize = cellSize + 1f;
                depthImage.rectTransform.sizeDelta = Vector2.one * faceSize;
                depthImage.rectTransform.anchoredPosition = new Vector2(0f, -faceSize * 0.12f);
                shadow.sizeDelta = new Vector2(faceSize * 0.88f, faceSize * 0.42f);
                shadow.anchoredPosition = new Vector2(4f, -faceSize * 0.36f);
                return;
            }

            if (golemEdgeTileSprite != null)
            {
                float sideHeight = (faceSize + 2f) * 0.55f;
                float sideCenterY = -faceSize * 0.5f - sideHeight * 0.5f + 3f;
                depthImage.rectTransform.sizeDelta = new Vector2(faceSize + 2f, sideHeight);
                depthImage.rectTransform.anchoredPosition = new Vector2(1f, sideCenterY);
                shadow.sizeDelta = new Vector2(faceSize + 10f, sideHeight + 10f);
                shadow.anchoredPosition = new Vector2(5f, sideCenterY - 5f);
            }
            else
            {
                float depthOffset = desktopLayout ? 8f : 6f;
                depthImage.rectTransform.sizeDelta = Vector2.one * (faceSize + 2f);
                depthImage.rectTransform.anchoredPosition = new Vector2(1f, -depthOffset);
                shadow.sizeDelta = Vector2.one * (faceSize + 10f);
                shadow.anchoredPosition = new Vector2(5f, -depthOffset - 5f);
            }
        }

        private void SetTileDepthColor(int x, int y, Color topColor)
        {
            Image depthImage = mainTileDepthImages[x, y];
            if (depthImage == null)
            {
                return;
            }

            Color opaqueTop = new Color(topColor.r, topColor.g, topColor.b, 1f);
            Color depthColor = Color.Lerp(opaqueTop, new Color(0.025f, 0.02f, 0.015f, 1f), 0.68f);
            depthColor.a = 0.98f;
            depthImage.color = depthColor;
        }

        private void GenerateSpecialTiles()
        {
            // Battle items were removed. An empty collection disables their
            // visuals, pickup effects, and interaction descriptions.
            specialTiles.Clear();
        }

        private void UpdatePhaseLabel()
        {
            shapeText.text = phaseTwoActive ? "PHASE 2 · ESC SKIP" : "PHASE 1 · ESC SKIP";
            shapeText.color = phaseTwoActive ? Danger : Muted;
        }

        private void SetupFixedCrystals()
        {
            crystalLayoutVersion++;
            crystalsRelocated = false;
            crystalWarningCounts.Clear();
            crystalFiringCounts.Clear();
            crystalTelegraphProgress.Clear();
            crystalCells.Clear();
            var center = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);
            List<Vector2Int> fixedLayout = CrystalRules.CreateCardinalLayout(model.Walkable, center);
            foreach (Vector2Int original in fixedLayout)
            {
                Vector2Int cell = original;
                if (cell == model.Player)
                {
                    Vector2Int inward = new Vector2Int(
                        cell.x == center.x ? 0 : cell.x > center.x ? -1 : 1,
                        cell.y == center.y ? 0 : cell.y > center.y ? -1 : 1);
                    Vector2Int adjusted = cell + inward;
                    if (model.IsWalkable(adjusted) && adjusted != model.Player && !crystalCells.Contains(adjusted))
                    {
                        cell = adjusted;
                    }
                }
                if (!crystalCells.Contains(cell))
                {
                    crystalCells.Add(cell);
                }
            }
            ApplyCrystalLayout(true);
        }

        private void RelocateCrystals()
        {
            crystalLayoutVersion++;
            crystalsRelocated = true;
            crystalWarningCounts.Clear();
            crystalFiringCounts.Clear();
            crystalTelegraphProgress.Clear();
            var excluded = new HashSet<Vector2Int> { model.Player, model.Start, model.End };
            foreach (Vector2Int oldCrystal in crystalCells)
            {
                excluded.Add(oldCrystal);
            }
            foreach (Vector2Int trailCell in model.Trail)
            {
                excluded.Add(trailCell);
            }
            List<Vector2Int> randomLayout = CrystalRules.CreateRandomLayout(
                model.Walkable, excluded, System.Environment.TickCount ^ bossHealth ^ round);
            crystalCells.Clear();
            crystalCells.AddRange(randomLayout);
            ApplyCrystalLayout(false);
            statusText.text = "수정 폭주 — 위치 변경! 두 수정은 5초, 두 수정은 4초 주기로 공격합니다";
            PlaySfx(phaseTwoSfx, 1.35f);
            StartCoroutine(ShakeField(22f, 0.32f));
            StartCoroutine(FlashFrame(GetSpecialTileColor(SpecialTileType.Spike)));
        }

        private void ApplyCrystalLayout(bool regenerateRound)
        {
            model.SetBlockedCells(crystalCells);
            if (regenerateRound || model.IsBlocked(model.Start) || model.IsBlocked(model.End))
            {
                model.BeginRound(round, false);
            }
            GenerateSpecialTiles();
            for (int i = 0; i < crystalAttackTimers.Length; i++)
            {
                float interval = GetCrystalInterval(i);
                crystalAttackTimers[i] = interval + i * 0.22f;
            }
            RefreshCrystalVisuals();
            RefreshBoard();
        }

        private float GetCrystalInterval(int index)
        {
            return CrystalRules.AttackIntervalSeconds(crystalsRelocated, index);
        }

        private IEnumerator CrystalPatternLoop()
        {
            while (true)
            {
                if (phaseTwoActive && !playerDead && !gameCleared && crystalCells.Count > 0)
                {
                    for (int i = 0; i < crystalCells.Count && i < crystalAttackTimers.Length; i++)
                    {
                        crystalAttackTimers[i] -= Time.deltaTime;
                        if (crystalAttackTimers[i] <= 0f)
                        {
                            crystalAttackTimers[i] = GetCrystalInterval(i);
                            StartCoroutine(FireCrystalBlast(i, crystalLayoutVersion));
                        }
                    }
                }
                yield return null;
            }
        }

        private IEnumerator FireCrystalBlast(int crystalIndex, int layoutVersion)
        {
            if (crystalIndex < 0 || crystalIndex >= crystalCells.Count || layoutVersion != crystalLayoutVersion)
            {
                yield break;
            }

            Vector2Int origin = crystalCells[crystalIndex];
            HashSet<Vector2Int> blast = CrystalRules.CreateCheckerBlast(model.Traversable, origin);
            EnsureCrystalEscape(blast);
            int telegraphId = ++crystalTelegraphSequence;
            AddCellCounts(crystalWarningCounts, blast);
            SetCrystalTelegraphProgress(blast, telegraphId, 0f);
            statusText.text = "수정 격자 폭발 예고 — 0.7초 안에 피하세요";
            PlaySfx(warningSfx, 1.55f, 0.72f);
            RefreshBoard();

            float elapsed = 0f;
            while (elapsed < CrystalRules.WarningSeconds)
            {
                if (!phaseTwoActive || gameCleared || playerDead || layoutVersion != crystalLayoutVersion)
                {
                    RemoveCellCounts(crystalWarningCounts, blast);
                    RemoveCrystalTelegraphProgress(blast, telegraphId);
                    RefreshBoard();
                    yield break;
                }

                elapsed = Mathf.Min(CrystalRules.WarningSeconds, elapsed + Time.deltaTime);
                SetCrystalTelegraphProgress(blast, telegraphId,
                    BossPatternRules.TelegraphProgress(elapsed, CrystalRules.WarningSeconds));
                if (elapsed >= CrystalRules.WarningSeconds)
                {
                    break;
                }
                yield return null;
            }

            if (!phaseTwoActive || gameCleared || playerDead || layoutVersion != crystalLayoutVersion)
            {
                RemoveCellCounts(crystalWarningCounts, blast);
                RemoveCrystalTelegraphProgress(blast, telegraphId);
                RefreshBoard();
                yield break;
            }

            AnimateAttackWarnings();
            AddCellCounts(crystalFiringCounts, blast);
            var impactCells = new HashSet<Vector2Int>(blast);
            Vector2Int playerAtImpact = model.Player;
            int burstIndex = 0;
            foreach (Vector2Int cell in blast)
            {
                SpawnBurst(cell, Hex("FF6A3D"), 4);
                if (burstIndex++ % 4 == 0)
                {
                    SpawnDirtLaneEruption(cell, 0f);
                }
            }
            PlaySfx(explosionSfx, 1.18f, 0.82f);
            StartCoroutine(ShakeField(12f, 0.18f));
            RefreshBoard();
            yield return new WaitForSeconds(CombatBalanceRules.ExplosionCoyoteSeconds);

            RemoveCellCounts(crystalFiringCounts, blast);
            RemoveCellCounts(crystalWarningCounts, blast);
            RemoveCrystalTelegraphProgress(blast, telegraphId);
            RefreshBoard();
            if (!playerDead && CombatBalanceRules.ShouldApplyExplosionDamage(
                    impactCells, playerAtImpact, model.Player))
            {
                yield return StartCoroutine(KillPlayer("수정 격자 폭발"));
            }
        }

        private void EnsureCrystalEscape(HashSet<Vector2Int> blast)
        {
            var existingDanger = new HashSet<Vector2Int>(warnedCells);
            foreach (Vector2Int cell in crystalWarningCounts.Keys)
            {
                existingDanger.Add(cell);
            }
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            foreach (Vector2Int direction in directions)
            {
                Vector2Int escape = model.Player + direction;
                if (model.IsTraversable(escape) && !existingDanger.Contains(escape))
                {
                    blast.Remove(escape);
                    return;
                }
            }
        }

        private static void AddCellCounts(Dictionary<Vector2Int, int> counts, IEnumerable<Vector2Int> cells)
        {
            foreach (Vector2Int cell in cells)
            {
                counts.TryGetValue(cell, out int count);
                counts[cell] = count + 1;
            }
        }

        private static void RemoveCellCounts(Dictionary<Vector2Int, int> counts, IEnumerable<Vector2Int> cells)
        {
            foreach (Vector2Int cell in cells)
            {
                if (!counts.TryGetValue(cell, out int count))
                {
                    continue;
                }
                if (count <= 1)
                {
                    counts.Remove(cell);
                }
                else
                {
                    counts[cell] = count - 1;
                }
            }
        }

        private void SetCrystalTelegraphProgress(IEnumerable<Vector2Int> cells, int telegraphId, float progress)
        {
            foreach (Vector2Int cell in cells)
            {
                if (!crystalTelegraphProgress.TryGetValue(cell, out Dictionary<int, float> activeTelegraphs))
                {
                    activeTelegraphs = new Dictionary<int, float>();
                    crystalTelegraphProgress[cell] = activeTelegraphs;
                }
                activeTelegraphs[telegraphId] = Mathf.Clamp01(progress);
            }
        }

        private void RemoveCrystalTelegraphProgress(IEnumerable<Vector2Int> cells, int telegraphId)
        {
            foreach (Vector2Int cell in cells)
            {
                if (!crystalTelegraphProgress.TryGetValue(cell, out Dictionary<int, float> activeTelegraphs))
                {
                    continue;
                }
                activeTelegraphs.Remove(telegraphId);
                if (activeTelegraphs.Count == 0)
                {
                    crystalTelegraphProgress.Remove(cell);
                }
            }
        }

        private float GetCrystalTelegraphProgress(Vector2Int cell)
        {
            if (!crystalTelegraphProgress.TryGetValue(cell, out Dictionary<int, float> activeTelegraphs))
            {
                return 0f;
            }

            float progress = 0f;
            foreach (float activeProgress in activeTelegraphs.Values)
            {
                progress = Mathf.Max(progress, activeProgress);
            }
            return progress;
        }

        private void RefreshCrystalVisuals()
        {
            for (int i = 0; i < crystalVisuals.Length; i++)
            {
                RectTransform visual = crystalVisuals[i];
                if (visual == null)
                {
                    continue;
                }
                bool active = phaseTwoActive && i < crystalCells.Count;
                visual.gameObject.SetActive(active);
                if (active)
                {
                    visual.anchoredPosition = GridPosition(crystalCells[i].x, crystalCells[i].y, mainCellSize);
                    visual.SetAsLastSibling();
                }
            }
            if (mainPlayer != null)
            {
                mainPlayer.SetAsLastSibling();
            }
        }

        private IEnumerator BossPatternLoop()
        {
            while (titleActive || tutorialActive || hubActive)
            {
                yield return null;
            }
            yield return StartCoroutine(WaitGameplaySeconds(2.4f));
            while (!gameCleared)
            {
                bossAttackCount++;
                UpdatePhaseLabel();
                yield return StartCoroutine(GlyphPattern());

                if (!gameCleared)
                {
                    float interval = BossPatternRules.PatternIntervalSeconds(phaseTwoActive, bossAttackCount);
                    yield return StartCoroutine(WaitGameplaySeconds(interval));
                }
            }
        }

        private IEnumerator EnterPhaseTwo()
        {
            phaseBanner.text = "PHASE 2\nENRAGED";
            phaseBannerGroup.alpha = 1f;
            phaseOverlayRoot.SetAsLastSibling();
            phasePageLeft.localScale = new Vector3(0f, 1f, 1f);
            phasePageRight.localScale = new Vector3(0f, 1f, 1f);
            phaseBanner.rectTransform.localScale = Vector3.one * 0.72f;
            phaseBanner.color = new Color(Danger.r, Danger.g, Danger.b, 0f);
            PlaySfx(phaseTwoSfx);

            const float closeSeconds = 0.42f;
            for (float elapsed = 0f; elapsed < closeSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / closeSeconds);
                phasePageLeft.localScale = new Vector3(progress, 1f, 1f);
                phasePageRight.localScale = new Vector3(progress, 1f, 1f);
                yield return null;
            }
            phasePageLeft.localScale = Vector3.one;
            phasePageRight.localScale = Vector3.one;

            phaseTwoActive = true;
            SetupFixedCrystals();
            patternVersion++;
            glyphPatternIndex = 0;
            bossAttackCount = 0;
            bossMaxHealth = BossPatternRules.PhaseMaxHealth(true);
            bossHealth = bossMaxHealth;
            bossHealthFill.fillAmount = 1f;
            bossHealthText.text = bossHealth + " / " + bossMaxHealth;
            warnedCells.Clear();
            targetedCells.Clear();
            hazardFiring = false;
            targetedFiring = false;
            hazardTelegraphProgress = 0f;
            targetedTelegraphProgress = 0f;
            UpdatePhaseLabel();
            RefreshBoard();

            statusText.text = "PHASE 2 — 공격 수정 4개와 격자 문양이 활성화됩니다";
            StartCoroutine(ShakeHud());
            StartCoroutine(FlashFrame(Danger));

            const float revealSeconds = 0.48f;
            for (float elapsed = 0f; elapsed < revealSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / revealSeconds);
                phaseBanner.color = new Color(Danger.r, Danger.g, Danger.b, progress);
                phaseBanner.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, progress);
                yield return null;
            }
            phaseBanner.color = Danger;
            phaseBanner.rectTransform.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(0.32f);

            const float openSeconds = 0.50f;
            for (float elapsed = 0f; elapsed < openSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / openSeconds);
                float pageScale = 1f - progress;
                phasePageLeft.localScale = new Vector3(pageScale, 1f, 1f);
                phasePageRight.localScale = new Vector3(pageScale, 1f, 1f);
                phaseBanner.color = new Color(Danger.r, Danger.g, Danger.b, 1f - progress);
                yield return null;
            }

            phaseBannerGroup.alpha = 0f;
            phasePageLeft.localScale = new Vector3(0f, 1f, 1f);
            phasePageRight.localScale = new Vector3(0f, 1f, 1f);
            phaseBanner.color = Danger;
            phaseBanner.rectTransform.localScale = Vector3.one;
        }

        private IEnumerator GlyphPattern()
        {
            int version = patternVersion;
            int sequenceLength = phaseTwoActive ? 6 : 3;
            int pattern = glyphPatternIndex % sequenceLength;
            glyphPatternIndex++;
            var center = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);
            HashSet<Vector2Int> glyph;
            string patternName;

            switch (pattern)
            {
                case 1:
                    int distance = TrailFieldModel.ScaleLegacyDistance(diamondUseCount % 2 == 0 ? 3 : 5);
                    diamondUseCount++;
                    glyph = BossPatternRules.CreateDiamondGlyph(model.Traversable, center, distance);
                    patternName = "마름모 문양 " + distance;
                    break;
                case 2:
                    glyph = BossPatternRules.CreateDiagonalGlyph(model.Traversable, center);
                    patternName = "X 문양";
                    break;
                case 3:
                    glyph = BossPatternRules.CreateCombinedGlyph(model.Traversable, center,
                        TrailFieldModel.ScaleLegacyDistance(3));
                    patternName = "이중 문양";
                    break;
                case 4:
                    glyph = BossPatternRules.CreateHorizontalGrid(model.Traversable, center);
                    patternName = "전체 가로 격자";
                    break;
                case 5:
                    glyph = BossPatternRules.CreateVerticalGrid(model.Traversable, center);
                    patternName = "전체 세로 격자";
                    break;
                default:
                    glyph = BossPatternRules.CreateCrossGlyph(model.Traversable, center);
                    patternName = "십자 문양";
                    break;
            }

            warnedCells.Clear();
            warnedCells.UnionWith(BossPatternRules.EnsureEscapeRoute(model.Traversable, model.Player, glyph));
            yield return StartCoroutine(RunGlyphTelegraph(patternName, version));
        }

        private IEnumerator RunGlyphTelegraph(string patternName, int version)
        {
            float warningDuration = BossPatternRules.TelegraphSeconds(phaseTwoActive);
            float remaining = warningDuration;
            float elapsed = 0f;
            float targetRemaining = 0f;
            bool targetAttempted = false;
            bool targetActive = false;
            hazardFiring = false;
            targetedFiring = false;
            hazardTelegraphProgress = 0f;
            targetedTelegraphProgress = 0f;
            targetedCells.Clear();
            PlaySfx(warningSfx, warningDuration <= 1f ? 1.35f : 1f);
            RefreshBoard();

            while (remaining > 0f && !gameCleared && version == patternVersion)
            {
                if (!inputLocked && !playerDead)
                {
                    remaining -= Time.deltaTime;
                    elapsed += Time.deltaTime;
                    hazardTelegraphProgress = BossPatternRules.TelegraphProgress(elapsed, warningDuration);

                    if (phaseTwoActive && !targetAttempted && elapsed >= 0.15f)
                    {
                        targetAttempted = true;
                        if (BossPatternRules.HasAdjacentSafeCell(model.Traversable, model.Player, warnedCells))
                        {
                            targetedCells.Add(model.Player);
                            targetRemaining = TargetedWarningSeconds;
                            targetedTelegraphProgress = 0f;
                            targetActive = true;
                            PlaySfx(targetLockSfx);
                            RefreshBoard();
                        }
                    }

                    if (targetActive)
                    {
                        targetRemaining -= Time.deltaTime;
                        targetedTelegraphProgress = BossPatternRules.TelegraphProgress(
                            TargetedWarningSeconds - Mathf.Max(0f, targetRemaining), TargetedWarningSeconds);
                        statusText.text = "◉ 위치 추적 폭발  " + Mathf.Max(0f, targetRemaining).ToString("0.0") + "초";
                        if (targetRemaining <= 0f)
                        {
                            targetActive = false;
                            yield return StartCoroutine(FireTargetedShot(version));
                        }
                    }
                    else
                    {
                        statusText.text = "⚠ " + patternName + " 예고  " + Mathf.Max(0f, remaining).ToString("0.0") + "초";
                    }
                }
                if (remaining <= 0f)
                {
                    break;
                }
                yield return null;
            }

            if (gameCleared || version != patternVersion)
            {
                yield break;
            }

            hazardTelegraphProgress = 1f;
            AnimateAttackWarnings();
            hazardFiring = true;
            var impactCells = new HashSet<Vector2Int>(warnedCells);
            Vector2Int playerAtImpact = model.Player;
            var impactCenter = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);
            foreach (Vector2Int cell in warnedCells)
            {
                float dirtDelay = (Mathf.Abs(cell.x - impactCenter.x) + Mathf.Abs(cell.y - impactCenter.y)) * 0.006f;
                SpawnDirtLaneEruption(cell, dirtDelay);
                SpawnBurst(cell, Danger, 1);
            }
            PlaySfx(laserSfx, phaseTwoActive ? 1.15f : 0.92f);
            RefreshBoard();
            yield return StartCoroutine(FlashFrame(Danger));
            yield return new WaitForSeconds(Mathf.Max(0f,
                CombatBalanceRules.ExplosionCoyoteSeconds - 0.12f));

            hazardFiring = false;
            warnedCells.Clear();
            hazardTelegraphProgress = 0f;
            RefreshBoard();

            if (CombatBalanceRules.ShouldApplyExplosionDamage(
                    impactCells, playerAtImpact, model.Player))
            {
                yield return StartCoroutine(KillPlayer(patternName));
            }
            else
            {
                statusText.text = "회피 성공! 공격 경로를 계속 연결하세요";
                PlaySfx(startSfx, 1.35f, 0.7f);
            }
        }

        private IEnumerator FireTargetedShot(int version)
        {
            if (targetedCells.Count == 0 || version != patternVersion)
            {
                yield break;
            }

            targetedTelegraphProgress = 1f;
            AnimateAttackWarnings();
            targetedFiring = true;
            var impactCells = new HashSet<Vector2Int>(targetedCells);
            Vector2Int playerAtImpact = model.Player;
            Color targetColor = Hex("B44CFF");
            foreach (Vector2Int cell in targetedCells)
            {
                SpawnDirtAreaExplosion(cell);
                SpawnBurst(cell, targetColor, 6);
            }
            StartCoroutine(ShakeField(18f, 0.20f));
            PlaySfx(explosionSfx, 1.3f, 0.75f);
            RefreshBoard();
            yield return StartCoroutine(FlashFrame(targetColor));
            yield return new WaitForSeconds(Mathf.Max(0f,
                CombatBalanceRules.ExplosionCoyoteSeconds - 0.12f));

            targetedFiring = false;
            targetedCells.Clear();
            targetedTelegraphProgress = 0f;
            RefreshBoard();

            if (CombatBalanceRules.ShouldApplyExplosionDamage(
                    impactCells, playerAtImpact, model.Player))
            {
                yield return StartCoroutine(KillPlayer("위치 추적 폭발"));
            }
            else
            {
                statusText.text = "견제 회피 — 문양 공격을 계속 피하세요";
            }
        }

        private IEnumerator KillPlayer(string patternName)
        {
            playerDead = true;
            inputLocked = true;
            movementFrozen = false;
            movementFreezeVersion++;
            patternVersion++;
            warnedCells.Clear();
            targetedCells.Clear();
            hazardFiring = false;
            targetedFiring = false;
            hazardTelegraphProgress = 0f;
            targetedTelegraphProgress = 0f;
            crystalWarningCounts.Clear();
            crystalFiringCounts.Clear();
            crystalTelegraphProgress.Clear();
            playerHealthText.text = "♥  HP 0";
            playerHealthText.color = Danger;
            statusText.text = patternName + " 피격 — 플레이어 사망";
            SpawnBurst(model.Player, Danger, 30);
            PlaySfx(deathSfx);
            RefreshBoard();

            Vector3 baseScale = mainPlayer.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime * 2.8f)
            {
                mainPlayer.localRotation = Quaternion.Euler(0f, 0f, t * 360f);
                mainPlayer.localScale = baseScale * Mathf.Max(0.05f, 1f - t);
                mainPlayerImage.color = Color.Lerp(White, Danger, t);
                yield return null;
            }

            yield return new WaitForSeconds(0.65f);
            mainPlayer.localRotation = Quaternion.identity;
            mainPlayer.localScale = Vector3.one;
            mainPlayerImage.color = White;
            playerHealthText.color = StartColor;
            StartStage(0);
        }

        private IEnumerator WaitGameplaySeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds && !gameCleared)
            {
                if (!inputLocked && !playerDead)
                {
                    elapsed += Time.deltaTime;
                }
                yield return null;
            }
        }

        private int CalculateDamage(IReadOnlyCollection<Vector2Int> trailCells)
        {
            return CombatBalanceRules.CalculateTrailDamage(trailCells, TrailFieldModel.MaxSize);
        }

        private void BuildInterface()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.gameObject.SetActive(false);
            }

            GameObject canvasObject = new GameObject(desktopLayout ? "Desktop UI" : "Portrait UI",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            GameObject cameraObject = new GameObject("UI Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            uiCamera = cameraObject.GetComponent<Camera>();
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = desktopLayout ? ArenaVoid : Background;
            uiCamera.orthographic = true;
            uiCamera.nearClipPlane = 0.1f;
            uiCamera.farClipPlane = 100f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 10f;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = desktopLayout
                ? new Vector2(DesktopReferenceWidth, DesktopReferenceHeight)
                : new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventObject.transform.SetParent(transform, false);
            }

            RectTransform root = CreateRect("Safe Area", canvasObject.transform);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            ApplySafeArea(root);
            CreateImage("Background", root, desktopLayout ? ArenaVoid : Background,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            BuildHeader(root);
            BuildMainField(root);
            BuildFooterV2(root);
            if (desktopLayout)
            {
                BuildDesktopMinimap(root);
            }
            BuildPhaseOverlay(root);
            BuildTitleScreen(root);
        }

        private void BuildTitleScreen(RectTransform root)
        {
            titleScreen = CreateRect("Title Screen", root);
            titleScreen.anchorMin = Vector2.zero;
            titleScreen.anchorMax = Vector2.one;
            titleScreen.offsetMin = Vector2.zero;
            titleScreen.offsetMax = Vector2.zero;

            RectTransform blind = CreateImage("Circular Blind", titleScreen, White,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            titleBlindImage = blind.GetComponent<Image>();
            titleBlindImage.raycastTarget = false;
            Shader blindShader = Resources.Load<Shader>("Shaders/TraceStrikeTitleBlind");
            if (blindShader == null)
            {
                blindShader = Shader.Find("UI/TraceStrikeTitleBlind");
            }
            if (blindShader != null)
            {
                titleBlindMaterial = new Material(blindShader);
                titleBlindMaterial.SetColor("_Color", Color.black);
                titleBlindMaterial.SetColor("_RingColor", new Color(Trail.r, Trail.g, Trail.b, 0.92f));
                titleBlindMaterial.SetFloat("_Radius", TitleInitialRadius);
                titleBlindMaterial.SetFloat("_Feather", 0.006f);
                titleBlindMaterial.SetFloat("_RingWidth", 0.008f);
                titleBlindImage.material = titleBlindMaterial;
            }
            else
            {
                titleBlindImage.color = new Color(0f, 0f, 0f, 0.94f);
                Debug.LogError("Trace Strike title blind shader could not be loaded.");
            }

            RectTransform foreground = CreateRect("Title Foreground", titleScreen);
            foreground.anchorMin = Vector2.zero;
            foreground.anchorMax = Vector2.one;
            foreground.offsetMin = Vector2.zero;
            foreground.offsetMax = Vector2.zero;
            titleForegroundGroup = foreground.gameObject.AddComponent<CanvasGroup>();
            titleForegroundGroup.blocksRaycasts = false;
            titleForegroundGroup.interactable = false;

            CreateText("Game Title", foreground, "TRACE STRIKE", desktopLayout ? 78 : 66,
                FontStyle.Bold, White, new Vector2(0.10f, 0.815f), new Vector2(0.90f, 0.955f),
                TextAnchor.MiddleCenter);
            CreateImage("Title Accent", foreground, Trail,
                new Vector2(0.39f, 0.81f), new Vector2(0.61f, 0.814f), Vector2.zero, Vector2.zero);

            RectTransform hintPanel = CreatePanel("Start Input Guide", foreground,
                new Color(Panel.r, Panel.g, Panel.b, 0.88f),
                new Vector2(0.355f, 0.045f), new Vector2(0.645f, 0.135f));
            Outline hintOutline = hintPanel.gameObject.AddComponent<Outline>();
            hintOutline.effectColor = Trail;
            hintOutline.effectDistance = new Vector2(2f, -2f);
            titleInputHint = CreateText("WASD Start Hint", hintPanel, "W / A / S / D   TO PLAY",
                desktopLayout ? 25 : 21, FontStyle.Bold, White,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            titleScreen.gameObject.SetActive(false);
        }

        private void BuildTitleSpecialGuide(RectTransform parent, string name, SpecialTileType type,
            string title, string copy, Vector2 min, Vector2 max)
        {
            RectTransform card = CreatePanel(name, parent, Hex("111A2D"), min, max);
            Color accent = GetSpecialTileColor(type);
            AddAccent(card, Vector2.zero, new Vector2(0.018f, 1f), accent);

            RectTransform diamond = CreateRect(name + " Diamond", card);
            diamond.anchorMin = diamond.anchorMax = new Vector2(0.17f, 0.5f);
            diamond.sizeDelta = Vector2.one * 72f;
            diamond.anchoredPosition = Vector2.zero;
            diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image diamondImage = diamond.gameObject.AddComponent<Image>();
            diamondImage.color = accent;
            diamondImage.raycastTarget = false;
            Outline diamondOutline = diamond.gameObject.AddComponent<Outline>();
            diamondOutline.effectColor = White;
            diamondOutline.effectDistance = new Vector2(3f, -3f);

            RectTransform icon = CreateRect(name + " Icon", diamond);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = Vector2.one * 52f;
            icon.anchoredPosition = Vector2.zero;
            icon.localRotation = Quaternion.Euler(0f, 0f, -45f);
            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = GetSpecialTileIcon(type);
            iconImage.color = White;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            CreateText(name + " Title", card, title, 23, FontStyle.Bold, accent,
                new Vector2(0.32f, 0.51f), new Vector2(0.96f, 0.90f), TextAnchor.MiddleLeft);
            CreateText(name + " Copy", card, copy, 18, FontStyle.Normal, White,
                new Vector2(0.32f, 0.10f), new Vector2(0.96f, 0.52f), TextAnchor.MiddleLeft);
        }

        private void BuildPhaseOverlay(RectTransform root)
        {
            RectTransform overlay = CreatePanel("Phase Page Transition", root, Color.clear,
                Vector2.zero, Vector2.one);
            phaseOverlayRoot = overlay;

            phasePageLeft = CreatePanel("Phase Page Left", overlay, Hex("171611"),
                Vector2.zero, new Vector2(0.5f, 1f));
            phasePageLeft.pivot = new Vector2(0f, 0.5f);
            phasePageRight = CreatePanel("Phase Page Right", overlay, Hex("171611"),
                new Vector2(0.5f, 0f), Vector2.one);
            phasePageRight.pivot = new Vector2(1f, 0.5f);
            AddAccent(phasePageLeft, new Vector2(0.982f, 0f), Vector2.one, Danger);
            AddAccent(phasePageRight, Vector2.zero, new Vector2(0.018f, 1f), Danger);
            CreateText("Phase Page Left Mark", phasePageLeft, "01", 54, FontStyle.Bold,
                new Color(Danger.r, Danger.g, Danger.b, 0.28f),
                new Vector2(0.08f, 0.08f), new Vector2(0.40f, 0.30f), TextAnchor.MiddleLeft);
            CreateText("Phase Page Right Mark", phasePageRight, "02", 54, FontStyle.Bold,
                new Color(Danger.r, Danger.g, Danger.b, 0.28f),
                new Vector2(0.60f, 0.70f), new Vector2(0.92f, 0.92f), TextAnchor.MiddleRight);

            phaseBanner = CreateText("Phase Banner Text", overlay, "PHASE 2\nENRAGED", 58, FontStyle.Bold, Danger,
                new Vector2(0.22f, 0.34f), new Vector2(0.78f, 0.66f), TextAnchor.MiddleCenter);
            phaseBannerGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            phaseBannerGroup.alpha = 0f;
            phaseBannerGroup.blocksRaycasts = false;
            phasePageLeft.localScale = new Vector3(0f, 1f, 1f);
            phasePageRight.localScale = new Vector3(0f, 1f, 1f);
        }

        private void BuildHeader(RectTransform root)
        {
            if (desktopLayout)
            {
                BuildDesktopHeader(root);
                return;
            }

            RectTransform header = CreatePanel("Boss HUD", root, Panel, new Vector2(0.035f, 0.855f), new Vector2(0.965f, 0.985f));
            bossHud = header;
            AddAccent(header, new Vector2(0f, 0f), new Vector2(0.018f, 1f), Trail);

            stageText = CreateText("Stage", header, "STAGE 01", 30, FontStyle.Bold, White,
                new Vector2(0.035f, 0.72f), new Vector2(0.32f, 0.96f), TextAnchor.MiddleLeft);
            playerHealthText = CreateText("Player HP", header, "♥  HP 1", 26, FontStyle.Bold, StartColor,
                new Vector2(0.32f, 0.72f), new Vector2(0.62f, 0.96f), TextAnchor.MiddleCenter);
            shapeText = CreateText("Shape", header, "원형 필드", 24, FontStyle.Normal, Muted,
                new Vector2(0.60f, 0.74f), new Vector2(0.96f, 0.94f), TextAnchor.MiddleRight);
            bossNameText = CreateText("Boss Name", header, "크림슨 골렘", 42, FontStyle.Bold, White,
                new Vector2(0.035f, 0.39f), new Vector2(0.72f, 0.74f), TextAnchor.MiddleLeft);

            RectTransform healthBack = CreatePanel("Boss Health Back", header, Hex("0A0F1C"),
                new Vector2(0.035f, 0.15f), new Vector2(0.965f, 0.36f));
            RectTransform fillRect = CreateRect("Boss Health", healthBack);
            fillRect.anchorMin = new Vector2(0.01f, 0.12f);
            fillRect.anchorMax = new Vector2(0.99f, 0.88f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            bossHealthFill = fillRect.gameObject.AddComponent<Image>();
            bossHealthFill.color = Danger;
            bossHealthFill.type = Image.Type.Filled;
            bossHealthFill.fillMethod = Image.FillMethod.Horizontal;
            bossHealthFill.fillOrigin = 0;

            bossHealthText = CreateText("Health Text", healthBack, "50 / 50", 24, FontStyle.Bold, White,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            damagePopup = CreateText("Damage Popup", header, "-0", 64, FontStyle.Bold, TrailHot,
                new Vector2(0.66f, 0.28f), new Vector2(0.96f, 0.75f), TextAnchor.MiddleRight);
            damagePopupGroup = damagePopup.gameObject.AddComponent<CanvasGroup>();
            damagePopupGroup.alpha = 0f;

        }

        private void BuildDesktopHeader(RectTransform root)
        {
            RectTransform header = CreatePanel("Boss HUD", root, Panel,
                new Vector2(0.30f, 0.94f), new Vector2(0.70f, 0.993f));
            bossHud = header;
            Outline headerOutline = header.gameObject.AddComponent<Outline>();
            headerOutline.effectColor = Danger;
            headerOutline.effectDistance = new Vector2(3f, -3f);
            AddAccent(header, Vector2.zero, new Vector2(1f, 0.07f), Danger);

            stageText = CreateText("Stage", header, "STAGE 01", 16, FontStyle.Bold, White,
                new Vector2(0.025f, 0.53f), new Vector2(0.22f, 0.96f), TextAnchor.MiddleLeft);
            bossNameText = CreateText("Boss Name", header, "크림슨 골렘", 24, FontStyle.Bold, White,
                new Vector2(0.20f, 0.50f), new Vector2(0.74f, 0.98f), TextAnchor.MiddleCenter);
            shapeText = CreateText("Shape", header, "PHASE 1", 15, FontStyle.Bold, Muted,
                new Vector2(0.72f, 0.53f), new Vector2(0.975f, 0.96f), TextAnchor.MiddleRight);

            RectTransform healthBack = CreatePanel("Boss Health Back", header, Hex("0A0F1C"),
                new Vector2(0.025f, 0.10f), new Vector2(0.975f, 0.45f));
            RectTransform fillRect = CreateRect("Boss Health", healthBack);
            fillRect.anchorMin = new Vector2(0.008f, 0.12f);
            fillRect.anchorMax = new Vector2(0.992f, 0.88f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            bossHealthFill = fillRect.gameObject.AddComponent<Image>();
            bossHealthFill.color = Danger;
            bossHealthFill.type = Image.Type.Filled;
            bossHealthFill.fillMethod = Image.FillMethod.Horizontal;
            bossHealthFill.fillOrigin = 0;
            bossHealthText = CreateText("Health Text", healthBack, "50 / 50", 15, FontStyle.Bold, White,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            RectTransform interactionPanel = CreatePanel("Interaction Information", root, Hex("211A1B"),
                new Vector2(0.29f, 0.705f), new Vector2(0.72f, 0.925f));
            interactionPanelRoot = interactionPanel;
            Outline interactionOutline = interactionPanel.gameObject.AddComponent<Outline>();
            interactionOutline.effectColor = Hex("B97A56");
            interactionOutline.effectDistance = new Vector2(4f, -4f);
            AddAccent(interactionPanel, new Vector2(0f, 0.965f), Vector2.one, Hex("D09468"));
            CreateText("Interaction Label", interactionPanel, "FRONT OBJECT · INTERACTION SCAN", 16,
                FontStyle.Bold, Hex("E2A77E"), new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.97f),
                TextAnchor.MiddleCenter);

            RectTransform portrait = CreateRect("Interaction Portrait", interactionPanel);
            portrait.anchorMin = portrait.anchorMax = new Vector2(0.5f, 0.61f);
            portrait.pivot = new Vector2(0.5f, 0.5f);
            portrait.sizeDelta = new Vector2(112f, 112f);
            portrait.anchoredPosition = Vector2.zero;

            Sprite circleSprite = CreateCircleSprite(96);
            RectTransform ring = CreateRect("Interaction Portrait Ring", portrait);
            ring.anchorMin = Vector2.zero;
            ring.anchorMax = Vector2.one;
            ring.offsetMin = Vector2.zero;
            ring.offsetMax = Vector2.zero;
            interactionRingImage = ring.gameObject.AddComponent<Image>();
            interactionRingImage.sprite = circleSprite;
            interactionRingImage.color = Danger;
            interactionRingImage.raycastTarget = false;

            RectTransform inner = CreateRect("Interaction Portrait Inner", portrait);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(7f, 7f);
            inner.offsetMax = new Vector2(-7f, -7f);
            Image innerImage = inner.gameObject.AddComponent<Image>();
            innerImage.sprite = circleSprite;
            innerImage.color = Hex("241523");
            innerImage.raycastTarget = false;

            RectTransform bossIcon = CreateRect("Interaction Object Image", portrait);
            bossIcon.anchorMin = bossIcon.anchorMax = new Vector2(0.5f, 0.5f);
            bossIcon.sizeDelta = new Vector2(78f, 78f);
            bossIcon.anchoredPosition = Vector2.zero;
            interactionIconImage = bossIcon.gameObject.AddComponent<Image>();
            bossPortraitSprite = LoadPixelSprite("Art/red_attack_crystal", 64f);
            interactionIconImage.sprite = bossPortraitSprite;
            interactionIconImage.preserveAspect = true;
            interactionIconImage.raycastTarget = false;

            interactionTitleText = CreateText("Interaction Object Name", interactionPanel,
                "스테이지 보스 · 크림슨 골렘", 21, FontStyle.Bold, White,
                new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.32f), TextAnchor.MiddleCenter);
            interactionBodyText = CreateText("Interaction Object Description", interactionPanel,
                "광물 증식형 바이러스 · 레이저와 수정 폭발 패턴", 16, FontStyle.Normal, Muted,
                new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.18f), TextAnchor.MiddleCenter);

            damagePopup = CreateText("Damage Popup", root, "-0", 52, FontStyle.Bold, TrailHot,
                new Vector2(0.69f, 0.74f), new Vector2(0.79f, 0.86f), TextAnchor.MiddleLeft);
            damagePopupGroup = damagePopup.gameObject.AddComponent<CanvasGroup>();
            damagePopupGroup.alpha = 0f;

            // Desktop gameplay is intentionally HUD-free. The references stay alive so
            // combat state can continue updating without coupling game rules to presentation.
            header.gameObject.SetActive(false);
            interactionPanel.gameObject.SetActive(false);
            damagePopup.gameObject.SetActive(false);
        }

        private void BuildMainField(RectTransform root)
        {
            RectTransform section = CreateRect("Main Field Section", root);
            section.anchorMin = desktopLayout ? Vector2.zero : new Vector2(0.035f, 0.245f);
            section.anchorMax = desktopLayout ? Vector2.one : new Vector2(0.965f, 0.835f);
            section.offsetMin = Vector2.zero;
            section.offsetMax = Vector2.zero;

            RectTransform titleBar = CreatePanel("Field Title", section, PanelLight,
                new Vector2(0f, 0.91f), new Vector2(1f, 1f));
            fieldTitleText = CreateText("Field Label", titleBar, "IVY TEMPLE", 30, FontStyle.Bold, White,
                new Vector2(0.04f, 0f), new Vector2(0.45f, 1f), TextAnchor.MiddleLeft);
            comboText = CreateText("Power", titleBar, "경로 1칸  ·  예상 피해 13", 25, FontStyle.Bold, Trail,
                new Vector2(0.38f, 0f), new Vector2(0.96f, 1f), TextAnchor.MiddleRight);
            if (desktopLayout)
            {
                titleBar.gameObject.SetActive(false);
            }

            RectTransform field = CreatePanel("Field Frame", section, desktopLayout ? ArenaVoid : Hex("0A1020"),
                Vector2.zero, desktopLayout ? Vector2.one : new Vector2(1f, 0.89f));
            fieldFrame = field.GetComponent<Image>();
            Outline outline = field.gameObject.AddComponent<Outline>();
            outline.effectColor = Hex("42D9EA");
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = !desktopLayout;

            // Keep the playfield clean: no full-screen temple artwork or
            // background motes behind the tilemap.

            if (desktopLayout)
            {
                BuildHudlessArenaPresentation(field);
            }

            mainGrid = CreateRect("Main Grid", field);
            mainGrid.anchorMin = new Vector2(0.5f, 0.5f);
            mainGrid.anchorMax = new Vector2(0.5f, 0.5f);
            mainGrid.pivot = new Vector2(0.5f, 0.5f);
            // Enlarge the arena without shrinking its cells to fit the screen.
            float legacyGridSize = desktopLayout ? LegacyDesktopGridSize : LegacyMobileGridSize;
            float gridSize = legacyGridSize * TrailFieldModel.Size / LegacyFieldSize;
            if (desktopLayout)
            {
                gridSize *= BattleCameraZoom;
            }
            mainGrid.sizeDelta = new Vector2(gridSize, gridSize);
            mainGrid.anchoredPosition = Vector2.zero;
            mainCellSize = gridSize / TrailFieldModel.Size;
            LoadGolemTileSprites();
            floorTileSprites = golemBaseTileSprite != null
                ? new[] { golemBaseTileSprite }
                : LoadFloorTileSprites();
            floorTileSprite = floorTileSprites.Length > 0
                ? floorTileSprites[0]
                : LoadPixelSprite("Art/cave_floor_tile_v2", 128f);
            RandomizeFloorTileLayout();
            startMarkerSprite = LoadPixelSprite("Art/start_sword_retouch", 32f);
            endMarkerSprite = LoadPixelSprite("Art/end_flag_retouch", 32f);
            powerIconSprite = LoadPixelSprite("Art/special_plus", 32f);
            amplifyIconSprite = LoadPixelSprite("Art/special_up", 32f);
            mudIconSprite = LoadPixelSprite("Art/special_pause", 32f);
            curseIconSprite = LoadPixelSprite("Art/special_down", 32f);

            // Slightly overlapping plates form one connected gray silhouette
            // behind only the playable cells, separating the arena from the temple.
            float groundExpansion = desktopLayout ? 18f : 14f;
            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    RectTransform ground = CreateRect("Arena Ground " + x + "," + y, mainGrid);
                    ground.anchorMin = ground.anchorMax = new Vector2(0.5f, 0.5f);
                    ground.sizeDelta = Vector2.one * (mainCellSize + groundExpansion);
                    ground.anchoredPosition = GridPosition(x, y, mainCellSize);
                    Image groundImage = ground.gameObject.AddComponent<Image>();
                    groundImage.color = desktopLayout
                        ? new Color(0.115f, 0.132f, 0.092f, 0.96f)
                        : new Color(0.105f, 0.118f, 0.086f, 0.95f);
                    groundImage.raycastTarget = false;
                    ground.gameObject.SetActive(false);
                    arenaGroundTiles[x, y] = groundImage;
                }
            }

            // Draw every extrusion before every top face so neighbouring tiles
            // naturally cover each other's depth and read as a raised 2.5D grid.
            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    RectTransform depthRoot = CreateRect("Tile Depth " + x + "," + y, mainGrid);
                    depthRoot.anchorMin = depthRoot.anchorMax = new Vector2(0.5f, 0.5f);

                    RectTransform dropShadow = CreateRect("Depth Shadow", depthRoot);
                    dropShadow.anchorMin = dropShadow.anchorMax = new Vector2(0.5f, 0.5f);
                    Image shadowImage = dropShadow.gameObject.AddComponent<Image>();
                    shadowImage.color = new Color(0f, 0f, 0f, 0.68f);
                    shadowImage.raycastTarget = false;

                    RectTransform depthFace = CreateRect("Extruded Tile Side", depthRoot);
                    depthFace.anchorMin = depthFace.anchorMax = new Vector2(0.5f, 0.5f);
                    Image depthImage = depthFace.gameObject.AddComponent<Image>();
                    depthImage.sprite = golemEdgeTileSprite != null
                        ? golemEdgeTileSprite
                        : GetFloorTileSprite(x, y);
                    depthImage.type = Image.Type.Simple;
                    depthImage.color = new Color(0.10f, 0.075f, 0.045f, 0.98f);
                    depthImage.raycastTarget = false;
                    Outline depthOutline = depthFace.gameObject.AddComponent<Outline>();
                    depthOutline.effectColor = new Color(0f, 0f, 0f, 0.88f);
                    depthOutline.effectDistance = new Vector2(2f, -2f);

                    mainTileDepthRoots[x, y] = depthRoot;
                    mainTileDepthImages[x, y] = depthImage;
                    mainTileDropShadows[x, y] = dropShadow;
                    LayoutTileDepth(x, y, mainCellSize, GridPosition(x, y, mainCellSize));
                    depthRoot.gameObject.SetActive(false);
                }
            }

            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    RectTransform tile = CreateRect("Tile " + x + "," + y, mainGrid);
                    tile.anchorMin = tile.anchorMax = new Vector2(0.5f, 0.5f);
                    tile.sizeDelta = Vector2.one * (UseIsometricArena
                        ? mainCellSize + 1f
                        : mainCellSize - (desktopLayout ? 7f : 5f));
                    tile.anchoredPosition = GridPosition(x, y, mainCellSize);
                    Image image = tile.gameObject.AddComponent<Image>();
                    image.sprite = GetFloorTileSprite(x, y);
                    image.type = Image.Type.Simple;
                    image.color = Floor;
                    mainTiles[x, y] = image;
                    Outline tileEdge = tile.gameObject.AddComponent<Outline>();
                    tileEdge.effectColor = desktopLayout ? Hex("39251F") : FloorEdge;
                    tileEdge.effectDistance = new Vector2(1.2f, -1.2f);
                    mainTileOutlines[x, y] = tileEdge;

                    if (UseExtraTileDepth && !UseIsometricArena)
                    {
                        CreateImage("Top Bevel", tile, new Color(0.92f, 0.84f, 0.62f, 0.38f),
                            new Vector2(0f, 1f), new Vector2(1f, 1f),
                            new Vector2(3f, -3f), new Vector2(-3f, 0f)).GetComponent<Image>().raycastTarget = false;
                        CreateImage("Left Bevel", tile, new Color(0.78f, 0.72f, 0.52f, 0.28f),
                            new Vector2(0f, 0f), new Vector2(0f, 1f),
                            new Vector2(0f, 3f), new Vector2(3f, -3f)).GetComponent<Image>().raycastTarget = false;
                        CreateImage("Bottom Shade", tile, new Color(0.015f, 0.012f, 0.01f, 0.68f),
                            new Vector2(0f, 0f), new Vector2(1f, 0f),
                            new Vector2(2f, 0f), new Vector2(-2f, 5f)).GetComponent<Image>().raycastTarget = false;
                        CreateImage("Right Shade", tile, new Color(0.015f, 0.012f, 0.01f, 0.58f),
                            new Vector2(1f, 0f), new Vector2(1f, 1f),
                            new Vector2(-5f, 2f), new Vector2(0f, -2f)).GetComponent<Image>().raycastTarget = false;
                    }

                    RectTransform item = CreateRect("Special Item", tile);
                    item.anchorMin = item.anchorMax = new Vector2(0.5f, 0.5f);
                    item.sizeDelta = Vector2.one * (mainCellSize * 0.48f);
                    item.anchoredPosition = Vector2.zero;
                    item.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    Image itemImage = item.gameObject.AddComponent<Image>();
                    itemImage.raycastTarget = false;
                    Outline itemOutline = item.gameObject.AddComponent<Outline>();
                    itemOutline.effectColor = new Color(1f, 1f, 1f, 0.8f);
                    itemOutline.effectDistance = new Vector2(3f, -3f);

                    RectTransform itemIcon = CreateRect("Item Icon", item);
                    itemIcon.anchorMin = itemIcon.anchorMax = new Vector2(0.5f, 0.5f);
                    itemIcon.sizeDelta = Vector2.one * (mainCellSize * 0.34f);
                    itemIcon.anchoredPosition = Vector2.zero;
                    itemIcon.localRotation = Quaternion.Euler(0f, 0f, -45f);
                    Image itemIconImage = itemIcon.gameObject.AddComponent<Image>();
                    itemIconImage.color = White;
                    itemIconImage.preserveAspect = true;
                    itemIconImage.raycastTarget = false;
                    itemIcon.gameObject.SetActive(false);

                    Text itemLabel = CreateText("Item Symbol", item, string.Empty, 24, FontStyle.Bold, Background,
                        Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                    itemLabel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                    item.gameObject.SetActive(false);
                    specialItemVisuals[x, y] = item;
                    specialItemImages[x, y] = itemImage;
                    specialItemIconImages[x, y] = itemIconImage;
                    specialItemLabels[x, y] = itemLabel;

                    RectTransform endpointMarker = CreateRect("Endpoint Marker", tile);
                    endpointMarker.anchorMin = endpointMarker.anchorMax = new Vector2(0.5f, 0.5f);
                    endpointMarker.sizeDelta = Vector2.one * (mainCellSize * 0.68f);
                    endpointMarker.anchoredPosition = Vector2.zero;
                    Image endpointImage = endpointMarker.gameObject.AddComponent<Image>();
                    endpointImage.color = White;
                    endpointImage.preserveAspect = true;
                    endpointImage.raycastTarget = false;
                    endpointMarker.gameObject.SetActive(false);
                    endpointMarkerImages[x, y] = endpointImage;

                    tileLabels[x, y] = CreateText("Marker", tile, string.Empty, 24, FontStyle.Bold, Background,
                        Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                    BuildAttackWarningVisual(tile, x, y);
                }
            }

            hubCampZone = CreateRect("Hub Campfire Zone", mainGrid);
            hubCampZone.anchorMin = hubCampZone.anchorMax = new Vector2(0.5f, 0.5f);
            hubCampZone.sizeDelta = Vector2.one * (mainCellSize * 4.25f);
            hubCampZone.anchoredPosition = GridPosition(2, 2, mainCellSize);
            Image campZoneImage = hubCampZone.gameObject.AddComponent<Image>();
            campZoneImage.color = new Color(0.06f, 0.62f, 0.72f, 0.075f);
            campZoneImage.raycastTarget = false;
            Outline campZoneOutline = hubCampZone.gameObject.AddComponent<Outline>();
            campZoneOutline.effectColor = Hex("1FB8D3");
            campZoneOutline.effectDistance = new Vector2(3f, -3f);
            hubCampZone.gameObject.SetActive(false);

            hubStageLane = CreateRect("Hub Stage Lane", mainGrid);
            hubStageLane.anchorMin = hubStageLane.anchorMax = new Vector2(0.5f, 0.5f);
            hubStageLane.sizeDelta = new Vector2(mainCellSize * 10.4f, mainCellSize * 1.35f);
            hubStageLane.anchoredPosition = GridPosition(5, 9, mainCellSize);
            Image stageLaneImage = hubStageLane.gameObject.AddComponent<Image>();
            stageLaneImage.color = new Color(0.72f, 0.06f, 0.10f, 0.055f);
            stageLaneImage.raycastTarget = false;
            Outline stageLaneOutline = hubStageLane.gameObject.AddComponent<Outline>();
            stageLaneOutline.effectColor = new Color(Danger.r, Danger.g, Danger.b, 0.7f);
            stageLaneOutline.effectDistance = new Vector2(2f, -2f);
            hubStageLane.gameObject.SetActive(false);

            effectsLayer = CreateRect("Effects Layer", mainGrid);
            effectsLayer.anchorMin = effectsLayer.anchorMax = new Vector2(0.5f, 0.5f);
            effectsLayer.sizeDelta = new Vector2(gridSize, gridSize);
            effectsLayer.anchoredPosition = Vector2.zero;

            attackSlash = CreateRect("Attack Slash", effectsLayer);
            attackSlash.anchorMin = attackSlash.anchorMax = new Vector2(0.5f, 0.5f);
            attackSlash.sizeDelta = new Vector2(desktopLayout ? 780f : 820f, desktopLayout ? 20f : 22f);
            attackSlash.gameObject.AddComponent<Image>().color = TrailHot;
            attackSlashGroup = attackSlash.gameObject.AddComponent<CanvasGroup>();
            attackSlashGroup.alpha = 0f;

            Texture2D crystalTexture = Resources.Load<Texture2D>("Art/red_attack_crystal");
            Sprite crystalSprite = null;
            if (crystalTexture != null)
            {
                crystalTexture.filterMode = FilterMode.Point;
                crystalTexture.wrapMode = TextureWrapMode.Clamp;
                crystalSprite = Sprite.Create(crystalTexture,
                    new Rect(0f, 0f, crystalTexture.width, crystalTexture.height),
                    new Vector2(0.5f, 0.5f), 64f);
            }
            for (int i = 0; i < crystalVisuals.Length; i++)
            {
                RectTransform crystal = CreateRect("Attack Crystal " + i, mainGrid);
                crystal.anchorMin = crystal.anchorMax = new Vector2(0.5f, 0.5f);
                crystal.sizeDelta = Vector2.one * (mainCellSize * 0.82f);
                Image crystalImage = crystal.gameObject.AddComponent<Image>();
                crystalImage.sprite = crystalSprite;
                crystalImage.preserveAspect = true;
                crystalImage.raycastTarget = false;
                Outline crystalOutline = crystal.gameObject.AddComponent<Outline>();
                crystalOutline.effectColor = Hex("FF3156");
                crystalOutline.effectDistance = new Vector2(3f, -3f);
                crystal.gameObject.SetActive(false);
                crystalVisuals[i] = crystal;
            }

            campfireSprite = CreateCampfireSprite();
            playerCharacterSprite = LoadPixelSprite("Art/warrior_front", 32f);
            for (int y = 0; y < HubWorldModel.Size; y++)
            {
                for (int x = 0; x < HubWorldModel.Size; x++)
                {
                    RectTransform hubObject = CreateRect("Hub Object " + x + "," + y, mainGrid);
                    hubObject.anchorMin = hubObject.anchorMax = new Vector2(0.5f, 0.5f);
                    hubObject.sizeDelta = Vector2.one * (mainCellSize * 0.82f);
                    hubObject.anchoredPosition = GridPosition(x, y, mainCellSize);
                    Image objectImage = hubObject.gameObject.AddComponent<Image>();
                    objectImage.color = Hex("100E14");
                    objectImage.raycastTarget = false;
                    Outline objectOutline = hubObject.gameObject.AddComponent<Outline>();
                    objectOutline.effectColor = ArenaBorderGlow;
                    objectOutline.effectDistance = new Vector2(3f, -3f);

                    RectTransform icon = CreateRect("Hub Object Icon", hubObject);
                    icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
                    icon.sizeDelta = Vector2.one * (mainCellSize * 0.68f);
                    icon.anchoredPosition = Vector2.zero;
                    Image iconImage = icon.gameObject.AddComponent<Image>();
                    iconImage.color = White;
                    iconImage.preserveAspect = true;
                    iconImage.raycastTarget = false;

                    Text label = CreateText("Hub Object Label", hubObject, string.Empty, 18,
                        FontStyle.Bold, White, new Vector2(0f, 0f), new Vector2(1f, 0.28f),
                        TextAnchor.MiddleCenter);
                    hubObject.gameObject.SetActive(false);
                    hubObjectVisuals[x, y] = hubObject;
                    hubObjectImages[x, y] = objectImage;
                    hubObjectIconImages[x, y] = iconImage;
                    hubObjectLabels[x, y] = label;
                }
            }

            mainPlayer = CreatePlayer("Player", mainGrid, mainCellSize * BattlePlayerSizeRatio, false);
            objectiveArrow = CreateRect("Objective Direction Arrow", mainGrid);
            objectiveArrow.anchorMin = objectiveArrow.anchorMax = new Vector2(0.5f, 0.5f);
            objectiveArrow.sizeDelta = new Vector2(40f, 50f);
            ObjectiveArrowGraphic arrowGraphic = objectiveArrow.gameObject.AddComponent<ObjectiveArrowGraphic>();
            arrowGraphic.color = White;
            arrowGraphic.raycastTarget = false;
            objectiveArrow.gameObject.SetActive(false);
            // The supplied block already includes its side faces. Draw distant
            // blocks first so the nearer blocks cover their back edges.
            if (UseIsometricArena)
            {
                for (int diagonal = (TrailFieldModel.Size - 1) * 2; diagonal >= 0; diagonal--)
                {
                    for (int x = 0; x < TrailFieldModel.Size; x++)
                    {
                        int y = diagonal - x;
                        if (y >= 0 && y < TrailFieldModel.Size)
                            mainTiles[x, y].transform.SetAsLastSibling();
                    }
                }
                foreach (RectTransform crystal in crystalVisuals)
                    if (crystal != null) crystal.SetAsLastSibling();
                mainPlayer.SetAsLastSibling();
            }
            if (arenaBossCore != null) arenaBossCore.SetAsLastSibling();
        }

        private void BuildHudlessArenaPresentation(RectTransform field)
        {
            arenaBossCore = CreateRect("Crimson Golem World Core", field);
            arenaBossCore.anchorMin = arenaBossCore.anchorMax = new Vector2(0.84f, 0.52f);
            arenaBossCore.sizeDelta = new Vector2(220f, 220f);
            Image coreImage = arenaBossCore.gameObject.AddComponent<Image>();
            coreImage.sprite = LoadPixelSprite("Art/red_attack_crystal", 64f);
            coreImage.color = White;
            coreImage.preserveAspect = true;
            coreImage.raycastTarget = false;

            arenaBossHealthRoot = CreateRect("World Boss Health Back", arenaBossCore);
            arenaBossHealthRoot.anchorMin = arenaBossHealthRoot.anchorMax = new Vector2(0.5f, 0.5f);
            arenaBossHealthRoot.pivot = new Vector2(0.5f, 0.5f);
            arenaBossHealthRoot.sizeDelta = new Vector2(270f, 34f);
            arenaBossHealthRoot.anchoredPosition = new Vector2(0f, -132f);
            Image worldHealthBack = arenaBossHealthRoot.gameObject.AddComponent<Image>();
            worldHealthBack.color = Hex("16090D");
            worldHealthBack.raycastTarget = false;
            Outline worldHealthOutline = arenaBossHealthRoot.gameObject.AddComponent<Outline>();
            worldHealthOutline.effectColor = Hex("FF264B");
            worldHealthOutline.effectDistance = new Vector2(2f, -2f);
            RectTransform worldHealthFill = CreateRect("World Boss Health Fill", arenaBossHealthRoot);
            worldHealthFill.anchorMin = new Vector2(0.018f, 0.16f);
            worldHealthFill.anchorMax = new Vector2(0.982f, 0.84f);
            worldHealthFill.offsetMin = Vector2.zero;
            worldHealthFill.offsetMax = Vector2.zero;
            bossHealthFill = worldHealthFill.gameObject.AddComponent<Image>();
            bossHealthFill.color = Danger;
            bossHealthFill.type = Image.Type.Filled;
            bossHealthFill.fillMethod = Image.FillMethod.Horizontal;
            bossHealthFill.fillOrigin = 0;
            bossHealthFill.raycastTarget = false;
            bossHealthText = CreateText("World Boss Health Text", arenaBossHealthRoot, "150 / 150", 18,
                FontStyle.Bold, White, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

        }

        private void BuildAttackWarningVisual(RectTransform tile, int x, int y)
        {
            RectTransform warning = CreateRect("Attack Warning", tile);
            if (UseIsometricArena)
            {
                warning.anchorMin = warning.anchorMax = new Vector2(0.5f, 0.5f);
                warning.sizeDelta = Vector2.one * (mainCellSize * 0.68f);
                warning.anchoredPosition = Vector2.zero;
                warning.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            else
            {
                warning.anchorMin = Vector2.zero;
                warning.anchorMax = Vector2.one;
                warning.offsetMin = new Vector2(3f, 3f);
                warning.offsetMax = new Vector2(-3f, -3f);
            }

            RectTransform fill = CreateRect("Warning Fill", warning);
            fill.anchorMin = fill.anchorMax = new Vector2(0.5f, 0.5f);
            fill.sizeDelta = Vector2.zero;
            fill.anchoredPosition = Vector2.zero;
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(Danger.r, Danger.g, Danger.b, 0.72f);
            fillImage.raycastTarget = false;

            CreateWarningEdge(warning, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(4f, 0f), new Vector2(0f, 0.5f));
            CreateWarningEdge(warning, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(4f, 0f), new Vector2(1f, 0.5f));
            CreateWarningEdge(warning, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 4f), new Vector2(0.5f, 0f));
            CreateWarningEdge(warning, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 4f), new Vector2(0.5f, 1f));

            warning.gameObject.SetActive(false);
            attackWarningVisuals[x, y] = warning;
            attackWarningFillImages[x, y] = fillImage;
        }

        private void CreateWarningEdge(RectTransform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 sizeDelta, Vector2 pivot)
        {
            RectTransform edge = CreateRect(name, parent);
            edge.anchorMin = anchorMin;
            edge.anchorMax = anchorMax;
            edge.pivot = pivot;
            edge.sizeDelta = sizeDelta;
            edge.anchoredPosition = Vector2.zero;
            Image edgeImage = edge.gameObject.AddComponent<Image>();
            edgeImage.color = Danger;
            edgeImage.raycastTarget = false;
        }

        private void BuildFooterV2(RectTransform root)
        {
            if (desktopLayout)
            {
                BuildDesktopPlayerPanel(root);
                return;
            }

            RectTransform footer = CreateRect("Footer", root);
            footer.anchorMin = new Vector2(0.035f, 0.02f);
            footer.anchorMax = new Vector2(0.965f, 0.225f);
            footer.offsetMin = Vector2.zero;
            footer.offsetMax = Vector2.zero;

            RectTransform guide = CreatePanel("Tactical Guide", footer, Panel, Vector2.zero, Vector2.one);
            AddAccent(guide, Vector2.zero, new Vector2(0.014f, 1f), Trail);
            CreateText("Guide Title", guide, "D-PAD / SWIPE TO TRACE", 29, FontStyle.Bold, White,
                new Vector2(0.04f, 0.76f), new Vector2(0.56f, 0.96f), TextAnchor.MiddleLeft);

            RectTransform modeBadge = CreatePanel("Control Badge", guide, Hex("10192C"),
                new Vector2(0.61f, 0.78f), new Vector2(0.96f, 0.94f));
            CreateText("Control Badge Text", modeBadge, "기본 조작 · 방향 버튼", 20, FontStyle.Bold, StartColor,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            directionPadArea = CreateRect("Direction Pad", guide);
            directionPadArea.anchorMin = new Vector2(0.20f, 0.05f);
            directionPadArea.anchorMax = new Vector2(0.80f, 0.72f);
            directionPadArea.offsetMin = Vector2.zero;
            directionPadArea.offsetMax = Vector2.zero;
            BuildDirectionButton(directionPadArea, "Up", "▲", Vector2Int.up,
                new Vector2(0.36f, 0.52f), new Vector2(0.64f, 1f));
            BuildDirectionButton(directionPadArea, "Left", "◀", Vector2Int.left,
                new Vector2(0f, 0.04f), new Vector2(0.32f, 0.52f));
            BuildDirectionButton(directionPadArea, "Down", "▼", Vector2Int.down,
                new Vector2(0.36f, 0.04f), new Vector2(0.64f, 0.52f));
            BuildDirectionButton(directionPadArea, "Right", "▶", Vector2Int.right,
                new Vector2(0.68f, 0.04f), new Vector2(1f, 0.52f));

            powerText = CreateText("Hidden Rule State", guide, string.Empty, 1, FontStyle.Normal,
                new Color(0f, 0f, 0f, 0f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            powerText.gameObject.SetActive(false);

            RectTransform status = CreatePanel("Status", root, PanelLight,
                new Vector2(0.035f, 0.225f), new Vector2(0.965f, 0.245f));
            AddAccent(status, Vector2.zero, new Vector2(0.012f, 1f), TrailHot);
            statusText = CreateText("Status Text", status, string.Empty, 23, FontStyle.Bold, White,
                new Vector2(0.025f, 0f), new Vector2(0.975f, 1f), TextAnchor.MiddleCenter);
        }

        private void BuildDesktopPlayerPanel(RectTransform root)
        {
            RectTransform panel = CreatePanel("Desktop Player Panel", root, Panel,
                new Vector2(0.025f, 0.14f), new Vector2(0.19f, 0.69f));
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = Hex("A64CC2");
            outline.effectDistance = new Vector2(4f, -4f);
            AddAccent(panel, Vector2.zero, new Vector2(0.025f, 1f), Hex("B85BD4"));

            CreateText("Player Panel Label", panel, "CURRENT CHARACTER", 16, FontStyle.Bold, Hex("D88BEE"),
                new Vector2(0.07f, 0.925f), new Vector2(0.93f, 0.985f), TextAnchor.MiddleCenter);

            RectTransform portrait = CreatePanel("Character Portrait Frame", panel, Hex("0D1424"),
                new Vector2(0.26f, 0.68f), new Vector2(0.74f, 0.91f));
            Outline portraitOutline = portrait.gameObject.AddComponent<Outline>();
            portraitOutline.effectColor = Trail;
            portraitOutline.effectDistance = new Vector2(2f, -2f);
            RectTransform portraitImageRect = CreateRect("Character Portrait Image", portrait);
            portraitImageRect.anchorMin = Vector2.zero;
            portraitImageRect.anchorMax = Vector2.one;
            portraitImageRect.offsetMin = new Vector2(8f, 8f);
            portraitImageRect.offsetMax = new Vector2(-8f, -8f);
            Image portraitImage = portraitImageRect.gameObject.AddComponent<Image>();
            portraitImage.sprite = LoadPixelSprite("Art/warrior_front", 32f);
            portraitImage.color = White;
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            CreateText("Character Name", panel, "트레이스 워리어", 23, FontStyle.Bold, White,
                new Vector2(0.07f, 0.61f), new Vector2(0.93f, 0.68f), TextAnchor.MiddleCenter);
            playerHealthText = CreateText("Player HP", panel, "♥  HP 1", 22, FontStyle.Bold, StartColor,
                new Vector2(0.07f, 0.555f), new Vector2(0.93f, 0.615f), TextAnchor.MiddleCenter);

            RectTransform special = CreatePanel("Character Special Ability", panel, Hex("16102A"),
                new Vector2(0.07f, 0.31f), new Vector2(0.93f, 0.54f));
            AddAccent(special, Vector2.zero, new Vector2(0.025f, 1f), Hex("B85BD4"));
            CreateText("Special Ability Label", special, "특수능력", 15, FontStyle.Bold, Hex("D88BEE"),
                new Vector2(0.07f, 0.70f), new Vector2(0.93f, 0.95f), TextAnchor.MiddleLeft);
            CreateText("Special Ability Name", special, "트레이스 드라이버", 17, FontStyle.Bold, White,
                new Vector2(0.07f, 0.45f), new Vector2(0.93f, 0.72f), TextAnchor.MiddleLeft);
            CreateText("Special Ability Description", special, "END 도착 시\n그린 경로로 공격", 14,
                FontStyle.Normal, Muted, new Vector2(0.07f, 0.06f), new Vector2(0.93f, 0.46f),
                TextAnchor.MiddleLeft);

            RectTransform passive = CreatePanel("Character Passive Ability", panel, Hex("10192C"),
                new Vector2(0.07f, 0.07f), new Vector2(0.93f, 0.29f));
            AddAccent(passive, Vector2.zero, new Vector2(0.025f, 1f), Trail);
            CreateText("Passive Ability Label", passive, "패시브", 15, FontStyle.Bold, Trail,
                new Vector2(0.07f, 0.69f), new Vector2(0.93f, 0.95f), TextAnchor.MiddleLeft);
            CreateText("Passive Ability Name", passive, "경로 증폭", 17, FontStyle.Bold, White,
                new Vector2(0.07f, 0.44f), new Vector2(0.93f, 0.70f), TextAnchor.MiddleLeft);
            CreateText("Passive Ability Description", passive, "경로가 길수록\n공격 피해 증가", 14,
                FontStyle.Normal, Muted, new Vector2(0.07f, 0.06f), new Vector2(0.93f, 0.45f),
                TextAnchor.MiddleLeft);

            RectTransform status = CreatePanel("Desktop Status", root, PanelLight,
                new Vector2(0.025f, 0.07f), new Vector2(0.19f, 0.13f));
            AddAccent(status, Vector2.zero, new Vector2(0.025f, 1f), TrailHot);
            statusText = CreateText("Status Text", status, string.Empty, 14, FontStyle.Bold, White,
                new Vector2(0.07f, 0.08f), new Vector2(0.93f, 0.92f), TextAnchor.MiddleLeft);

            powerText = CreateText("Hidden Rule State", panel, string.Empty, 1, FontStyle.Normal,
                new Color(0f, 0f, 0f, 0f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            powerText.gameObject.SetActive(false);
            directionPadArea = null;

            panel.gameObject.SetActive(false);
            status.gameObject.SetActive(false);
        }

        private void BuildDesktopMinimap(RectTransform root)
        {
            // Anchor a true square to the screen, not the moving battle grid.
            minimapRoot = CreateRect("Desktop Minimap", root);
            minimapRoot.anchorMin = minimapRoot.anchorMax = Vector2.one;
            minimapRoot.pivot = Vector2.one;
            minimapRoot.sizeDelta = Vector2.one * DesktopMinimapSize;
            minimapRoot.anchoredPosition = new Vector2(-28f, -28f);
            Image backdrop = minimapRoot.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.025f, 0.035f, 0.045f, 0.30f);
            backdrop.raycastTarget = false;
            CanvasGroup group = minimapRoot.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            // Thin edge images avoid Outline duplicating a translucent panel's entire fill.
            for (int side = 0; side < 4; side++)
            {
                bool horizontal = side < 2;
                float edge = side % 2;
                RectTransform border = CreateRect("Minimap Border " + side, minimapRoot);
                border.anchorMin = horizontal ? new Vector2(0f, edge) : new Vector2(edge, 0f);
                border.anchorMax = horizontal ? new Vector2(1f, edge) : new Vector2(edge, 1f);
                border.sizeDelta = horizontal ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
                border.anchoredPosition = Vector2.zero;
                Image borderImage = border.gameObject.AddComponent<Image>();
                borderImage.color = new Color(0.76f, 0.80f, 0.77f, 0.45f);
                borderImage.raycastTarget = false;
            }

            float miniGridSize = MinimapCellSize * TrailFieldModel.Size;
            minimapGrid = CreateRect("Minimap Grid", minimapRoot);
            minimapGrid.anchorMin = minimapGrid.anchorMax = new Vector2(0.5f, 0.5f);
            minimapGrid.pivot = new Vector2(0.5f, 0.5f);
            minimapGrid.sizeDelta = Vector2.one * miniGridSize;
            minimapGrid.anchoredPosition = Vector2.zero;

            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    RectTransform tile = CreateRect("Minimap Tile " + x + "," + y, minimapGrid);
                    tile.anchorMin = tile.anchorMax = new Vector2(0.5f, 0.5f);
                    tile.sizeDelta = Vector2.one * (MinimapCellSize - 1.5f);
                    tile.anchoredPosition = GridPosition(x, y, MinimapCellSize);
                    Image image = tile.gameObject.AddComponent<Image>();
                    image.color = Floor;
                    image.raycastTarget = false;
                    minimapTiles[x, y] = image;
                }
            }

            minimapPlayer = CreateRect("Minimap Player", minimapGrid);
            minimapPlayer.anchorMin = minimapPlayer.anchorMax = new Vector2(0.5f, 0.5f);
            minimapPlayer.sizeDelta = Vector2.one * 10f;
            minimapPlayer.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image playerMarker = minimapPlayer.gameObject.AddComponent<Image>();
            playerMarker.color = White;
            playerMarker.raycastTarget = false;
            Outline playerOutline = minimapPlayer.gameObject.AddComponent<Outline>();
            playerOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            playerOutline.effectDistance = new Vector2(2f, -2f);
            minimapRoot.gameObject.SetActive(false);
        }

        private void RefreshMinimap()
        {
            if (!desktopLayout || minimapGrid == null)
            {
                return;
            }

            int tutorialOffset = (TrailFieldModel.Size - TutorialRules.Size) / 2;
            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    Image tile = minimapTiles[x, y];
                    if (tile == null)
                    {
                        continue;
                    }

                    if (tutorialActive)
                    {
                        bool active = x >= tutorialOffset && x < tutorialOffset + TutorialRules.Size &&
                            y >= tutorialOffset && y < tutorialOffset + TutorialRules.Size;
                        tile.gameObject.SetActive(active);
                        if (!active)
                        {
                            continue;
                        }

                        Vector2Int cell = new Vector2Int(x - tutorialOffset, y - tutorialOffset);
                        Color color = Floor;
                        if (tutorialStep == 0 && tutorialTrail.Contains(cell)) color = Trail;
                        if (tutorialStep == 0 && cell == TutorialRules.Start) color = StartColor;
                        if (tutorialStep == 0 && cell == TutorialRules.End) color = EndColor;
                        tile.color = color;
                        continue;
                    }

                    Vector2Int boardCell = new Vector2Int(x, y);
                    bool walkable = model.IsWalkable(boardCell);
                    tile.gameObject.SetActive(walkable);
                    if (!walkable)
                    {
                        continue;
                    }

                    Color boardColor = CombatBalanceRules.IsCenterDamageCell(boardCell, TrailFieldModel.Size)
                        ? new Color(0.90f, 0.55f, 0.24f, 0.65f)
                        : new Color(0.65f, 0.62f, 0.53f, 0.48f);
                    if (model.IsTrail(boardCell))
                        boardColor = new Color(Trail.r, Trail.g, Trail.b, 0.85f);
                    if (crystalCells.Contains(boardCell)) boardColor = Danger;
                    if (warnedCells.Contains(boardCell) || targetedCells.Contains(boardCell) ||
                        crystalWarningCounts.ContainsKey(boardCell) ||
                        crystalFiringCounts.ContainsKey(boardCell)) boardColor = Hex("C7465F");
                    // Navigation endpoints remain legible even during a warning.
                    if (boardCell == model.Start) boardColor = StartColor;
                    if (boardCell == model.End) boardColor = EndColor;
                    tile.color = boardColor;
                }
            }
            UpdateMinimapPlayer();
        }

        private void UpdateMinimapPlayer()
        {
            if (minimapRoot == null) return;
            bool visible = desktopLayout && !titleActive && !hubActive;
            minimapRoot.gameObject.SetActive(visible);
            if (!visible || minimapPlayer == null) return;

            if (tutorialActive)
            {
                int offset = (TrailFieldModel.Size - TutorialRules.Size) / 2;
                minimapPlayer.anchoredPosition = GridPosition(
                    tutorialPlayer.x + offset, tutorialPlayer.y + offset, MinimapCellSize);
            }
            else
            {
                // Ignore camera scrolling and shake; track the player's smooth grid position.
                minimapPlayer.anchoredPosition = battlePlayerVisualPosition * (MinimapCellSize / mainCellSize);
            }
            minimapPlayer.SetAsLastSibling();
        }

        private void BuildDirectionButton(RectTransform parent, string name, string label,
            Vector2Int direction, Vector2 min, Vector2 max)
        {
            RectTransform buttonRect = CreatePanel(name + " Direction Button", parent, PanelLight, min, max);
            Outline outline = buttonRect.gameObject.AddComponent<Outline>();
            outline.effectColor = Trail;
            outline.effectDistance = new Vector2(3f, -3f);
            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonRect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.42f, 0.82f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() => MoveFromDirectionButton(direction));
            CreateText(name + " Arrow", buttonRect, label, 44, FontStyle.Bold, White,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        }

        private void MoveFromDirectionButton(Vector2Int direction)
        {
            if (titleActive || inputLocked || movementFrozen || playerDead || gameCleared)
            {
                return;
            }
            Move(direction);
        }

        private void RefreshBoard()
        {
            HideHubWorldVisuals();
            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    var cell = new Vector2Int(x, y);
                    bool active = model.IsWalkable(cell);
                    arenaGroundTiles[x, y].gameObject.SetActive(false);
                    mainTileDepthRoots[x, y].gameObject.SetActive(
                        active && UseExtraTileDepth);
                    mainTiles[x, y].gameObject.SetActive(active);
                    if (!active)
                    {
                        continue;
                    }

                    Color color = GetFloorColor(x, y);
                    Outline tileOutline = mainTileOutlines[x, y];
                    if (desktopLayout && tileOutline != null)
                    {
                        bool boundary = IsArenaBoundary(cell);
                        tileOutline.effectColor = boundary ? ArenaBorderGlow : Hex("39251F");
                        tileOutline.effectDistance = boundary
                            ? new Vector2(2.4f, -2.4f)
                            : new Vector2(1.1f, -1.1f);
                    }
                    bool isCrystal = crystalCells.Contains(cell);
                    bool crystalWarned = crystalWarningCounts.ContainsKey(cell);
                    bool crystalFiring = crystalFiringCounts.ContainsKey(cell);
                    bool hasSpecialTile = specialTiles.TryGetValue(cell, out SpecialTileType specialType);
                    if (isCrystal) color = Hex("4A1723");
                    if (model.IsTrail(cell)) color = Trail;
                    if (cell == model.Start) color = StartColor;
                    if (cell == model.End) color = EndColor;
                    color.a = model.IsTrail(cell) ? 1f : StandardTileOpacity;
                    if (warnedCells.Contains(cell))
                    {
                        if (hazardFiring)
                        {
                            color = Color.Lerp(Danger, White, 0.28f);
                            color.a = 1f;
                        }
                    }
                    if (targetedCells.Contains(cell))
                    {
                        if (targetedFiring)
                        {
                            Color targetColor = Hex("B44CFF");
                            color = Color.Lerp(targetColor, White, 0.32f);
                            color.a = 1f;
                        }
                    }
                    if (crystalWarned)
                    {
                        color.a = StandardTileOpacity;
                    }
                    if (crystalFiring)
                    {
                        color = Color.Lerp(Hex("FF3B24"), White, 0.38f);
                        color.a = 1f;
                    }
                    mainTiles[x, y].color = color;
                    SetTileDepthColor(x, y, color);
                    SetSpecialItemVisual(x, y, hasSpecialTile && !isCrystal, specialType, mainCellSize * 0.48f);
                    bool lightMarker = warnedCells.Contains(cell) || targetedCells.Contains(cell) ||
                        crystalWarned || crystalFiring;
                    SetEndpointMarkerVisual(x, y,
                        cell == model.Start && !lightMarker,
                        cell == model.End && !lightMarker,
                        mainCellSize * 0.68f);
                    tileLabels[x, y].color = lightMarker ? White : Background;
                    tileLabels[x, y].fontSize = 24;
                    tileLabels[x, y].text =
                        cell == model.Start && startMarkerSprite == null ? "S" :
                        cell == model.End && endMarkerSprite == null ? "E" :
                        string.Empty;
                }
            }

            mainPlayer.SetAsLastSibling();
            UpdateBattleCameraTarget();
            RefreshMinimap();

            int shownLength = model.IsTracing ? model.Trail.Count : 0;
            int projectedDamage = model.IsTracing ? CalculateDamage(model.Trail) : 0;
            comboText.text = model.IsTracing
                ? "경로 " + shownLength + "칸  ·  예상 피해 " + projectedDamage
                : "경로 대기  ·  START 필요";
            UpdatePowerRuleText();
            RefreshInteractionPanel();
        }

        private void UpdateBattleCameraTarget()
        {
            if (mainGrid == null || tutorialActive || hubActive)
            {
                return;
            }

            Vector2 playerPosition = GridPosition(model.Player.x, model.Player.y, mainCellSize);
            battleCameraTarget = GetBattleCameraTarget(playerPosition);
            if (!battleCameraInitialized)
            {
                battlePlayerVisualPosition = playerPosition;
                battlePlayerMoveFrom = playerPosition;
                battlePlayerMoveTarget = playerPosition;
                battlePlayerMoveTime = BattlePlayerMoveSeconds;
                battlePlayerNudge = Vector2.zero;
                battleCameraPosition = battleCameraTarget;
                battleCameraShake = Vector2.zero;
                mainPlayer.anchoredPosition = PixelSnap(playerPosition);
                mainGrid.anchoredPosition = PixelSnap(battleCameraPosition);
                battleCameraInitialized = true;
            }
            else if (battlePlayerMoveTarget != playerPosition)
            {
                battlePlayerMoveFrom = battlePlayerVisualPosition;
                battlePlayerMoveTarget = playerPosition;
                battlePlayerMoveTime = 0f;
            }
        }

        private Vector2 GetBattleCameraTarget(Vector2 playerPosition)
        {
            Vector2 focus = desktopLayout ? new Vector2(-96f, -24f) : new Vector2(0f, -60f);
            Vector2 lookAhead = titleActive ? Vector2.zero : (Vector2)playerFacing * (mainCellSize * 0.24f);
            Vector2 viewport = desktopLayout
                ? new Vector2(DesktopReferenceWidth, DesktopReferenceHeight)
                : ((RectTransform)mainGrid.parent).rect.size;
            return ClampBattleCamera(focus - playerPosition - lookAhead, mainGrid.sizeDelta, viewport);
        }

        public static Vector2 ClampBattleCamera(Vector2 desired, Vector2 arenaSize, Vector2 viewportSize)
        {
            Vector2 limit = Vector2.Max(Vector2.zero, (arenaSize - viewportSize) * 0.5f);
            return new Vector2(Mathf.Clamp(desired.x, -limit.x, limit.x),
                Mathf.Clamp(desired.y, -limit.y, limit.y));
        }

        private void LateUpdate()
        {
            AnimateBattleCamera();
            UpdateObjectiveArrow();
            UpdateMinimapPlayer();
        }

        private void UpdateObjectiveArrow()
        {
            if (objectiveArrow == null || mainPlayer == null) return;
            bool visible = !titleActive && !hubActive && !playerDead && !gameCleared &&
                !inputLocked && !tutorialTransitioning && (!tutorialActive || tutorialStep == 0) &&
                (phaseBannerGroup == null || phaseBannerGroup.alpha < 0.2f);
            Vector2 target = tutorialActive
                ? TutorialGridPosition(tutorialTrail.Count > 0 ? TutorialRules.End : TutorialRules.Start)
                : GridPosition(model.NavigationTarget.x, model.NavigationTarget.y, mainCellSize);
            Vector2 direction = target - mainPlayer.anchoredPosition;
            visible &= direction.sqrMagnitude > 4f;
            objectiveArrow.gameObject.SetActive(visible);
            if (!visible)
            {
                objectiveArrowInitialized = false;
                return;
            }

            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            objectiveArrowAngle = objectiveArrowInitialized
                ? Mathf.MoveTowardsAngle(objectiveArrowAngle, targetAngle, 900f * Time.unscaledDeltaTime)
                : targetAngle;
            objectiveArrowInitialized = true;
            float radius = mainPlayer.sizeDelta.x * 0.5f + 38f;
            float radians = objectiveArrowAngle * Mathf.Deg2Rad;
            Vector2 orbit = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
            objectiveArrow.anchoredPosition = PixelSnap(mainPlayer.anchoredPosition + orbit);
            objectiveArrow.localRotation = Quaternion.Euler(0f, 0f, objectiveArrowAngle);
            objectiveArrow.SetAsLastSibling();
        }

        private void AnimateBattleCamera()
        {
            if (!battleCameraInitialized || mainGrid == null ||
                tutorialActive || hubActive)
            {
                return;
            }

            battlePlayerMoveTime = Mathf.Min(BattlePlayerMoveSeconds,
                battlePlayerMoveTime + Time.unscaledDeltaTime);
            float moveProgress = Mathf.SmoothStep(0f, 1f, battlePlayerMoveTime / BattlePlayerMoveSeconds);
            battlePlayerVisualPosition = Vector2.Lerp(battlePlayerMoveFrom, battlePlayerMoveTarget, moveProgress);
            mainPlayer.anchoredPosition = PixelSnap(battlePlayerVisualPosition + battlePlayerNudge);
            battleCameraTarget = GetBattleCameraTarget(battlePlayerMoveTarget);
            Vector2 trackingTarget = GetBattleCameraTarget(battlePlayerVisualPosition);
            float blend = 1f - Mathf.Exp(-BattleCameraFollowSpeed * Time.unscaledDeltaTime);
            // Preserve subpixel progress; snap only the displayed position.
            battleCameraPosition = Vector2.Lerp(battleCameraPosition, trackingTarget, blend);
            mainGrid.anchoredPosition = PixelSnap(battleCameraPosition + battleCameraShake);
        }

        private void HideHubWorldVisuals()
        {
            if (hubCampZone != null) hubCampZone.gameObject.SetActive(false);
            if (hubStageLane != null) hubStageLane.gameObject.SetActive(false);
            for (int y = 0; y < HubWorldModel.Size; y++)
            {
                for (int x = 0; x < HubWorldModel.Size; x++)
                {
                    RectTransform visual = hubObjectVisuals[x, y];
                    if (visual != null)
                    {
                        visual.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void RefreshInteractionPanel()
        {
            if (hubActive || !desktopLayout || interactionTitleText == null || interactionBodyText == null)
            {
                return;
            }

            Vector2Int frontCell = model.Player + playerFacing;
            string title = "스테이지 보스 · 크림슨 골렘";
            string body = "광물 증식형 바이러스 · 레이저와 수정 폭발 패턴";
            Sprite icon = bossPortraitSprite;
            Color accent = Danger;

            if (crystalCells.Contains(frontCell))
            {
                title = "공격 수정 · 이동 불가";
                body = phaseTwoActive
                    ? "주변 2칸에 격자 폭발을 일으키는 바이러스 복제 노드"
                    : "2페이즈에서 활성화되는 바이러스 복제 노드";
                icon = bossPortraitSprite;
                accent = Hex("FF3156");
            }
            else if (frontCell == model.Start)
            {
                title = "공격 시작 타일";
                body = "이 지점을 밟으면 트레이스 경로 기록이 시작됩니다";
                icon = startMarkerSprite;
                accent = StartColor;
            }
            else if (frontCell == model.End)
            {
                title = "공격 종료 타일";
                body = "경로를 유지한 채 도착하면 보스 공격이 실행됩니다";
                icon = endMarkerSprite;
                accent = EndColor;
            }
            else if (specialTiles.TryGetValue(frontCell, out SpecialTileType specialType))
            {
                GetSpecialInteractionInfo(specialType, out title, out body);
                icon = GetSpecialTileIcon(specialType);
                accent = GetSpecialTileColor(specialType);
            }
            else if (!model.IsWalkable(frontCell))
            {
                title = "필드 경계";
                body = "이동할 수 없는 영역입니다 · 다른 방향으로 이동하세요";
                icon = null;
                accent = Muted;
            }

            interactionTitleText.text = title;
            interactionBodyText.text = body;
            if (interactionRingImage != null)
            {
                interactionRingImage.color = accent;
            }
            if (interactionIconImage != null)
            {
                interactionIconImage.sprite = icon;
                interactionIconImage.color = icon == null ? accent : White;
                interactionIconImage.gameObject.SetActive(icon != null);
            }
        }

        private static void GetSpecialInteractionInfo(SpecialTileType type, out string title, out string body)
        {
            switch (type)
            {
                case SpecialTileType.Power:
                    title = "더하기 발판 · 공격 보조";
                    body = "다음 공격의 기본 피해가 25 증가합니다";
                    break;
                case SpecialTileType.Amplify:
                    title = "곱셈 발판 · 출력 증폭";
                    body = "다음 공격 피해가 1.35배로 증가합니다";
                    break;
                case SpecialTileType.Mud:
                    title = "정지 발판 · 이동 방해";
                    body = "밟으면 플레이어 이동이 1초 동안 정지합니다";
                    break;
                case SpecialTileType.Curse:
                    title = "다운 발판 · 출력 감소";
                    body = "다음 공격 피해가 0.65배로 감소합니다";
                    break;
                default:
                    title = "위험 발판";
                    body = "플레이어에게 불리한 효과를 발생시킵니다";
                    break;
            }
        }

        private void SetSpecialItemVisual(int x, int y, bool active, SpecialTileType type, float size)
        {
            RectTransform item = specialItemVisuals[x, y];
            if (item == null)
            {
                return;
            }

            item.gameObject.SetActive(active);
            if (!active)
            {
                return;
            }

            item.sizeDelta = Vector2.one * size;
            item.anchoredPosition = Vector2.zero;
            Color itemColor = GetSpecialTileColor(type);
            itemColor.a = 0.96f;
            specialItemImages[x, y].color = itemColor;
            Image itemIcon = specialItemIconImages[x, y];
            Sprite iconSprite = GetSpecialTileIcon(type);
            bool showIcon = itemIcon != null && iconSprite != null;
            if (itemIcon != null)
            {
                itemIcon.gameObject.SetActive(showIcon);
                if (showIcon)
                {
                    itemIcon.sprite = iconSprite;
                    itemIcon.color = White;
                    itemIcon.rectTransform.sizeDelta = Vector2.one * (size * 0.72f);
                    itemIcon.rectTransform.anchoredPosition = Vector2.zero;
                }
            }
            Text itemLabel = specialItemLabels[x, y];
            itemLabel.gameObject.SetActive(!showIcon);
            itemLabel.text = showIcon ? string.Empty : GetSpecialTileMarker(type);
            itemLabel.fontSize = Mathf.Max(18, Mathf.RoundToInt(size * 0.55f));
            itemLabel.color = type == SpecialTileType.Curse || type == SpecialTileType.Spike
                ? White : Background;
            itemLabel.rectTransform.anchoredPosition = Vector2.zero;
        }

        private Sprite GetSpecialTileIcon(SpecialTileType type)
        {
            switch (type)
            {
                case SpecialTileType.Power: return powerIconSprite;
                case SpecialTileType.Amplify: return amplifyIconSprite;
                case SpecialTileType.Mud: return mudIconSprite;
                case SpecialTileType.Curse: return curseIconSprite;
                default: return null;
            }
        }

        private void SetEndpointMarkerVisual(int x, int y, bool showStart, bool showEnd, float size)
        {
            Image markerImage = endpointMarkerImages[x, y];
            if (markerImage == null)
            {
                return;
            }

            Sprite markerSprite = showStart ? startMarkerSprite : showEnd ? endMarkerSprite : null;
            markerImage.gameObject.SetActive(markerSprite != null);
            if (markerSprite == null)
            {
                return;
            }

            markerImage.sprite = markerSprite;
            markerImage.color = White;
            markerImage.rectTransform.sizeDelta = Vector2.one * size;
            markerImage.rectTransform.anchoredPosition = Vector2.zero;
        }

        private static Sprite LoadPixelSprite(string resourcePath, float pixelsPerUnit)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private void LoadGolemTileSprites()
        {
            Texture2D squareFloor = Resources.Load<Texture2D>("Art/cave_floor_tile_128x128");
            if (!UseIsometricArena && squareFloor != null)
            {
                squareFloor.filterMode = FilterMode.Point;
                squareFloor.wrapMode = TextureWrapMode.Clamp;
                // Slice out only the supplied tile's face; preserve the source PNG.
                // The 128x128 source has a 101x101 opaque face at (14,14), top-left
                // origin. Remove only transparent padding, retaining the pixel border.
                Rect face = new Rect(14f, 13f, 101f, 101f);
                golemBaseTileSprite = Sprite.Create(squareFloor, face,
                    new Vector2(0.5f, 0.5f), 64f);
                golemEdgeTileSprite = null;
                return;
            }

            Texture2D isometricFloor = Resources.Load<Texture2D>(
                "Art/rounded_cave_block_tile_64");
            Texture2D isometricEdge = Resources.Load<Texture2D>(
                "Art/isometric_brown_edge_tile_64");
            if (UseIsometricArena && isometricFloor != null && isometricEdge != null)
            {
                isometricFloor.filterMode = FilterMode.Point;
                isometricFloor.wrapMode = TextureWrapMode.Clamp;
                isometricEdge.filterMode = FilterMode.Point;
                isometricEdge.wrapMode = TextureWrapMode.Clamp;
                golemBaseTileSprite = Sprite.Create(isometricFloor,
                    new Rect(0f, 0f, isometricFloor.width, isometricFloor.height),
                    new Vector2(0.5f, 0.5f), 64f);
                golemEdgeTileSprite = Sprite.Create(isometricEdge,
                    new Rect(0f, 0f, isometricEdge.width, isometricEdge.height),
                    new Vector2(0.5f, 0.5f), 64f);
                return;
            }

            Texture2D texture = Resources.Load<Texture2D>("Art/golem_tiles");
            if (texture == null || texture.width < 80 || texture.height < 64)
            {
                golemBaseTileSprite = null;
                golemEdgeTileSprite = null;
                return;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            // Source sheet is numbered left-to-right, top-to-bottom. Remove its
            // transparent spacing and crop tile 6 (top face) and tile 4 (edge).
            golemBaseTileSprite = Sprite.Create(texture, new Rect(64f, 48f, 16f, 16f),
                new Vector2(0.5f, 0.5f), 16f);
            golemEdgeTileSprite = Sprite.Create(texture, new Rect(0f, 53f, 16f, 11f),
                new Vector2(0.5f, 0.5f), 16f);
        }

        private static Sprite[] LoadFloorTileSprites()
        {
            const int rows = 5;
            const int columns = 3;
            var sprites = new List<Sprite>(rows * columns);
            for (int row = 1; row <= rows; row++)
            {
                for (int column = 1; column <= columns; column++)
                {
                    Sprite sprite = LoadPixelSprite(
                        $"Art/IvyTiles/ivy_tile_r{row}_c{column}", 32f);
                    if (sprite != null)
                    {
                        sprites.Add(sprite);
                    }
                }
            }

            return sprites.ToArray();
        }

        private Sprite GetFloorTileSprite(int x, int y)
        {
            if (floorTileSprites == null || floorTileSprites.Length == 0)
            {
                return floorTileSprite;
            }

            int index = floorTileVariantIndices[x, y] % floorTileSprites.Length;
            return floorTileSprites[index];
        }

        private void RandomizeFloorTileLayout()
        {
            if (floorTileSprites == null || floorTileSprites.Length == 0)
            {
                return;
            }

            var shuffleBag = new List<int>(floorTileSprites.Length);
            int cellIndex = 0;
            int cellCount = TrailFieldModel.Size * TrailFieldModel.Size;
            while (cellIndex < cellCount)
            {
                shuffleBag.Clear();
                for (int i = 0; i < floorTileSprites.Length; i++)
                {
                    shuffleBag.Add(i);
                }

                for (int i = shuffleBag.Count - 1; i > 0; i--)
                {
                    int swapIndex = floorTileRandom.Next(i + 1);
                    int value = shuffleBag[i];
                    shuffleBag[i] = shuffleBag[swapIndex];
                    shuffleBag[swapIndex] = value;
                }

                for (int i = 0; i < shuffleBag.Count && cellIndex < cellCount; i++, cellIndex++)
                {
                    int x = cellIndex % TrailFieldModel.Size;
                    int y = cellIndex / TrailFieldModel.Size;
                    floorTileVariantIndices[x, y] = shuffleBag[i];
                }
            }
        }

        private void ApplyFloorTileLayout()
        {
            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    if (mainTiles[x, y] != null)
                    {
                        mainTiles[x, y].sprite = GetFloorTileSprite(x, y);
                    }
                    if (mainTileDepthImages[x, y] != null)
                    {
                        mainTileDepthImages[x, y].sprite = golemEdgeTileSprite != null
                            ? golemEdgeTileSprite
                            : GetFloorTileSprite(x, y);
                    }
                }
            }
        }

        private static Sprite CreateCircleSprite(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Runtime Circle";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = center - 1f;
            float featherStart = radius - 1.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    byte alpha = distance <= featherStart
                        ? (byte)255
                        : distance >= radius ? (byte)0 : (byte)Mathf.RoundToInt((radius - distance) / 1.5f * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateCampfireSprite()
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Runtime Pixel Campfire";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];

            PaintRect(pixels, size, 6, 5, 20, 5, new Color32(72, 38, 27, 255));
            PaintRect(pixels, size, 9, 7, 14, 4, new Color32(151, 75, 39, 255));
            PaintRect(pixels, size, 9, 11, 14, 8, new Color32(255, 91, 30, 255));
            PaintRect(pixels, size, 12, 15, 8, 9, new Color32(255, 176, 38, 255));
            PaintRect(pixels, size, 14, 20, 4, 7, new Color32(255, 241, 126, 255));
            PaintRect(pixels, size, 6, 3, 6, 3, new Color32(116, 61, 37, 255));
            PaintRect(pixels, size, 20, 3, 6, 3, new Color32(116, 61, 37, 255));

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static void PaintRect(Color32[] pixels, int textureSize, int startX, int startY,
            int width, int height, Color32 color)
        {
            for (int y = startY; y < startY + height; y++)
            {
                for (int x = startX; x < startX + width; x++)
                {
                    pixels[y * textureSize + x] = color;
                }
            }
        }

        private Color GetFloorColor(int x, int y)
        {
            if (floorTileSprite != null)
            {
                Color textureTint = CombatBalanceRules.IsCenterDamageCell(
                    new Vector2Int(x, y), TrailFieldModel.MaxSize)
                    ? CenterDamageTileTint
                    : ArenaTileTextureTint;
                textureTint.a = StandardTileOpacity;
                return textureTint;
            }

            Color baseColor;
            Color highlight;
            if (desktopLayout)
            {
                baseColor = ArenaTile;
                highlight = ArenaTileLift;
                float arenaPattern = (x + y) % 2 == 0 ? 0.34f : 0.10f;
                if ((x * 3 + y * 5 + stage) % 7 == 0)
                {
                    arenaPattern += 0.14f;
                }
                Color arenaColor = Color.Lerp(baseColor, highlight, arenaPattern);
                arenaColor.a = StandardTileOpacity;
                return arenaColor;
            }
            switch (model.ShapeIndex)
            {
                case 1:
                    baseColor = Hex("332D56");
                    highlight = Hex("4A3D71");
                    break;
                case 2:
                    baseColor = Hex("1E4050");
                    highlight = Hex("28596A");
                    break;
                default:
                    baseColor = Hex("202838");
                    highlight = Hex("3A465A");
                    break;
            }

            float pattern = (x + y) % 2 == 0 ? 0.32f : 0.08f;
            if ((x * 3 + y * 5 + stage) % 7 == 0)
            {
                pattern += 0.16f;
            }
            Color floorColor = Color.Lerp(baseColor, highlight, pattern);
            floorColor.a = StandardTileOpacity;
            return floorColor;
        }

        private void AnimateVisuals()
        {
            AnimateArenaAmbience();
            if (mainPlayerImage == null)
            {
                return;
            }

            AnimateSpecialItems();
            AnimateAttackWarnings();

            if (tutorialActive)
            {
                if (!playerDead && !movementFrozen)
                {
                    float tutorialPulse = (Mathf.Sin(Time.unscaledTime * 4.5f) + 1f) * 0.5f;
                    mainPlayerImage.color = Color.Lerp(White, TrailHot, tutorialPulse * 0.08f);
                }
                return;
            }

            if (hubActive)
            {
                float hubPulse = (Mathf.Sin(Time.unscaledTime * 4.2f) + 1f) * 0.5f;
                if (!playerDead && !movementFrozen)
                {
                    mainPlayerImage.color = Color.Lerp(activeCharacterTint, White, hubPulse * 0.12f);
                }
                foreach (KeyValuePair<Vector2Int, HubObjectData> entry in hubModel.Objects)
                {
                    RectTransform visual = hubObjectVisuals[entry.Key.x, entry.Key.y];
                    if (visual == null || !visual.gameObject.activeSelf)
                    {
                        continue;
                    }
                    bool focused = hubModel.FocusedCell.HasValue && hubModel.FocusedCell.Value == entry.Key;
                    float scale = focused ? 1.08f + hubPulse * 0.08f : 0.96f + hubPulse * 0.04f;
                    visual.localScale = Vector3.one * PixelStep(scale, 0.02f);
                }
                return;
            }

            if (!model.IsWalkable(model.Start) || !model.IsWalkable(model.End))
            {
                return;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * 4.5f) + 1f) * 0.5f;
            Color startPulse = Color.Lerp(StartColor, White, pulse * 0.2f);
            Color endPulse = Color.Lerp(EndColor, TrailHot, pulse * 0.35f);
            startPulse.a = model.IsTrail(model.Start) ? 1f : StandardTileOpacity;
            endPulse.a = model.IsTrail(model.End) ? 1f : StandardTileOpacity;
            if (!warnedCells.Contains(model.Start) && !targetedCells.Contains(model.Start))
            {
                mainTiles[model.Start.x, model.Start.y].color = startPulse;
            }
            if (!warnedCells.Contains(model.End) && !targetedCells.Contains(model.End))
            {
                mainTiles[model.End.x, model.End.y].color = endPulse;
            }
            if (!playerDead && !movementFrozen)
            {
                mainPlayerImage.color = Color.Lerp(activeCharacterTint, TrailHot, pulse * 0.08f);
            }
            for (int i = 0; i < crystalVisuals.Length; i++)
            {
                RectTransform crystal = crystalVisuals[i];
                if (crystal != null && crystal.gameObject.activeSelf)
                {
                    float crystalPulse = (Mathf.Sin(Time.unscaledTime * 7f + i * 1.4f) + 1f) * 0.5f;
                    crystal.localScale = Vector3.one * PixelStep(0.94f + crystalPulse * 0.14f, 0.04f);
                }
            }
        }

        private bool IsArenaBoundary(Vector2Int cell)
        {
            return !model.IsWalkable(cell + Vector2Int.up) ||
                !model.IsWalkable(cell + Vector2Int.right) ||
                !model.IsWalkable(cell + Vector2Int.down) ||
                !model.IsWalkable(cell + Vector2Int.left);
        }

        private void AnimateArenaAmbience()
        {
            if (!desktopLayout)
            {
                return;
            }

            float time = Time.unscaledTime;
            for (int i = 0; i < ambientParticles.Count; i++)
            {
                RectTransform particle = ambientParticles[i];
                Image image = ambientParticleImages[i];
                float wave = (Mathf.Sin(time * (1.7f + i % 4 * 0.23f) + i * 0.91f) + 1f) * 0.5f;
                particle.anchoredPosition = PixelSnap(new Vector2(
                    Mathf.Sin(time * 0.65f + i) * (2f + i % 5),
                    Mathf.Cos(time * 0.82f + i * 0.7f) * (3f + i % 7)));
                Color color = image.color;
                color.a = (i % 5 == 0 ? 0.18f : 0.12f) + wave * 0.34f;
                image.color = color;
                particle.localScale = Vector3.one * PixelStep(0.75f + wave * 0.65f, 0.125f);
            }

            float bossPulse = (Mathf.Sin(time * 3.6f) + 1f) * 0.5f;
            if (arenaBossCore != null)
            {
                arenaBossCore.localScale = Vector3.one * PixelStep(0.94f + bossPulse * 0.12f, 0.02f);
            }
        }

        private void AnimateAttackWarnings()
        {
            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    RectTransform warning = attackWarningVisuals[x, y];
                    Image fillImage = attackWarningFillImages[x, y];
                    if (warning == null || fillImage == null)
                    {
                        continue;
                    }

                    Vector2Int cell = new Vector2Int(x, y);
                    bool glyphWarning = warnedCells.Contains(cell);
                    bool targetWarning = targetedCells.Contains(cell);
                    bool crystalWarning = crystalWarningCounts.ContainsKey(cell);
                    bool visible = !titleActive && !tutorialActive && mainTiles[x, y].gameObject.activeInHierarchy &&
                        (glyphWarning || targetWarning || crystalWarning);
                    warning.gameObject.SetActive(visible);
                    if (!visible)
                    {
                        continue;
                    }

                    float progress = 0f;
                    if (glyphWarning)
                    {
                        progress = Mathf.Max(progress, hazardFiring ? 1f : hazardTelegraphProgress);
                    }
                    if (targetWarning)
                    {
                        progress = Mathf.Max(progress, targetedFiring ? 1f : targetedTelegraphProgress);
                    }
                    if (crystalWarning)
                    {
                        progress = Mathf.Max(progress, GetCrystalTelegraphProgress(cell));
                    }

                    progress = Mathf.Clamp01(progress);
                    float maxSize = Mathf.Max(0f, UseIsometricArena
                        ? warning.sizeDelta.x - 8f
                        : mainTiles[x, y].rectTransform.sizeDelta.x - 10f);
                    float size = PixelStep(maxSize * progress, 1f);
                    fillImage.rectTransform.sizeDelta = Vector2.one * size;
                    fillImage.rectTransform.anchoredPosition = Vector2.zero;
                    fillImage.color = new Color(Danger.r, Danger.g, Danger.b, 0.42f + progress * 0.48f);
                    warning.SetAsLastSibling();
                }
            }
            if (mainPlayer != null)
            {
                mainPlayer.SetAsLastSibling();
            }
        }

        private void AnimateSpecialItems()
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.5f;
            float scale = PixelStep(0.90f + pulse * 0.16f, 0.04f);
            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    RectTransform item = specialItemVisuals[x, y];
                    if (item != null && item.gameObject.activeInHierarchy)
                    {
                        item.localScale = Vector3.one * scale;
                        item.anchoredPosition = Vector2.zero;
                    }
                }
            }
        }

        private void SpawnStepParticles(Color color)
        {
            SpawnBurst(model.Player, color, 5);
        }

        private void SpawnBurst(Vector2Int cell, Color color, int count)
        {
            if (effectsLayer == null)
            {
                return;
            }

            Vector2 origin = tutorialActive
                ? TutorialGridPosition(cell)
                : GridPosition(cell.x, cell.y, mainCellSize);
            for (int i = 0; i < count; i++)
            {
                float angle = (360f / Mathf.Max(1, count)) * i + Random.Range(-16f, 16f);
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                StartCoroutine(AnimateParticle(origin, direction, color, Random.Range(8f, 17f), Random.Range(45f, 115f)));
            }
        }

        private IEnumerator AnimateParticle(Vector2 origin, Vector2 direction, Color color, float size, float distance)
        {
            RectTransform particle = CreateRect("Trail Spark", effectsLayer);
            particle.anchorMin = particle.anchorMax = new Vector2(0.5f, 0.5f);
            particle.sizeDelta = Vector2.one * size;
            particle.anchoredPosition = origin;
            particle.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image image = particle.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            for (float t = 0f; t < 1f; t += Time.deltaTime * 8.5f)
            {
                Vector2 position = origin + direction * (distance * Mathf.Sin(t * Mathf.PI * 0.55f));
                particle.anchoredPosition = PixelSnap(position);
                particle.localScale = Vector3.one * PixelStep(1f - t * 0.65f, 0.125f);
                image.color = new Color(color.r, color.g, color.b, PixelStep(1f - t, 0.2f));
                yield return null;
            }

            Destroy(particle.gameObject);
        }

        private void SpawnDirtLaneEruption(Vector2Int cell, float delay)
        {
            if (effectsLayer != null)
            {
                StartCoroutine(AnimateDirtLaneEruption(GridPosition(cell.x, cell.y, mainCellSize), delay));
            }
        }

        private IEnumerator AnimateDirtLaneEruption(Vector2 origin, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            Color darkSoil = Hex("3A261C");
            Color soil = Hex("754426");
            Color clay = Hex("B9783D");
            Color stone = Hex("8B8275");
            StartCoroutine(AnimateDustPuff(origin, darkSoil, mainCellSize * 0.52f, 0.29f));
            StartCoroutine(AnimateDustPuff(origin + Vector2.up * 8f, soil, mainCellSize * 0.40f, 0.25f));
            StartCoroutine(AnimateDustPuff(origin + new Vector2(-11f, 14f), clay, mainCellSize * 0.26f, 0.21f));
            StartCoroutine(AnimateDustPuff(origin + new Vector2(12f, 11f), stone, mainCellSize * 0.20f, 0.19f));

            for (int i = 0; i < 12; i++)
            {
                Color chunkColor = i % 4 == 0 ? stone : i % 2 == 0 ? clay : soil;
                Vector2 velocity = new Vector2(Random.Range(-58f, 58f), Random.Range(105f, 205f));
                StartCoroutine(AnimateDirtChunk(origin, velocity, chunkColor,
                    Random.Range(6f, 14f), Random.Range(320f, 430f), Random.Range(0.24f, 0.38f)));
            }
        }

        private void SpawnDirtAreaExplosion(Vector2Int cell)
        {
            if (effectsLayer == null)
            {
                return;
            }

            Vector2 origin = GridPosition(cell.x, cell.y, mainCellSize);
            Color darkSoil = Hex("342219");
            Color soil = Hex("754426");
            Color clay = Hex("C8894B");
            Color stone = Hex("9B9182");
            StartCoroutine(AnimateDirtShockwave(origin, clay));
            StartCoroutine(AnimateDustPuff(origin, darkSoil, mainCellSize * 0.88f, 0.42f));
            StartCoroutine(AnimateDustPuff(origin + Vector2.up * 12f, soil, mainCellSize * 0.72f, 0.37f));
            StartCoroutine(AnimateDustPuff(origin + Vector2.up * 30f, clay, mainCellSize * 0.52f, 0.32f));
            StartCoroutine(AnimateDustPuff(origin + new Vector2(-24f, 18f), darkSoil, mainCellSize * 0.36f, 0.29f));
            StartCoroutine(AnimateDustPuff(origin + new Vector2(24f, 22f), soil, mainCellSize * 0.38f, 0.27f));
            StartCoroutine(AnimateDustPuff(origin + new Vector2(0f, 42f), stone, mainCellSize * 0.28f, 0.24f));

            for (int i = 0; i < 40; i++)
            {
                float angle = (360f / 40f) * i + Random.Range(-7f, 7f);
                float speed = Random.Range(135f, 285f);
                Vector2 velocity = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad) * speed,
                    Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad)) * speed + Random.Range(55f, 130f));
                Color chunkColor = i % 5 == 0 ? stone : i % 2 == 0 ? clay : soil;
                StartCoroutine(AnimateDirtChunk(origin, velocity, chunkColor,
                    Random.Range(6f, 18f), Random.Range(390f, 520f), Random.Range(0.32f, 0.50f)));
            }
        }

        private IEnumerator AnimateDirtChunk(Vector2 origin, Vector2 velocity, Color color, float size,
            float gravity, float lifetime)
        {
            RectTransform chunk = CreateRect("Dirt Chunk", effectsLayer);
            chunk.anchorMin = chunk.anchorMax = new Vector2(0.5f, 0.5f);
            float pixelSize = Mathf.Max(8f, PixelStep(size, 4f));
            chunk.sizeDelta = new Vector2(pixelSize,
                Mathf.Max(8f, PixelStep(size * Random.Range(0.65f, 1.35f), 4f)));
            chunk.anchoredPosition = origin;
            Image image = chunk.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            float spin = Random.Range(-620f, 620f);

            for (float elapsed = 0f; elapsed < lifetime; elapsed += Time.deltaTime)
            {
                float t = elapsed / lifetime;
                Vector2 position = origin + velocity * elapsed + Vector2.down * (gravity * elapsed * elapsed * 0.5f);
                chunk.anchoredPosition = PixelSnap(position);
                chunk.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(spin * elapsed / 90f) * 90f);
                float scale = PixelStep(Mathf.Lerp(1f, 0.35f, t * t), 0.2f);
                chunk.localScale = Vector3.one * scale;
                float alpha = t < 0.58f ? 1f : 1f - (t - 0.58f) / 0.42f;
                image.color = new Color(color.r, color.g, color.b, PixelStep(Mathf.Clamp01(alpha), 0.2f));
                yield return null;
            }

            Destroy(chunk.gameObject);
        }

        private IEnumerator AnimateDustPuff(Vector2 origin, Color color, float targetSize, float lifetime)
        {
            RectTransform dust = CreateRect("Dirt Dust", effectsLayer);
            dust.anchorMin = dust.anchorMax = new Vector2(0.5f, 0.5f);
            dust.sizeDelta = Vector2.one * targetSize;
            dust.anchoredPosition = origin;
            dust.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f);
            Image image = dust.gameObject.AddComponent<Image>();
            image.color = new Color(color.r, color.g, color.b, 0.72f);
            image.raycastTarget = false;

            for (float elapsed = 0f; elapsed < lifetime; elapsed += Time.deltaTime)
            {
                float t = elapsed / lifetime;
                dust.anchoredPosition = PixelSnap(origin + Vector2.up * (t * 24f));
                float scaleX = PixelStep(Mathf.Lerp(0.22f, 1.55f, t), 0.2f);
                float scaleY = PixelStep(Mathf.Lerp(0.18f, 0.88f, t), 0.2f);
                dust.localScale = new Vector3(scaleX, scaleY, 1f);
                image.color = new Color(color.r, color.g, color.b,
                    PixelStep((1f - t) * 0.72f, 0.15f));
                yield return null;
            }

            Destroy(dust.gameObject);
        }

        private IEnumerator AnimateDirtShockwave(Vector2 origin, Color color)
        {
            RectTransform shockwave = CreateRect("Dirt Shockwave", effectsLayer);
            shockwave.anchorMin = shockwave.anchorMax = new Vector2(0.5f, 0.5f);
            shockwave.sizeDelta = Vector2.one * (mainCellSize * 0.72f);
            shockwave.anchoredPosition = origin;
            Image image = shockwave.gameObject.AddComponent<Image>();
            image.color = new Color(color.r, color.g, color.b, 0.16f);
            image.raycastTarget = false;
            Outline edge = shockwave.gameObject.AddComponent<Outline>();
            edge.effectDistance = new Vector2(4f, -4f);

            for (float t = 0f; t < 1f; t += Time.deltaTime * 6.5f)
            {
                shockwave.localScale = Vector3.one * PixelStep(Mathf.Lerp(0.28f, 2.35f, t), 0.2f);
                image.color = new Color(color.r, color.g, color.b,
                    PixelStep((1f - t) * 0.2f, 0.05f));
                edge.effectColor = new Color(color.r, color.g, color.b,
                    PixelStep((1f - t) * 0.95f, 0.2f));
                yield return null;
            }

            Destroy(shockwave.gameObject);
        }

        private IEnumerator ShakeField(float strength, float duration)
        {
            int version = ++fieldShakeVersion;
            Vector2 basePosition = mainGrid.anchoredPosition;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (version != fieldShakeVersion) yield break;
                float fade = 1f - elapsed / duration;
                Vector2 offset = Random.insideUnitCircle * (strength * fade);
                if (battleCameraInitialized && !tutorialActive && !hubActive)
                    battleCameraShake = offset;
                else
                    mainGrid.anchoredPosition = PixelSnap(basePosition + offset);
                yield return null;
            }
            if (version != fieldShakeVersion) yield break;
            battleCameraShake = Vector2.zero;
            if (!battleCameraInitialized || tutorialActive || hubActive)
                mainGrid.anchoredPosition = basePosition;
        }

        private IEnumerator PunchPlayer(bool blocked)
        {
            Vector2Int playerCell = hubActive ? hubModel.Player : model.Player;
            Vector2 basePosition = GridPosition(playerCell.x, playerCell.y, mainCellSize);
            if (blocked)
            {
                for (float t = 0f; t < 1f; t += Time.deltaTime * 8f)
                {
                    Vector2 nudge = Vector2.right * (Mathf.Sin(t * 38f) * (1f - t) * 13f);
                    if (battleCameraInitialized && !tutorialActive && !hubActive)
                        battlePlayerNudge = nudge;
                    else
                        mainPlayer.anchoredPosition = basePosition + nudge;
                    yield return null;
                }
                battlePlayerNudge = Vector2.zero;
                if (!battleCameraInitialized || tutorialActive || hubActive)
                    mainPlayer.anchoredPosition = basePosition;
            }
            else
            {
                for (float t = 0f; t < 1f; t += Time.deltaTime * 7f)
                {
                    float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.24f;
                    mainPlayer.localScale = Vector3.one * scale;
                    yield return null;
                }
                mainPlayer.localScale = Vector3.one;
            }
        }

        private IEnumerator AnimateAttackSlash()
        {
            Vector2 from = GridPosition(model.Start.x, model.Start.y, mainCellSize);
            Vector2 to = GridPosition(model.End.x, model.End.y, mainCellSize);
            Vector2 delta = to - from;
            attackSlash.anchoredPosition = (from + to) * 0.5f;
            attackSlash.sizeDelta = new Vector2(Mathf.Max(mainCellSize, delta.magnitude + mainCellSize), 22f);
            attackSlash.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            attackSlash.SetAsLastSibling();

            for (float t = 0f; t < 1f; t += Time.deltaTime * 9.5f)
            {
                float visible = Mathf.Sin(t * Mathf.PI);
                float scaleX = PixelStep(Mathf.SmoothStep(0.05f, 1f, Mathf.Min(1f, t * 2.4f)), 0.125f);
                float scaleY = PixelStep(1f + visible * 1.8f, 0.25f);
                attackSlash.localScale = new Vector3(scaleX, scaleY, 1f);
                attackSlashGroup.alpha = PixelStep(visible * 0.88f, 0.2f);
                yield return null;
            }

            attackSlashGroup.alpha = 0f;
            attackSlash.localScale = Vector3.one;
        }

        private IEnumerator ShakeHud()
        {
            Vector2 basePosition = bossHud.anchoredPosition;
            for (float t = 0f; t < 1f; t += Time.deltaTime * 7f)
            {
                float strength = (1f - t) * 16f;
                bossHud.anchoredPosition = basePosition + new Vector2(Mathf.Sin(t * 57f), Mathf.Cos(t * 43f)) * strength;
                yield return null;
            }
            bossHud.anchoredPosition = basePosition;
        }

        private IEnumerator FlashFrame(Color flash)
        {
            Color original = fieldFrame.color;
            fieldFrame.color = Color.Lerp(original, flash, 0.55f);
            yield return new WaitForSeconds(0.12f);
            fieldFrame.color = original;
        }

        private IEnumerator ShowDamagePopup()
        {
            damagePopupGroup.alpha = 1f;
            RectTransform rect = damagePopup.rectTransform;
            Vector2 start = rect.anchoredPosition;
            for (float t = 0f; t < 1f; t += Time.deltaTime * 2.8f)
            {
                damagePopupGroup.alpha = 1f - t;
                rect.anchoredPosition = start + Vector2.up * (t * 45f);
                yield return null;
            }
            rect.anchoredPosition = start;
            damagePopupGroup.alpha = 0f;
        }

        private void BuildSoundBank()
        {
            moveSfx = CreateSynthClip("step", 0.075f, new[] { 320f, 640f }, 0.01f, 0f);
            startSfx = CreateSynthClip("start", 0.22f, new[] { 420f, 630f, 840f }, 0f, 180f);
            blockedSfx = CreateSynthClip("blocked", 0.14f, new[] { 95f, 145f }, 0.16f, -35f);
            resetSfx = CreateSynthClip("reset", 0.36f, new[] { 240f, 360f }, 0.09f, -150f);
            attackSfx = CreateSynthClip("slash", 0.42f, new[] { 180f, 520f, 920f }, 0.12f, 520f);
            hitSfx = CreateSynthClip("impact", 0.48f, new[] { 62f, 105f, 210f }, 0.24f, -28f);
            victorySfx = CreateSynthClip("victory", 0.8f, new[] { 390f, 520f, 780f }, 0.015f, 360f);
            warningSfx = CreateSynthClip("warning", 0.28f, new[] { 220f, 440f }, 0.025f, 90f);
            laserSfx = CreateSynthClip("laser", 0.55f, new[] { 170f, 680f, 1120f }, 0.08f, 780f);
            explosionSfx = CreateSynthClip("explosion", 0.7f, new[] { 48f, 82f, 135f }, 0.34f, -40f);
            deathSfx = CreateSynthClip("death", 0.9f, new[] { 310f, 190f, 95f }, 0.1f, -210f);
            phaseTwoSfx = CreateSynthClip("phase-two", 1.15f, new[] { 72f, 144f, 288f, 576f }, 0.12f, 420f);
            targetLockSfx = CreateSynthClip("target-lock", 0.34f, new[] { 540f, 810f }, 0.025f, 260f);
            templeMusic = CreateTempleMusicLoop();
        }

        private AudioClip CreateTempleMusicLoop()
        {
            const int sampleRate = 22050;
            const float duration = 16f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            var random = new System.Random(7319);
            float[] melody =
            {
                146.83f, 174.61f, 196f, 220f,
                196f, 174.61f, 146.83f, 130.81f,
                146.83f, 196f, 220f, 261.63f,
                220f, 196f, 174.61f, 146.83f
            };

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                float normalized = (float)i / sampleCount;
                float loopFade = Mathf.Clamp01(Mathf.Min(normalized, 1f - normalized) * 90f);

                float dronePulse = 0.78f + Mathf.Sin(2f * Mathf.PI * 0.125f * time) * 0.22f;
                float drone = Mathf.Sin(2f * Mathf.PI * 73.415f * time) * 0.105f;
                drone += Mathf.Sin(2f * Mathf.PI * 110f * time) * 0.035f;

                const float stepLength = 0.5f;
                int step = Mathf.FloorToInt(time / stepLength) % melody.Length;
                float stepTime = time - Mathf.Floor(time / stepLength) * stepLength;
                float noteEnvelope = Mathf.Clamp01(stepTime / 0.025f) *
                    Mathf.Pow(1f - stepTime / stepLength, 2.2f);
                float noteFrequency = melody[step];
                float notePhase = 2f * Mathf.PI * noteFrequency * time;
                float note = (Mathf.Sin(notePhase) * 0.055f +
                    Mathf.Sign(Mathf.Sin(notePhase * 0.5f)) * 0.018f) * noteEnvelope;

                float bellTime = time % 2f;
                float bellEnvelope = Mathf.Exp(-bellTime * 3.1f);
                float bellFrequency = step % 4 == 0 ? 587.33f : 440f;
                float bell = (Mathf.Sin(2f * Mathf.PI * bellFrequency * time) +
                    Mathf.Sin(2f * Mathf.PI * bellFrequency * 2.01f * time) * 0.35f) *
                    bellEnvelope * 0.035f;

                float air = ((float)random.NextDouble() * 2f - 1f) * 0.0045f;
                samples[i] = Mathf.Clamp((drone * dronePulse + note + bell + air) * loopFade,
                    -0.82f, 0.82f);
            }

            AudioClip clip = AudioClip.Create("ivy-temple-loop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateSynthClip(string clipName, float duration, float[] frequencies, float noise, float sweep)
        {
            int sampleRate = 22050;
            int sampleCount = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
            var samples = new float[sampleCount];
            var random = new System.Random(clipName.GetHashCode());
            for (int i = 0; i < sampleCount; i++)
            {
                float normalized = (float)i / sampleCount;
                float time = (float)i / sampleRate;
                float attack = Mathf.Clamp01(normalized / 0.055f);
                float release = Mathf.Pow(1f - normalized, clipName == "victory" ? 0.7f : 1.8f);
                float signal = 0f;
                foreach (float baseFrequency in frequencies)
                {
                    float frequency = Mathf.Max(35f, baseFrequency + sweep * normalized);
                    signal += Mathf.Sin(2f * Mathf.PI * frequency * time) / frequencies.Length;
                }

                float noiseSample = ((float)random.NextDouble() * 2f - 1f) * noise * (1f - normalized);
                samples[i] = Mathf.Clamp((signal * 0.5f + noiseSample) * attack * release, -0.92f, 0.92f);
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlaySfx(AudioClip clip, float pitch = 1f, float volume = 1f)
        {
            if (clip == null)
            {
                return;
            }

            audioSource.pitch = Mathf.Clamp(pitch, 0.65f, 1.8f);
            audioSource.PlayOneShot(clip, volume);
        }

        private IEnumerator CaptureOnCommandLine()
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            int flagIndex = System.Array.IndexOf(arguments, "-capturePath");
            if (flagIndex < 0 || flagIndex + 1 >= arguments.Length)
            {
                yield break;
            }

            Application.runInBackground = true;
            if (System.Array.IndexOf(arguments, "-captureMinimap") >= 0)
            {
                model.TryPlacePlayer(new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2));
                battleCameraInitialized = false;
                RefreshBoard();
                yield return null;
                Vector2 panelPosition = minimapRoot.anchoredPosition;
                Vector2 markerPosition = minimapPlayer.anchoredPosition;
                Move(Vector2Int.right);
                // Inspect after LateUpdate has actually advanced the marker, including slow startup frames.
                for (int frame = 0; frame < 60; frame++)
                {
                    yield return null;
                    if (Vector2.Distance(minimapPlayer.anchoredPosition - markerPosition,
                        Vector2.right * MinimapCellSize) <= 0.1f) break;
                }
                if (minimapRoot.anchoredPosition != panelPosition ||
                    Vector2.Distance(minimapPlayer.anchoredPosition - markerPosition,
                        Vector2.right * MinimapCellSize) > 0.1f)
                    Debug.LogError("Minimap movement validation failed: marker delta " +
                        (minimapPlayer.anchoredPosition - markerPosition) + ", panel " + minimapRoot.anchoredPosition);
                else
                    Debug.Log("Minimap movement validation passed: player tracks one cell; panel stays fixed.");
            }
            else if (System.Array.IndexOf(arguments, "-captureArenaOverview") >= 0)
            {
                // Preview-only overview; normal play retains the close follow camera.
                battleCameraInitialized = false;
                mainGrid.anchoredPosition = Vector2.zero;
                mainGrid.localScale = Vector3.one * (LegacyDesktopGridSize / mainGrid.sizeDelta.x);
            }
            else if (System.Array.IndexOf(arguments, "-captureObjectiveStart") >= 0 ||
                System.Array.IndexOf(arguments, "-captureObjectiveEnd") >= 0)
            {
                bool endTarget = System.Array.IndexOf(arguments, "-captureObjectiveEnd") >= 0;
                var center = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);
                if (endTarget)
                {
                    Vector2Int start = model.Start;
                    model.TryPlacePlayer(start + Vector2Int.up);
                    model.TryMove(Vector2Int.down);
                    for (int step = 0; step < TrailFieldModel.Size && model.Player.y < center.y; step++)
                        model.TryMove(Vector2Int.up);
                    for (int step = 0; step < TrailFieldModel.Size && model.Player.x != center.x; step++)
                        model.TryMove(model.Player.x < center.x ? Vector2Int.right : Vector2Int.left);
                }
                else
                {
                    model.TryPlacePlayer(center);
                }
                battleCameraInitialized = false;
                RefreshBoard();
                UpdateObjectiveArrow();
                Vector2 target = GridPosition(model.NavigationTarget.x, model.NavigationTarget.y, mainCellSize);
                Vector2 expectedDirection = (target - mainPlayer.anchoredPosition).normalized;
                Vector2 shownDirection = (objectiveArrow.anchoredPosition - mainPlayer.anchoredPosition).normalized;
                if (model.IsTracing != endTarget || !objectiveArrow.gameObject.activeSelf ||
                    Vector2.Dot(expectedDirection, shownDirection) < 0.99f)
                    Debug.LogError("Objective arrow validation failed.");
                else
                    Debug.Log("Objective arrow validation passed: target " + (endTarget ? "END" : "START") +
                        ", cell " + model.NavigationTarget);
            }
            else if (System.Array.IndexOf(arguments, "-captureCameraFollow") >= 0)
            {
                var center = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);
                model.TryPlacePlayer(center);
                battleCameraInitialized = false;
                RefreshBoard();
                Vector2 initialTarget = battleCameraTarget;
                Move(Vector2Int.up);
                if (battlePlayerVisualPosition == battlePlayerMoveTarget)
                    Debug.LogError("Player interpolation validation failed: move snapped instantly.");
                yield return null;
                Move(Vector2Int.right);
                yield return StartCoroutine(ShakeField(12f, 0.18f));
                for (float elapsed = 0f; elapsed < 0.9f; elapsed += Time.unscaledDeltaTime)
                    yield return null;
                float error = Vector2.Distance(battleCameraPosition, battleCameraTarget);
                if (Vector2.Distance(battlePlayerVisualPosition, battlePlayerMoveTarget) > 0.5f ||
                    model.Player != center + Vector2Int.up + Vector2Int.right ||
                    Vector2.Distance(initialTarget, battleCameraTarget) < 1f || error > 1f)
                    Debug.LogError("Camera follow validation failed: target error " + error);
                else
                    Debug.Log("Camera follow validation passed: smooth grid moves, shake recovery, error " + error);
            }
            else if (System.Array.IndexOf(arguments, "-captureCameraEdge") >= 0)
            {
                model.TryPlacePlayer(new Vector2Int(TrailFieldModel.Size / 2, 0));
                battleCameraInitialized = false;
                RefreshBoard();
                Vector2 renderedPlayer = battleCameraTarget + battlePlayerMoveTarget;
                Vector2 halfView = (desktopLayout
                    ? new Vector2(DesktopReferenceWidth, DesktopReferenceHeight)
                    : ((RectTransform)mainGrid.parent).rect.size) * 0.5f;
                Vector2 halfPlayer = mainPlayer.sizeDelta * 0.5f;
                if (Mathf.Abs(renderedPlayer.x) + halfPlayer.x > halfView.x ||
                    Mathf.Abs(renderedPlayer.y) + halfPlayer.y > halfView.y)
                    Debug.LogError("Camera edge validation failed: player outside viewport.");
                else
                    Debug.Log("Camera edge validation passed: full player visible at arena boundary.");
            }
            else if (System.Array.IndexOf(arguments, "-captureEndpoints") >= 0 && !tutorialActive)
            {
                model.TryMove(Vector2Int.up);
                RefreshBoard();
            }
            else if (System.Array.IndexOf(arguments, "-captureHubCharacterInfo") >= 0 && hubActive)
            {
                HubMove(Vector2Int.up);
            }
            else if (System.Array.IndexOf(arguments, "-captureTitleReveal") >= 0 && titleActive)
            {
                yield return StartCoroutine(RevealTitleScreen(Vector2Int.right));
            }
            else if (System.Array.IndexOf(arguments, "-capturePhaseTransition") >= 0)
            {
                phaseOverlayRoot.SetAsLastSibling();
                phaseBannerGroup.alpha = 1f;
                phasePageLeft.localScale = Vector3.one;
                phasePageRight.localScale = Vector3.one;
                phaseBanner.text = "PHASE 2\nENRAGED";
                phaseBanner.color = Danger;
                phaseBanner.rectTransform.localScale = Vector3.one;
            }
            else if (System.Array.IndexOf(arguments, "-captureTelegraph") >= 0)
            {
                phaseTwoActive = true;
                SetupFixedCrystals();
                bossMaxHealth = BossPatternRules.PhaseMaxHealth(true);
                bossHealth = bossMaxHealth;
                bossHealthFill.fillAmount = 1f;
                bossHealthText.text = bossHealth + " / " + bossMaxHealth;
                warnedCells.Clear();
                var captureCenter = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);
                warnedCells.UnionWith(BossPatternRules.CreateHorizontalGrid(model.Traversable, captureCenter));
                hazardTelegraphProgress = 0.62f;
                UpdatePhaseLabel();
                statusText.text = "공격 예고 진행 — 중앙 붉은 사각형이 테두리까지 확장됩니다";
                RefreshBoard();
            }
            else if (System.Array.IndexOf(arguments, "-captureHazard") >= 0)
            {
                phaseTwoActive = true;
                SetupFixedCrystals();
                bossMaxHealth = BossPatternRules.PhaseMaxHealth(true);
                bossHealth = bossMaxHealth;
                bossHealthFill.fillAmount = 1f;
                bossHealthText.text = bossHealth + " / " + bossMaxHealth;
                warnedCells.Clear();
                var captureCenter = new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2);
                warnedCells.UnionWith(BossPatternRules.CreateHorizontalGrid(model.Traversable, captureCenter));
                targetedCells.Clear();
                targetedCells.Add(model.Player);
                UpdatePhaseLabel();
                statusText.text = "◉ 위치 추적 폭발 0.6초 · 가로 격자 예고";
                RefreshBoard();
                foreach (Vector2Int cell in warnedCells)
                {
                    SpawnDirtLaneEruption(cell, 0f);
                }
                SpawnDirtAreaExplosion(model.Player);
            }
            else if (System.Array.IndexOf(arguments, "-captureCrystal") >= 0)
            {
                phaseTwoActive = true;
                SetupFixedCrystals();
                bossMaxHealth = BossPatternRules.PhaseMaxHealth(true);
                bossHealth = bossMaxHealth;
                bossHealthFill.fillAmount = 1f;
                bossHealthText.text = bossHealth + " / " + bossMaxHealth;
                if (crystalCells.Count > 0)
                {
                    HashSet<Vector2Int> previewBlast =
                        CrystalRules.CreateCheckerBlast(model.Traversable, crystalCells[0]);
                    AddCellCounts(crystalWarningCounts, previewBlast);
                    SetCrystalTelegraphProgress(previewBlast, ++crystalTelegraphSequence, 0.62f);
                }
                UpdatePhaseLabel();
                statusText.text = "수정 격자 폭발 예고 — 주변 2칸 체크무늬";
                RefreshBoard();
            }
            string capturePath = Path.GetFullPath(arguments[flagIndex + 1]);
            string directory = Path.GetDirectoryName(capturePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            int captureWidth = desktopLayout ? 1600 : 540;
            int captureHeight = desktopLayout ? 900 : 960;
            var renderTexture = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
            uiCamera.targetTexture = renderTexture;

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            Canvas.ForceUpdateCanvases();
            if (System.Array.IndexOf(arguments, "-validateMinimap") >= 0)
                ValidateMinimapCapture();
            uiCamera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var screenshot = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(capturePath, screenshot.EncodeToPNG());
            RenderTexture.active = previous;
            uiCamera.targetTexture = null;
            renderTexture.Release();
            Destroy(screenshot);
            Destroy(renderTexture);
            yield return null;
            Application.Quit();
        }

        private void ValidateMinimapCapture()
        {
            var errors = new List<string>();
            bool shouldShow = desktopLayout && !titleActive && !hubActive;
            if (minimapRoot == null)
            {
                Debug.LogError("Minimap validation failed: missing desktop minimap.");
                return;
            }
            if (minimapRoot.gameObject.activeSelf != shouldShow) errors.Add("visibility");
            if (!Mathf.Approximately(minimapRoot.rect.width, minimapRoot.rect.height)) errors.Add("not square");
            if (minimapRoot.anchorMin != Vector2.one || minimapRoot.anchorMax != Vector2.one)
                errors.Add("not top-right anchored");
            float opacity = minimapRoot.GetComponent<Image>().color.a;
            if (!Mathf.Approximately(opacity, 0.30f)) errors.Add("backdrop opacity is not 30%");
            if (minimapRoot.GetComponent<CanvasGroup>().blocksRaycasts) errors.Add("blocks input");
            foreach (Graphic graphic in minimapRoot.GetComponentsInChildren<Graphic>(true))
                if (graphic.raycastTarget) errors.Add("raycast target: " + graphic.name);

            var corners = new Vector3[4];
            minimapRoot.GetWorldCorners(corners);
            RectTransform parent = (RectTransform)minimapRoot.parent;
            foreach (Vector3 corner in corners)
                if (!parent.rect.Contains(parent.InverseTransformPoint(corner))) errors.Add("outside viewport");

            if (shouldShow && !tutorialActive)
            {
                int activeCells = 0;
                foreach (Image tile in minimapTiles)
                    if (tile.gameObject.activeSelf) activeCells++;
                if (activeCells != model.Walkable.Count) errors.Add("incomplete map");
                if (minimapTiles[model.Start.x, model.Start.y].color != StartColor) errors.Add("start marker");
                if (minimapTiles[model.End.x, model.End.y].color != EndColor) errors.Add("end marker");
                Vector2 expectedPlayer = battlePlayerVisualPosition * (MinimapCellSize / mainCellSize);
                if (Vector2.Distance(minimapPlayer.anchoredPosition, expectedPlayer) > 0.1f)
                    errors.Add("player marker position");
            }

            if (errors.Count > 0) Debug.LogError("Minimap validation failed: " + string.Join(", ", errors));
            else Debug.Log("Minimap validation passed: square, top-right, 30% backdrop, input pass-through; " +
                (shouldShow ? "full map and navigation markers." : "hidden on title/hub."));
        }

        private static Vector2 GridPosition(int x, int y, float size)
        {
            float center = (TrailFieldModel.Size - 1) * 0.5f;
            if (UseIsometricArena)
            {
                return new Vector2(
                    (x - y) * size * 0.5f,
                    (x + y - center * 2f) * size * 0.5f);
            }

            return new Vector2((x - center) * size, (y - center) * size);
        }

        private static Vector2 PixelSnap(Vector2 value)
        {
            const float pixelUnit = 4f;
            return new Vector2(Mathf.Round(value.x / pixelUnit) * pixelUnit,
                Mathf.Round(value.y / pixelUnit) * pixelUnit);
        }

        private static float PixelStep(float value, float step)
        {
            return Mathf.Round(value / step) * step;
        }

        private RectTransform CreatePlayer(string name, RectTransform parent, float size, bool mini)
        {
            RectTransform player = CreateRect(name, parent);
            player.anchorMin = player.anchorMax = new Vector2(0.5f, 0.5f);
            player.sizeDelta = Vector2.one * size;
            Image image = player.gameObject.AddComponent<Image>();
            image.color = White;
            image.preserveAspect = true;

            if (!mini)
            {
                Texture2D warriorTexture = Resources.Load<Texture2D>("Art/warrior_front");
                if (warriorTexture != null)
                {
                    warriorTexture.filterMode = FilterMode.Point;
                    warriorTexture.wrapMode = TextureWrapMode.Clamp;
                    image.sprite = Sprite.Create(warriorTexture,
                        new Rect(0f, 0f, warriorTexture.width, warriorTexture.height),
                        new Vector2(0.5f, 0.5f), 32f);
                }
                mainPlayerImage = image;
            }

            if (mini)
            {
                CreateText("Icon", player, "◆", 14, FontStyle.Bold, Background,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }
            return player;
        }

        private RectTransform CreatePanel(string name, Transform parent, Color color, Vector2 min, Vector2 max)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private RectTransform CreateImage(string name, Transform parent, Color color, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private Text CreateText(string name, Transform parent, string value, int size, FontStyle style, Color color,
            Vector2 min, Vector2 max, TextAnchor alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = gameFont;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void AddAccent(RectTransform parent, Vector2 min, Vector2 max, Color color)
        {
            RectTransform accent = CreateRect("Accent", parent);
            accent.anchorMin = min;
            accent.anchorMax = max;
            accent.offsetMin = Vector2.zero;
            accent.offsetMax = Vector2.zero;
            accent.gameObject.AddComponent<Image>().color = color;
        }

        private static void ApplySafeArea(RectTransform root)
        {
            Rect safe = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }
            root.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            root.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }
    }
}
