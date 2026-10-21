// All screens and HUD, built in code. No game state here; the session
// drives everything through these calls plus events.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AxePath
{
    public class Hud : MonoBehaviour
    {
        public event Action OnWake;
        public event Action OnBookClose;
        public event Action OnExport;
        public event Action OnFirstLock;
        public event Action OnPauseResume;
        public event Action OnPauseQuit;
        public event Action<float> OnLookSens;

        private static Font uiFont;
        private static Color Orange = new Color(0.85f, 0.64f, 0.25f);

        private GameObject hudRoot;
        private GameObject titleRoot;
        private GameObject bookRoot;
        private GameObject modalRoot;
        private GameObject deathRoot;
        private GameObject lockRoot;
        private GameObject pauseRoot;
        private Slider pauseSens;
        private Text pauseSensVal;
        private GameObject dialogueBox;
        private Text dialogueName;
        private Text dialogueLine;
        private Text promptText;
        private Text bannerText;
        private Text titleMsg;
        private Text bookTitle;
        private Text bookBody;
        private Text modalTitle;
        private Text modalBody;
        private Button modalOk;
        private Button modalCancel;
        private Text deathCopy;
        private Text deathDwarfLine;
        private GameObject crossDot;
        private RawImage hurtImg;
        private List<Image> hpPips = new List<Image>();
        private List<Image> edgePips = new List<Image>();
        private Text edgeCap;
        private Text handAxe;
        private Text handBook;
        private Text handLog;
        private Transform slotWrap;
        private Action modalOkFn;
        private Action modalCancelFn;
        private float promptHoldUntil;
        private float crossUntil;
        private float hurtUntil;

        public static Font GetFont()
        {
            if (uiFont == null)
            {
                uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (uiFont == null) uiFont = Font.CreateDynamicFontFromOSFont(new string[] { "Arial", "Helvetica", "Verdana" }, 16);
            }
            return uiFont;
        }

        private static Text MkText(Transform parent, string name, int size, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = GetFont();
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            // Save imports can smuggle markup-looking text into labels and
            // error echoes; no game string uses rich text, so it stays off.
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static GameObject MkPanel(Transform parent, string name, float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, alpha);
            return go;
        }

        private static Button MkButton(Transform parent, string name, string label, int size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.13f, 0.16f, 0.24f, 0.95f);
            var btn = go.AddComponent<Button>();
            var t = MkText(go.transform, "Label", size, TextAnchor.MiddleCenter);
            t.text = label;
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return btn;
        }

        private static void Place(RectTransform rt, float ax, float ay, float px, float py, float w, float h)
        {
            rt.anchorMin = new Vector2(ax, ay);
            rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(px, py);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
        }

        public void Build()
        {
            var canvasGo = new GameObject("HudCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 800f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var esGo = new GameObject("EventSystem");
            esGo.transform.SetParent(transform, false);
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
            var C = canvasGo.transform;

            hudRoot = new GameObject("Hud");
            hudRoot.transform.SetParent(C, false);
            var hudRt = hudRoot.AddComponent<RectTransform>();
            hudRt.anchorMin = Vector2.zero;
            hudRt.anchorMax = Vector2.one;
            hudRt.offsetMin = Vector2.zero;
            hudRt.offsetMax = Vector2.zero;
            hudRoot.SetActive(false);

            var hpRow = new GameObject("Hp");
            hpRow.transform.SetParent(hudRoot.transform, false);
            Place(hpRow.AddComponent<RectTransform>(), 0f, 0f, 0f, 0f, 220f, 30f);
            hpRow.GetComponent<RectTransform>().anchoredPosition = new Vector2(24f, 64f);
            for (int i = 0; i < 3; i++)
            {
                var pip = new GameObject("Pip");
                pip.transform.SetParent(hpRow.transform, false);
                var img = pip.AddComponent<Image>();
                img.color = new Color(0.2f, 0.2f, 0.2f);
                var rt = pip.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(18f, 18f);
                rt.anchoredPosition = new Vector2(i * 24f, 0f);
                hpPips.Add(img);
            }
            var hpCap = MkText(hpRow.transform, "Cap", 16, TextAnchor.MiddleLeft);
            var hpCapRt = hpCap.GetComponent<RectTransform>();
            hpCapRt.anchorMin = new Vector2(0f, 0.5f);
            hpCapRt.anchorMax = new Vector2(0f, 0.5f);
            hpCapRt.pivot = new Vector2(0f, 0.5f);
            hpCapRt.sizeDelta = new Vector2(80f, 24f);
            hpCapRt.anchoredPosition = new Vector2(80f, 0f);
            hpCap.text = Lines.HudHealth;
            hpCap.color = new Color(1f, 1f, 1f, 0.7f);

            var edgeRow = new GameObject("Edge");
            edgeRow.transform.SetParent(hudRoot.transform, false);
            Place(edgeRow.AddComponent<RectTransform>(), 0f, 0f, 0f, 0f, 220f, 30f);
            edgeRow.GetComponent<RectTransform>().anchoredPosition = new Vector2(24f, 30f);
            for (int i = 0; i < 3; i++)
            {
                var pip = new GameObject("Pip");
                pip.transform.SetParent(edgeRow.transform, false);
                var img = pip.AddComponent<Image>();
                img.color = new Color(0.2f, 0.2f, 0.2f);
                var rt = pip.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(18f, 18f);
                rt.anchoredPosition = new Vector2(i * 24f, 0f);
                edgePips.Add(img);
            }
            edgeCap = MkText(edgeRow.transform, "Cap", 16, TextAnchor.MiddleLeft);
            var edgeCapRt = edgeCap.GetComponent<RectTransform>();
            edgeCapRt.anchorMin = new Vector2(0f, 0.5f);
            edgeCapRt.anchorMax = new Vector2(0f, 0.5f);
            edgeCapRt.pivot = new Vector2(0f, 0.5f);
            edgeCapRt.sizeDelta = new Vector2(80f, 24f);
            edgeCapRt.anchoredPosition = new Vector2(80f, 0f);
            edgeCap.text = Lines.HudNoAxe;
            edgeCap.color = new Color(1f, 1f, 1f, 0.7f);

            var hands = new GameObject("Hands");
            hands.transform.SetParent(hudRoot.transform, false);
            Place(hands.AddComponent<RectTransform>(), 1f, 0f, 1f, 0f, 300f, 30f);
            hands.GetComponent<RectTransform>().anchoredPosition = new Vector2(-24f, 30f);
            handAxe = MkText(hands.transform, "Axe", 16, TextAnchor.MiddleRight);
            Place(handAxe.GetComponent<RectTransform>(), 1f, 0.5f, 1f, 0.5f, 90f, 26f);
            handAxe.GetComponent<RectTransform>().anchoredPosition = new Vector2(-200f, 0f);
            handAxe.text = Lines.HudHandAxe;
            handBook = MkText(hands.transform, "Book", 16, TextAnchor.MiddleRight);
            Place(handBook.GetComponent<RectTransform>(), 1f, 0.5f, 1f, 0.5f, 90f, 26f);
            handBook.GetComponent<RectTransform>().anchoredPosition = new Vector2(-100f, 0f);
            handBook.text = Lines.HudHandBook;
            handLog = MkText(hands.transform, "Log", 16, TextAnchor.MiddleRight);
            Place(handLog.GetComponent<RectTransform>(), 1f, 0.5f, 1f, 0.5f, 90f, 26f);
            handLog.text = Lines.HudHandLog;

            var promptPanel = MkPanel(hudRoot.transform, "Prompt", 0.72f);
            Place(promptPanel.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, 640f, 40f);
            promptPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 110f);
            promptText = MkText(promptPanel.transform, "Text", 20, TextAnchor.MiddleCenter);
            var promptRt = promptText.GetComponent<RectTransform>();
            promptRt.anchorMin = Vector2.zero;
            promptRt.anchorMax = Vector2.one;
            promptRt.offsetMin = new Vector2(12f, 0f);
            promptRt.offsetMax = new Vector2(-12f, 0f);
            promptPanel.SetActive(false);

            crossDot = new GameObject("Cross");
            crossDot.transform.SetParent(hudRoot.transform, false);
            var crossImg = crossDot.AddComponent<Image>();
            crossImg.color = Color.white;
            Place(crossDot.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, 6f, 6f);

            var bannerPanel = MkPanel(hudRoot.transform, "Banner", 0.72f);
            Place(bannerPanel.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 700f, 34f);
            bannerPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -12f);
            bannerText = MkText(bannerPanel.transform, "Text", 16, TextAnchor.MiddleCenter);
            var bannerRt = bannerText.GetComponent<RectTransform>();
            bannerRt.anchorMin = Vector2.zero;
            bannerRt.anchorMax = Vector2.one;
            bannerRt.offsetMin = new Vector2(12f, 0f);
            bannerRt.offsetMax = new Vector2(-12f, 0f);
            bannerPanel.SetActive(false);

            dialogueBox = MkPanel(hudRoot.transform, "Dialogue", 0.85f);
            Place(dialogueBox.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, 720f, 110f);
            dialogueBox.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 170f);
            dialogueName = MkText(dialogueBox.transform, "Name", 18, TextAnchor.MiddleLeft);
            Place(dialogueName.GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 700f, 28f);
            dialogueName.GetComponent<RectTransform>().anchoredPosition = new Vector2(16f, -6f);
            dialogueName.color = Orange;
            dialogueLine = MkText(dialogueBox.transform, "Line", 20, TextAnchor.UpperLeft);
            Place(dialogueLine.GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 688f, 70f);
            dialogueLine.GetComponent<RectTransform>().anchoredPosition = new Vector2(16f, -36f);
            dialogueBox.SetActive(false);

            var hurtGo = new GameObject("Hurt");
            hurtGo.transform.SetParent(hudRoot.transform, false);
            hurtImg = hurtGo.AddComponent<RawImage>();
            hurtImg.texture = RadialTexture();
            hurtImg.color = new Color(1f, 1f, 1f, 0f);
            var hurtRt = hurtGo.GetComponent<RectTransform>();
            hurtRt.anchorMin = Vector2.zero;
            hurtRt.anchorMax = Vector2.one;
            hurtRt.offsetMin = Vector2.zero;
            hurtRt.offsetMax = Vector2.zero;
            hurtImg.raycastTarget = false;
            crossImg.raycastTarget = false;

            BuildTitle(C);
            BuildBook(C);
            BuildModal(C);
            BuildDeath(C);
            BuildLock(C);
            BuildPause(C);
        }

        private void BuildTitle(Transform c)
        {
            titleRoot = MkPanel(c, "Title", 0.92f);
            var rt = titleRoot.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var head = MkText(titleRoot.transform, "Head", 54, TextAnchor.MiddleCenter);
            Place(head.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 800f, 80f);
            head.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -90f);
            head.text = Lines.TitleHead;
            head.color = Orange;
            var sub = MkText(titleRoot.transform, "Sub", 20, TextAnchor.MiddleCenter);
            Place(sub.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 800f, 30f);
            sub.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -150f);
            sub.text = Lines.TitleSub;
            sub.color = new Color(1f, 1f, 1f, 0.75f);
            titleMsg = MkText(titleRoot.transform, "Msg", 18, TextAnchor.MiddleCenter);
            Place(titleMsg.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 800f, 30f);
            titleMsg.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -190f);
            var wrap = new GameObject("Slots");
            wrap.transform.SetParent(titleRoot.transform, false);
            Place(wrap.AddComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 560f, 300f);
            wrap.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -230f);
            slotWrap = wrap.transform;
            var exp = MkButton(titleRoot.transform, "Export", Lines.TitleExport, 18);
            Place(exp.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, 260f, 44f);
            exp.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 60f);
            exp.onClick.AddListener(() => { if (OnExport != null) OnExport(); });
            var card = MkText(titleRoot.transform, "Controls", 16, TextAnchor.MiddleCenter);
            Place(card.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, 760f, 60f);
            card.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 130f);
            card.text = Lines.TitleControls;
            card.color = new Color(1f, 1f, 1f, 0.75f);
            titleRoot.SetActive(false);
        }

        private void BuildBook(Transform c)
        {
            bookRoot = MkPanel(c, "Book", 0.94f);
            Place(bookRoot.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, 620f, 420f);
            bookTitle = MkText(bookRoot.transform, "Title", 26, TextAnchor.MiddleCenter);
            Place(bookTitle.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 580f, 40f);
            bookTitle.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -16f);
            bookTitle.color = Orange;
            bookBody = MkText(bookRoot.transform, "Body", 20, TextAnchor.UpperLeft);
            Place(bookBody.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 560f, 300f);
            bookBody.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -66f);
            var close = MkButton(bookRoot.transform, "Close", Lines.BookClose, 18);
            Place(close.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, 160f, 40f);
            close.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 16f);
            close.onClick.AddListener(() => { if (OnBookClose != null) OnBookClose(); });
            bookRoot.SetActive(false);
        }

        private void BuildModal(Transform c)
        {
            modalRoot = MkPanel(c, "Modal", 0.9f);
            Place(modalRoot.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, 560f, 300f);
            modalTitle = MkText(modalRoot.transform, "Title", 24, TextAnchor.MiddleCenter);
            Place(modalTitle.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 520f, 40f);
            modalTitle.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -14f);
            modalTitle.color = Orange;
            modalBody = MkText(modalRoot.transform, "Body", 19, TextAnchor.UpperCenter);
            Place(modalBody.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 500f, 150f);
            modalBody.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -60f);
            modalOk = MkButton(modalRoot.transform, "Ok", Lines.ModalOk, 18);
            Place(modalOk.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, 140f, 40f);
            modalOk.GetComponent<RectTransform>().anchoredPosition = new Vector2(-80f, 18f);
            modalOk.onClick.AddListener(() => {
                modalRoot.SetActive(false);
                var f = modalOkFn;
                modalOkFn = null;
                modalCancelFn = null;
                if (f != null) f();
            });
            modalCancel = MkButton(modalRoot.transform, "Cancel", Lines.ModalCancel, 18);
            Place(modalCancel.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, 140f, 40f);
            modalCancel.GetComponent<RectTransform>().anchoredPosition = new Vector2(80f, 18f);
            modalCancel.onClick.AddListener(() => {
                modalRoot.SetActive(false);
                var f = modalCancelFn;
                modalOkFn = null;
                modalCancelFn = null;
                if (f != null) f();
            });
            modalRoot.SetActive(false);
        }

        private void BuildDeath(Transform c)
        {
            deathRoot = MkPanel(c, "Death", 0.94f);
            var rt = deathRoot.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var head = MkText(deathRoot.transform, "Head", 44, TextAnchor.MiddleCenter);
            Place(head.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 800f, 60f);
            head.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -140f);
            head.text = Lines.DeathHead;
            head.color = new Color(0.75f, 0.25f, 0.2f);
            deathCopy = MkText(deathRoot.transform, "Copy", 21, TextAnchor.MiddleCenter);
            Place(deathCopy.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 760f, 90f);
            deathCopy.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -220f);
            deathDwarfLine = MkText(deathRoot.transform, "DwarfLine", 19, TextAnchor.MiddleCenter);
            Place(deathDwarfLine.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 760f, 60f);
            deathDwarfLine.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -300f);
            deathDwarfLine.color = Orange;
            var wake = MkButton(deathRoot.transform, "Wake", Lines.DeathWake, 20);
            Place(wake.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, 300f, 48f);
            wake.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 120f);
            wake.onClick.AddListener(() => { if (OnWake != null) OnWake(); });
            deathRoot.SetActive(false);
        }

        private void BuildLock(Transform c)
        {
            lockRoot = MkPanel(c, "FirstLock", 0.55f);
            var rt = lockRoot.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var t = MkText(lockRoot.transform, "Text", 26, TextAnchor.MiddleCenter);
            Place(t.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, 700f, 60f);
            t.text = Lines.LockHint;
            var btn = lockRoot.AddComponent<Button>();
            btn.onClick.AddListener(() => { if (OnFirstLock != null) OnFirstLock(); });
            lockRoot.SetActive(false);
        }

        private static Slider MkSlider(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.13f, 0.16f, 0.24f, 0.95f);
            var slider = go.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            // A focused slider eats arrow keys, which are also move keys:
            // no keyboard navigation, mouse only.
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = Orange;
            fillImg.raycastTarget = false;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            slider.fillRect = fillRt;
            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(go.transform, false);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Color.white;
            handleImg.raycastTarget = false;
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0f, 0.5f);
            handleRt.anchorMax = new Vector2(0f, 0.5f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(20f, 36f);
            slider.handleRect = handleRt;
            return slider;
        }

        private void BuildPause(Transform c)
        {
            pauseRoot = MkPanel(c, "Pause", 0.92f);
            Place(pauseRoot.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, 560f, 440f);
            var head = MkText(pauseRoot.transform, "Head", 34, TextAnchor.MiddleCenter);
            Place(head.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 520f, 44f);
            head.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -14f);
            head.text = Lines.PauseHead;
            head.color = Orange;
            var sensLabel = MkText(pauseRoot.transform, "SensLabel", 18, TextAnchor.MiddleCenter);
            Place(sensLabel.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 480f, 30f);
            sensLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -72f);
            sensLabel.text = Lines.PauseSensitivity;
            pauseSens = MkSlider(pauseRoot.transform, "Sens");
            Place(pauseSens.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 440f, 28f);
            pauseSens.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -112f);
            pauseSens.minValue = PlayerController.LookSensMin;
            pauseSens.maxValue = PlayerController.LookSensMax;
            pauseSensVal = MkText(pauseRoot.transform, "SensVal", 16, TextAnchor.MiddleCenter);
            Place(pauseSensVal.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 200f, 26f);
            pauseSensVal.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -148f);
            pauseSensVal.color = new Color(1f, 1f, 1f, 0.75f);
            pauseSens.onValueChanged.AddListener((float v) => {
                pauseSensVal.text = SensPct(v);
                if (OnLookSens != null) OnLookSens(v);
            });
            var resume = MkButton(pauseRoot.transform, "Resume", Lines.PauseResume, 20);
            Place(resume.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, 260f, 46f);
            resume.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 116f);
            resume.onClick.AddListener(() => { if (OnPauseResume != null) OnPauseResume(); });
            var quit = MkButton(pauseRoot.transform, "Quit", Lines.PauseQuit, 18);
            Place(quit.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, 260f, 46f);
            quit.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 56f);
            quit.onClick.AddListener(() => { if (OnPauseQuit != null) OnPauseQuit(); });
            pauseRoot.SetActive(false);
        }

        private static string SensPct(float v)
        {
            return Mathf.RoundToInt(v / PlayerController.DefaultLookSens * 100f) + "%";
        }

        private static Texture2D RadialTexture()
        {
            int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x / (float)(n - 1)) * 2f - 1f;
                    float dy = (y / (float)(n - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((r - 0.55f) / 0.45f) * 0.55f;
                    tex.SetPixel(x, y, new Color(0.63f, 0.08f, 0.08f, a));
                }
            }
            tex.Apply();
            return tex;
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (crossDot != null)
            {
                bool hit = now < crossUntil;
                var img = crossDot.GetComponent<Image>();
                img.color = hit ? new Color(1f, 0.45f, 0.15f) : Color.white;
                crossDot.transform.localScale = Vector3.one * (hit ? 2f : 1f);
            }
            if (hurtImg != null)
            {
                var c = hurtImg.color;
                c.a = now < hurtUntil ? 1f : 0f;
                hurtImg.color = c;
            }
        }

        public void CloseAllScreens()
        {
            titleRoot.SetActive(false);
            bookRoot.SetActive(false);
            modalRoot.SetActive(false);
            deathRoot.SetActive(false);
            lockRoot.SetActive(false);
            pauseRoot.SetActive(false);
            modalOkFn = null;
            modalCancelFn = null;
        }

        public void SetTitleMessage(string text) { titleMsg.text = text ?? ""; }

        public void RenderSlots(Func<int, SlotDesc> describe, Action<int, SlotDesc> onPick)
        {
            foreach (Transform child in slotWrap) GameObject.Destroy(child.gameObject);
            for (int s = 1; s <= 3; s++)
            {
                int slot = s;
                var info = describe(slot);
                string label;
                if (info.state == "empty") label = Lines.SlotPrefix + s + Lines.SlotEmpty;
                else if (info.state == "filled")
                {
                    string when = info.savedAt != null && info.savedAt.Length >= 10 ? info.savedAt.Substring(0, 10) : "";
                    // Imports may carry 120-char labels; the button fits one line.
                    string shown = info.label ?? "";
                    int max = 40;
                    if (shown.Length > max)
                    {
                        if (char.IsHighSurrogate(shown[max - 1]) && char.IsLowSurrogate(shown[max])) max--;
                        shown = shown.Substring(0, max) + "...";
                    }
                    label = Lines.SlotPrefix + s + Lines.SlotMid + shown + Lines.SlotDateSep + when;
                }
                else label = Lines.SlotPrefix + s + Lines.SlotBroken;
                var b = MkButton(slotWrap, "Slot", label, 18);
                Place(b.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 540f, 52f);
                b.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -(s - 1) * 62f);
                b.onClick.AddListener(() => onPick(slot, info));
            }
        }

        public void ShowTitle(bool show) { titleRoot.SetActive(show); }
        public void ShowHud(bool show) { hudRoot.SetActive(show); }

        public void ShowPause(bool show)
        {
            pauseRoot.SetActive(show);
            if (!show && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public bool IsPauseOpen() { return pauseRoot.activeSelf; }

        public void SetLookSens(float v)
        {
            pauseSens.SetValueWithoutNotify(v);
            pauseSensVal.text = SensPct(v);
        }

        public void SetEdge(int step, bool have)
        {
            for (int i = 0; i < edgePips.Count; i++)
            {
                edgePips[i].color = (have && i <= 2 - step) ? new Color(0.85f, 0.8f, 0.6f) : new Color(0.2f, 0.2f, 0.2f);
            }
            edgeCap.text = have ? Lines.HudEdge : Lines.HudNoAxe;
        }

        public void SetHp(int hp)
        {
            for (int i = 0; i < hpPips.Count; i++)
            {
                hpPips[i].color = i < hp ? new Color(0.75f, 0.2f, 0.15f) : new Color(0.2f, 0.2f, 0.2f);
            }
        }

        public void SetHands(bool axe, bool book, int iron)
        {
            handAxe.color = axe ? Orange : new Color(1f, 1f, 1f, 0.35f);
            handBook.color = book ? Orange : new Color(1f, 1f, 1f, 0.35f);
            handLog.color = iron >= 1 ? Orange : new Color(1f, 1f, 1f, 0.35f);
        }

        public void SetPrompt(string text, float holdSecs)
        {
            if (!string.IsNullOrEmpty(text))
            {
                promptText.text = text;
                promptText.transform.parent.gameObject.SetActive(true);
                if (holdSecs > 0f) promptHoldUntil = Time.realtimeSinceStartup + holdSecs;
            }
            else
            {
                promptHoldUntil = 0f;
                promptText.transform.parent.gameObject.SetActive(false);
            }
        }

        public bool PromptHeld() { return Time.realtimeSinceStartup < promptHoldUntil; }
        public string PromptText() { return promptText.transform.parent.gameObject.activeSelf ? promptText.text : ""; }

        public void FlashCross(float secs = 0.16f) { crossUntil = Time.realtimeSinceStartup + secs; }

        public void SetBanner(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                bannerText.text = text;
                bannerText.transform.parent.gameObject.SetActive(true);
            }
            else bannerText.transform.parent.gameObject.SetActive(false);
        }

        public void ShowDialogue(string name, string line)
        {
            dialogueName.text = name;
            dialogueLine.text = line;
            dialogueBox.SetActive(true);
        }

        public void HideDialogue() { dialogueBox.SetActive(false); }
        public bool IsDialogueOpen() { return dialogueBox.activeSelf; }

        public void ShowModal(string title, string body, Action onOk, Action onCancel)
        {
            modalTitle.text = title;
            modalBody.text = body;
            modalOkFn = onOk;
            modalCancelFn = onCancel;
            modalCancel.gameObject.SetActive(onCancel != null);
            modalRoot.SetActive(true);
        }

        public bool IsModalOpen() { return modalRoot.activeSelf; }
        public void CloseModal() { modalRoot.SetActive(false); modalOkFn = null; modalCancelFn = null; }

        public void ShowDeath(string copy, string dwarfLine)
        {
            deathCopy.text = copy;
            deathDwarfLine.text = dwarfLine ?? "";
            deathRoot.SetActive(true);
        }

        public bool IsDeathOpen() { return deathRoot.activeSelf; }
        public void CloseDeath() { deathRoot.SetActive(false); }

        public void ShowBook(string title, string body)
        {
            bookTitle.text = title;
            bookBody.text = body;
            bookRoot.SetActive(true);
        }

        public bool IsBookOpen() { return bookRoot.activeSelf; }
        public void CloseBook() { bookRoot.SetActive(false); }

        public void ShowFirstLock(bool show) { lockRoot.SetActive(show); }
        public bool IsFirstLockOpen() { return lockRoot.activeSelf; }

        public void HurtFlash() { hurtUntil = Time.realtimeSinceStartup + 0.18f; }
    }
}
