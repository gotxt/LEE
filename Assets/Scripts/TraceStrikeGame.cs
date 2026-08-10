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
        private const float SwipeThreshold = 55f;
        private const int StandaloneWidth = 540;
        private const int StandaloneHeight = 960;
        private const float PortraitAspect = 9f / 16f;
        private const float TutorialCellSize = 180f;
        private const float TutorialPlayerSizeRatio = 0.68f;
        private const float BattlePlayerSizeRatio = 0.58f;
        private const float TargetedWarningSeconds = 0.65f;
        private const string BestClearTimeKey = "TraceStrike.BestClearTime";

        private static readonly Color Background = Hex("101525");
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

        private readonly TrailFieldModel model = new TrailFieldModel();
        private readonly Image[,] mainTiles = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Text[,] tileLabels = new Text[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly RectTransform[,] attackWarningVisuals = new RectTransform[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Image[,] attackWarningFillImages = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Image[,] endpointMarkerImages = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly RectTransform[,] specialItemVisuals = new RectTransform[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Image[,] specialItemImages = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Image[,] specialItemIconImages = new Image[TrailFieldModel.Size, TrailFieldModel.Size];
        private readonly Text[,] specialItemLabels = new Text[TrailFieldModel.Size, TrailFieldModel.Size];
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

        private Font gameFont;
        private RectTransform mainGrid;
        private RectTransform mainPlayer;
        private RectTransform effectsLayer;
        private RectTransform directionPadArea;
        private RectTransform attackSlash;
        private RectTransform bossHud;
        private RectTransform titleScreen;
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

        private Vector2 pressPosition;
        private bool pointerDown;
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
        private Vector2Int tutorialPlayer;
        private float mainCellSize;
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
            Screen.orientation = ScreenOrientation.Portrait;
#if UNITY_STANDALONE && !UNITY_EDITOR
            Screen.SetResolution(StandaloneWidth, StandaloneHeight, FullScreenMode.Windowed, 60);
#endif
            gameFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 48);

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.5f;
            BuildSoundBank();

            BuildInterface();
            RefreshFixedAspect();
            string[] launchArguments = System.Environment.GetCommandLineArgs();
            bool captureMode = System.Array.IndexOf(launchArguments, "-capturePath") >= 0;
            bool captureTutorial = System.Array.IndexOf(launchArguments, "-captureTutorial") >= 0;
            bool captureTitle = System.Array.IndexOf(launchArguments, "-captureTitle") >= 0;
            if (captureMode && captureTitle)
            {
                ShowTitleScreen();
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
                ShowTitleScreen();
            }
            StartCoroutine(BossPatternLoop());
            StartCoroutine(CrystalPatternLoop());
            StartCoroutine(CaptureOnCommandLine());
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
            if (screenAspect > PortraitAspect)
            {
                viewport.width = PortraitAspect / screenAspect;
                viewport.x = (1f - viewport.width) * 0.5f;
            }
            else if (screenAspect < PortraitAspect)
            {
                viewport.height = screenAspect / PortraitAspect;
                viewport.y = (1f - viewport.height) * 0.5f;
            }
            return viewport;
        }

        private void ReadKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                Move(Vector2Int.up);
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                Move(Vector2Int.right);
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                Move(Vector2Int.down);
            else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                Move(Vector2Int.left);
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

        private void ShowTitleScreen()
        {
            titleActive = true;
            inputLocked = true;
            movementFrozen = false;
            stageTimerRunning = false;
            if (titleScreen != null)
            {
                titleScreen.gameObject.SetActive(true);
                titleScreen.SetAsLastSibling();
            }
            UpdateTitleRecord();
        }

        private void HandleTitleInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                BeginChallenge();
            }
        }

        private void BeginChallenge()
        {
            if (!titleActive)
            {
                return;
            }

            titleActive = false;
            if (titleScreen != null)
            {
                titleScreen.gameObject.SetActive(false);
            }
            StartTutorial();
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
            mainPlayer.sizeDelta = Vector2.one * (TutorialCellSize * TutorialPlayerSizeRatio);
            mainPlayer.localScale = Vector3.one;
            crystalCells.Clear();
            specialTiles.Clear();
            RefreshCrystalVisuals();

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
            int activeSpecialStep = tutorialStep - 1;
            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    bool active = x < TutorialRules.Size && y < TutorialRules.Size;
                    mainTiles[x, y].gameObject.SetActive(active);
                    if (!active)
                    {
                        continue;
                    }

                    var cell = new Vector2Int(x, y);
                    RectTransform tile = mainTiles[x, y].rectTransform;
                    tile.sizeDelta = Vector2.one * (TutorialCellSize - 7f);
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
                    color.a = 0.5f;
                    mainTiles[x, y].color = color;
                    SetSpecialItemVisual(x, y, showSpecialItem, shownSpecialType, TutorialCellSize * 0.34f);
                    SetEndpointMarkerVisual(x, y,
                        tutorialStep == 0 && cell == TutorialRules.Start,
                        tutorialStep == 0 && cell == TutorialRules.End,
                        TutorialCellSize * 0.64f);
                    tileLabels[x, y].fontSize = 24;
                    tileLabels[x, y].text = marker;
                }
            }
            mainPlayer.anchoredPosition = TutorialGridPosition(tutorialPlayer);
            mainPlayer.SetAsLastSibling();
        }

        private static Vector2 TutorialGridPosition(Vector2Int cell)
        {
            float center = (TutorialRules.Size - 1) * 0.5f;
            return new Vector2((cell.x - center) * TutorialCellSize, (cell.y - center) * TutorialCellSize);
        }

        private IEnumerator AnimateTutorialSlash()
        {
            int version = tutorialVersion;
            Vector2 from = TutorialGridPosition(TutorialRules.Start);
            Vector2 to = TutorialGridPosition(TutorialRules.End);
            Vector2 delta = to - from;
            attackSlash.anchoredPosition = (from + to) * 0.5f;
            attackSlash.sizeDelta = new Vector2(delta.magnitude + TutorialCellSize, 26f);
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
            int baseDamage = CalculateDamage(model.Trail.Count);
            int damage = SpecialTileRules.ApplyDamageModifiers(baseDamage, nextAttackFlatBonus, nextAttackMultiplier);
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
            if (titleScreen != null)
            {
                titleScreen.gameObject.SetActive(false);
            }
            tutorialActive = false;
            tutorialTransitioning = false;
            stage = 0;
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
            RestoreMainGridLayout();
            model.CreateField(0);
            model.SetBlockedCells(crystalCells);
            model.BeginRound(round);
            GenerateSpecialTiles();
            RefreshCrystalVisuals();

            bossNameText.text = "크림슨 골렘";
            stageText.text = "STAGE 01";
            playerHealthText.text = "♥  HP 1";
            playerHealthText.color = StartColor;
            fieldTitleText.text = "CRYSTAL CAVERN";
            bossHealthFill.color = Danger;
            phaseBanner.color = Danger;
            mainPlayer.localRotation = Quaternion.identity;
            mainPlayer.sizeDelta = Vector2.one * (mainCellSize * BattlePlayerSizeRatio);
            mainPlayer.localScale = Vector3.one;
            mainPlayerImage.color = White;
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

        private void RestoreMainGridLayout()
        {
            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    RectTransform tile = mainTiles[x, y].rectTransform;
                    tile.sizeDelta = Vector2.one * (mainCellSize - 5f);
                    tile.anchoredPosition = GridPosition(x, y, mainCellSize);
                    tileLabels[x, y].fontSize = 24;
                }
            }
        }

        private void GenerateSpecialTiles()
        {
            var excluded = new HashSet<Vector2Int>
            {
                model.Player,
                model.Start,
                model.End
            };
            foreach (Vector2Int crystal in crystalCells)
            {
                excluded.Add(crystal);
            }
            specialTiles.Clear();
            int seed = stage * 1000 + round * 37 + (phaseTwoActive ? 503 : 0);
            foreach (KeyValuePair<Vector2Int, SpecialTileType> tile in
                     SpecialTileRules.Generate(model.Traversable, excluded, seed))
            {
                specialTiles[tile.Key] = tile.Value;
            }
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
            bool playerHit = blast.Contains(model.Player);
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
            yield return new WaitForSeconds(0.14f);

            RemoveCellCounts(crystalFiringCounts, blast);
            RemoveCellCounts(crystalWarningCounts, blast);
            RemoveCrystalTelegraphProgress(blast, telegraphId);
            RefreshBoard();
            if (playerHit && !playerDead)
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
            while (titleActive || tutorialActive)
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

            phaseBanner.text = "PHASE 2\nENRAGED";
            phaseBannerGroup.alpha = 1f;
            phaseBanner.rectTransform.localScale = Vector3.one * 0.45f;
            statusText.text = "PHASE 2 — 공격 수정 4개와 격자 문양이 활성화됩니다";
            PlaySfx(phaseTwoSfx);
            StartCoroutine(ShakeHud());
            StartCoroutine(FlashFrame(Danger));

            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                float normalized = t / 1.2f;
                phaseBanner.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1f, Mathf.SmoothStep(0f, 1f, normalized * 2f));
                phaseBannerGroup.alpha = normalized < 0.72f ? 1f : 1f - (normalized - 0.72f) / 0.28f;
                yield return null;
            }

            phaseBannerGroup.alpha = 0f;
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
                    int distance = diamondUseCount % 2 == 0 ? 3 : 5;
                    diamondUseCount++;
                    glyph = BossPatternRules.CreateDiamondGlyph(model.Traversable, center, distance);
                    patternName = "마름모 문양 " + distance;
                    break;
                case 2:
                    glyph = BossPatternRules.CreateDiagonalGlyph(model.Traversable, center);
                    patternName = "X 문양";
                    break;
                case 3:
                    glyph = BossPatternRules.CreateCombinedGlyph(model.Traversable, center, 3);
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
            bool playerHit = warnedCells.Contains(model.Player);
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
            yield return new WaitForSeconds(0.07f);

            hazardFiring = false;
            warnedCells.Clear();
            hazardTelegraphProgress = 0f;
            RefreshBoard();

            if (playerHit)
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
            bool playerHit = targetedCells.Contains(model.Player);
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

            targetedFiring = false;
            targetedCells.Clear();
            targetedTelegraphProgress = 0f;
            RefreshBoard();

            if (playerHit)
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

        private int CalculateDamage(int trailLength)
        {
            int steps = Mathf.Max(1, trailLength - 1);
            return 8 + steps * 5 + Mathf.Max(0, steps - 8) * 2;
        }

        private void BuildInterface()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.gameObject.SetActive(false);
            }

            GameObject canvasObject = new GameObject("Portrait UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            GameObject cameraObject = new GameObject("UI Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            uiCamera = cameraObject.GetComponent<Camera>();
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = Background;
            uiCamera.orthographic = true;
            uiCamera.nearClipPlane = 0.1f;
            uiCamera.farClipPlane = 100f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 10f;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
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
            CreateImage("Background", root, Background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            BuildHeader(root);
            BuildMainField(root);
            BuildFooterV2(root);
            BuildPhaseOverlay(root);
            BuildTitleScreen(root);
        }

        private void BuildTitleScreen(RectTransform root)
        {
            titleScreen = CreatePanel("Title Screen", root, Background, Vector2.zero, Vector2.one);

            Texture2D caveTexture = Resources.Load<Texture2D>("Art/cave_arena_background");
            if (caveTexture != null)
            {
                RectTransform backdrop = CreateImage("Title Cave Backdrop", titleScreen, White,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Image backdropImage = backdrop.GetComponent<Image>();
                backdropImage.sprite = Sprite.Create(caveTexture,
                    new Rect(0f, 0f, caveTexture.width, caveTexture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                backdropImage.preserveAspect = false;
                backdropImage.color = new Color(0.55f, 0.72f, 0.82f, 0.34f);
                backdropImage.raycastTarget = false;
            }

            CreatePanel("Title Veil", titleScreen,
                new Color(Background.r, Background.g, Background.b, 0.82f), Vector2.zero, Vector2.one);
            CreateImage("Top Cyan Line", titleScreen, Trail,
                new Vector2(0f, 0.975f), Vector2.one, Vector2.zero, Vector2.zero);

            CreateText("Title Kicker", titleScreen, "CRYSTAL CAVERN · BOSS RUSH", 23,
                FontStyle.Bold, Trail, new Vector2(0.08f, 0.91f), new Vector2(0.92f, 0.955f),
                TextAnchor.MiddleCenter);
            CreateText("Game Title", titleScreen, "TRACE STRIKE", 72,
                FontStyle.Bold, White, new Vector2(0.07f, 0.815f), new Vector2(0.93f, 0.92f),
                TextAnchor.MiddleCenter);
            CreateText("Title Copy", titleScreen, "한 칸의 선택이 공격 경로가 된다", 24,
                FontStyle.Normal, Muted, new Vector2(0.08f, 0.785f), new Vector2(0.92f, 0.825f),
                TextAnchor.MiddleCenter);

            RectTransform recordPanel = CreatePanel("Best Record", titleScreen,
                new Color(Panel.r, Panel.g, Panel.b, 0.97f),
                new Vector2(0.10f, 0.625f), new Vector2(0.90f, 0.775f));
            Outline recordOutline = recordPanel.gameObject.AddComponent<Outline>();
            recordOutline.effectColor = Trail;
            recordOutline.effectDistance = new Vector2(3f, -3f);
            AddAccent(recordPanel, Vector2.zero, new Vector2(0.018f, 1f), Trail);
            CreateText("Record Label", recordPanel, "최고 기록 · 크림슨 골렘", 24,
                FontStyle.Bold, Muted, new Vector2(0.05f, 0.68f), new Vector2(0.64f, 0.95f),
                TextAnchor.MiddleLeft);
            bestRecordText = CreateText("Best Time", recordPanel, "기록 없음", 50,
                FontStyle.Bold, White, new Vector2(0.05f, 0.10f), new Vector2(0.66f, 0.70f),
                TextAnchor.MiddleLeft);
            RectTransform rankBadge = CreatePanel("Rank Badge", recordPanel, Hex("0D1628"),
                new Vector2(0.68f, 0.16f), new Vector2(0.95f, 0.72f));
            bestRatingText = CreateText("Best Rank", rankBadge, "RANK  —", 29,
                FontStyle.Bold, Muted, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            RectTransform guidePanel = CreatePanel("Game Guide", titleScreen,
                new Color(Panel.r, Panel.g, Panel.b, 0.97f),
                new Vector2(0.06f, 0.255f), new Vector2(0.94f, 0.595f));
            Outline guideOutline = guidePanel.gameObject.AddComponent<Outline>();
            guideOutline.effectColor = new Color(Trail.r, Trail.g, Trail.b, 0.72f);
            guideOutline.effectDistance = new Vector2(3f, -3f);
            AddAccent(guidePanel, Vector2.zero, new Vector2(0.014f, 1f), EndColor);
            CreateText("Guide Header", guidePanel, "게임 안내", 31,
                FontStyle.Bold, White, new Vector2(0.045f, 0.87f), new Vector2(0.40f, 0.98f),
                TextAnchor.MiddleLeft);
            CreateText("Attack Guide", guidePanel,
                "검 시작 타일 → 중복 없이 경로 연결 → 깃발 종료 타일 도착 시 공격\n경로가 길수록 피해 증가 · 붉은 공격 예고 중에도 이동 가능",
                21, FontStyle.Normal, White,
                new Vector2(0.045f, 0.68f), new Vector2(0.96f, 0.88f), TextAnchor.MiddleLeft);

            BuildTitleSpecialGuide(guidePanel, "Power Guide", SpecialTileType.Power,
                "더하기", "다음 공격 +25", new Vector2(0.04f, 0.37f), new Vector2(0.49f, 0.65f));
            BuildTitleSpecialGuide(guidePanel, "Amplify Guide", SpecialTileType.Amplify,
                "곱셈", "다음 공격 ×1.35", new Vector2(0.51f, 0.37f), new Vector2(0.96f, 0.65f));
            BuildTitleSpecialGuide(guidePanel, "Pause Guide", SpecialTileType.Mud,
                "정지", "이동 1초 정지", new Vector2(0.04f, 0.06f), new Vector2(0.49f, 0.34f));
            BuildTitleSpecialGuide(guidePanel, "Down Guide", SpecialTileType.Curse,
                "다운", "다음 공격 ×0.65", new Vector2(0.51f, 0.06f), new Vector2(0.96f, 0.34f));

            RectTransform challenge = CreatePanel("Challenge Button", titleScreen, StartColor,
                new Vector2(0.18f, 0.085f), new Vector2(0.82f, 0.205f));
            Outline buttonOutline = challenge.gameObject.AddComponent<Outline>();
            buttonOutline.effectColor = White;
            buttonOutline.effectDistance = new Vector2(4f, -4f);
            Button button = challenge.gameObject.AddComponent<Button>();
            button.targetGraphic = challenge.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 0.78f, 1f);
            colors.pressedColor = new Color(0.65f, 0.88f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(BeginChallenge);
            CreateText("Challenge Label", challenge, "도전하기", 39,
                FontStyle.Bold, Background, new Vector2(0f, 0.30f), Vector2.one,
                TextAnchor.MiddleCenter);
            CreateText("Challenge Hint", challenge, "TAP  /  ENTER", 18,
                FontStyle.Bold, Hex("183B37"), Vector2.zero, new Vector2(1f, 0.36f),
                TextAnchor.MiddleCenter);
            CreateText("Title Footer", titleScreen, "최단 시간은 정상 플레이 완료 시 자동 저장됩니다",
                19, FontStyle.Normal, Muted, new Vector2(0.08f, 0.025f), new Vector2(0.92f, 0.07f),
                TextAnchor.MiddleCenter);

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
            RectTransform overlay = CreatePanel("Phase Transition", root, new Color(Background.r, Background.g, Background.b, 0.94f),
                new Vector2(0.12f, 0.40f), new Vector2(0.88f, 0.61f));
            Outline outline = overlay.gameObject.AddComponent<Outline>();
            outline.effectColor = Danger;
            outline.effectDistance = new Vector2(4f, -4f);
            phaseBanner = CreateText("Phase Banner Text", overlay, "PHASE 2\nENRAGED", 58, FontStyle.Bold, Danger,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f), TextAnchor.MiddleCenter);
            phaseBannerGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            phaseBannerGroup.alpha = 0f;
            phaseBannerGroup.blocksRaycasts = false;
        }

        private void BuildHeader(RectTransform root)
        {
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

            bossHealthText = CreateText("Health Text", healthBack, "260 / 260", 24, FontStyle.Bold, White,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            damagePopup = CreateText("Damage Popup", header, "-0", 64, FontStyle.Bold, TrailHot,
                new Vector2(0.66f, 0.28f), new Vector2(0.96f, 0.75f), TextAnchor.MiddleRight);
            damagePopupGroup = damagePopup.gameObject.AddComponent<CanvasGroup>();
            damagePopupGroup.alpha = 0f;
        }

        private void BuildMainField(RectTransform root)
        {
            RectTransform section = CreateRect("Main Field Section", root);
            section.anchorMin = new Vector2(0.035f, 0.245f);
            section.anchorMax = new Vector2(0.965f, 0.835f);
            section.offsetMin = Vector2.zero;
            section.offsetMax = Vector2.zero;

            RectTransform titleBar = CreatePanel("Field Title", section, PanelLight,
                new Vector2(0f, 0.91f), new Vector2(1f, 1f));
            fieldTitleText = CreateText("Field Label", titleBar, "CRYSTAL CAVERN", 30, FontStyle.Bold, White,
                new Vector2(0.04f, 0f), new Vector2(0.45f, 1f), TextAnchor.MiddleLeft);
            comboText = CreateText("Power", titleBar, "경로 1칸  ·  예상 피해 13", 25, FontStyle.Bold, Trail,
                new Vector2(0.38f, 0f), new Vector2(0.96f, 1f), TextAnchor.MiddleRight);

            RectTransform field = CreatePanel("Field Frame", section, Hex("0A1020"),
                new Vector2(0f, 0f), new Vector2(1f, 0.89f));
            fieldFrame = field.GetComponent<Image>();
            Outline outline = field.gameObject.AddComponent<Outline>();
            outline.effectColor = Hex("42D9EA");
            outline.effectDistance = new Vector2(3f, -3f);

            Texture2D caveTexture = Resources.Load<Texture2D>("Art/cave_arena_background");
            if (caveTexture != null)
            {
                caveTexture.filterMode = FilterMode.Point;
                caveTexture.wrapMode = TextureWrapMode.Clamp;
                RectTransform caveBackdrop = CreateImage("Cave Backdrop", field, White,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Image caveImage = caveBackdrop.GetComponent<Image>();
                caveImage.sprite = Sprite.Create(caveTexture,
                    new Rect(0f, 0f, caveTexture.width, caveTexture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                caveImage.preserveAspect = false;
                caveImage.raycastTarget = false;
            }

            for (int i = 0; i < 18; i++)
            {
                float x = 0.035f + ((i * 37) % 91) / 100f;
                float y = 0.035f + ((i * 53) % 87) / 100f;
                float size = i % 4 == 0 ? 3.5f : 2f;
                Color glow = i % 5 == 0
                    ? new Color(1f, 0.48f, 0.12f, 0.34f)
                    : new Color(0.26f, 0.9f, 0.96f, 0.24f);
                CreateImage("Cave Spark " + i, field, glow, new Vector2(x, y), new Vector2(x, y),
                    new Vector2(-size, -size), new Vector2(size, size));
            }

            mainGrid = CreateRect("Main Grid", field);
            mainGrid.anchorMin = new Vector2(0.5f, 0.5f);
            mainGrid.anchorMax = new Vector2(0.5f, 0.5f);
            mainGrid.pivot = new Vector2(0.5f, 0.5f);
            mainGrid.sizeDelta = new Vector2(900f, 900f);
            mainGrid.anchoredPosition = Vector2.zero;
            mainCellSize = 900f / TrailFieldModel.Size;
            startMarkerSprite = LoadPixelSprite("Art/start_sword_retouch", 32f);
            endMarkerSprite = LoadPixelSprite("Art/end_flag_retouch", 32f);
            powerIconSprite = LoadPixelSprite("Art/special_plus", 32f);
            amplifyIconSprite = LoadPixelSprite("Art/special_up", 32f);
            mudIconSprite = LoadPixelSprite("Art/special_pause", 32f);
            curseIconSprite = LoadPixelSprite("Art/special_down", 32f);

            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    RectTransform tile = CreateRect("Tile " + x + "," + y, mainGrid);
                    tile.anchorMin = tile.anchorMax = new Vector2(0.5f, 0.5f);
                    tile.sizeDelta = Vector2.one * (mainCellSize - 5f);
                    tile.anchoredPosition = GridPosition(x, y, mainCellSize);
                    Image image = tile.gameObject.AddComponent<Image>();
                    image.color = Floor;
                    mainTiles[x, y] = image;
                    Outline tileEdge = tile.gameObject.AddComponent<Outline>();
                    tileEdge.effectColor = FloorEdge;
                    tileEdge.effectDistance = new Vector2(1.2f, -1.2f);

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

            effectsLayer = CreateRect("Effects Layer", mainGrid);
            effectsLayer.anchorMin = effectsLayer.anchorMax = new Vector2(0.5f, 0.5f);
            effectsLayer.sizeDelta = new Vector2(900f, 900f);
            effectsLayer.anchoredPosition = Vector2.zero;

            attackSlash = CreateRect("Attack Slash", effectsLayer);
            attackSlash.anchorMin = attackSlash.anchorMax = new Vector2(0.5f, 0.5f);
            attackSlash.sizeDelta = new Vector2(820f, 22f);
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

            mainPlayer = CreatePlayer("Player", mainGrid, mainCellSize * BattlePlayerSizeRatio, false);
        }

        private void BuildAttackWarningVisual(RectTransform tile, int x, int y)
        {
            RectTransform warning = CreateRect("Attack Warning", tile);
            warning.anchorMin = Vector2.zero;
            warning.anchorMax = Vector2.one;
            warning.offsetMin = new Vector2(3f, 3f);
            warning.offsetMax = new Vector2(-3f, -3f);

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
            for (int y = 0; y < TrailFieldModel.Size; y++)
            {
                for (int x = 0; x < TrailFieldModel.Size; x++)
                {
                    var cell = new Vector2Int(x, y);
                    bool active = model.IsWalkable(cell);
                    mainTiles[x, y].gameObject.SetActive(active);
                    if (!active)
                    {
                        continue;
                    }

                    Color color = GetFloorColor(x, y);
                    bool isCrystal = crystalCells.Contains(cell);
                    bool crystalWarned = crystalWarningCounts.ContainsKey(cell);
                    bool crystalFiring = crystalFiringCounts.ContainsKey(cell);
                    bool hasSpecialTile = specialTiles.TryGetValue(cell, out SpecialTileType specialType);
                    if (isCrystal) color = Hex("4A1723");
                    if (model.IsTrail(cell)) color = Trail;
                    if (cell == model.Start) color = StartColor;
                    if (cell == model.End) color = EndColor;
                    color.a = 0.5f;
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
                        color.a = 0.5f;
                    }
                    if (crystalFiring)
                    {
                        color = Color.Lerp(Hex("FF3B24"), White, 0.38f);
                        color.a = 1f;
                    }
                    mainTiles[x, y].color = color;
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

            mainPlayer.anchoredPosition = GridPosition(model.Player.x, model.Player.y, mainCellSize);
            mainPlayer.SetAsLastSibling();

            int shownLength = model.IsTracing ? model.Trail.Count : 0;
            int projectedDamage = SpecialTileRules.ApplyDamageModifiers(
                CalculateDamage(shownLength), nextAttackFlatBonus, nextAttackMultiplier);
            comboText.text = model.IsTracing
                ? "경로 " + shownLength + "칸  ·  예상 피해 " + projectedDamage
                : "경로 대기  ·  START 필요";
            UpdatePowerRuleText();
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

        private Color GetFloorColor(int x, int y)
        {
            Color baseColor;
            Color highlight;
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
            floorColor.a = 0.5f;
            return floorColor;
        }

        private void AnimateVisuals()
        {
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

            if (!model.IsWalkable(model.Start) || !model.IsWalkable(model.End))
            {
                return;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * 4.5f) + 1f) * 0.5f;
            Color startPulse = Color.Lerp(StartColor, White, pulse * 0.2f);
            Color endPulse = Color.Lerp(EndColor, TrailHot, pulse * 0.35f);
            startPulse.a = 0.5f;
            endPulse.a = 0.5f;
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
                mainPlayerImage.color = Color.Lerp(White, TrailHot, pulse * 0.08f);
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
                    float maxSize = Mathf.Max(0f, mainTiles[x, y].rectTransform.sizeDelta.x - 10f);
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
            Vector2 basePosition = mainGrid.anchoredPosition;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float fade = 1f - elapsed / duration;
                mainGrid.anchoredPosition = PixelSnap(basePosition + Random.insideUnitCircle * (strength * fade));
                yield return null;
            }
            mainGrid.anchoredPosition = basePosition;
        }

        private IEnumerator PunchPlayer(bool blocked)
        {
            Vector2 basePosition = GridPosition(model.Player.x, model.Player.y, mainCellSize);
            if (blocked)
            {
                for (float t = 0f; t < 1f; t += Time.deltaTime * 8f)
                {
                    mainPlayer.anchoredPosition = basePosition + Vector2.right * (Mathf.Sin(t * 38f) * (1f - t) * 13f);
                    yield return null;
                }
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
            if (System.Array.IndexOf(arguments, "-captureEndpoints") >= 0 && !tutorialActive)
            {
                model.TryMove(Vector2Int.up);
                RefreshBoard();
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

            var renderTexture = new RenderTexture(540, 960, 24, RenderTextureFormat.ARGB32);
            uiCamera.targetTexture = renderTexture;

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            Canvas.ForceUpdateCanvases();
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

        private static Vector2 GridPosition(int x, int y, float size)
        {
            float center = (TrailFieldModel.Size - 1) * 0.5f;
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

            Outline outline = player.gameObject.AddComponent<Outline>();
            outline.effectColor = mini ? Background : Trail;
            outline.effectDistance = mini ? new Vector2(2f, -2f) : new Vector2(3f, -3f);
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
