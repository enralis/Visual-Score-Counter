﻿using CountersPlus.ConfigModels;
using CountersPlus.Utils;
using CountersPlus.Counters.Custom;
using CountersPlus.Counters.Interfaces;
using TMPro;
using Zenject;
using UnityEngine;
using UnityEngine.UI;
using IPA.Utilities;
using System;
using HMUI;
using BeatSaberMarkupLanguage;
using System.Linq;

namespace VisualScoreCounter
{
    internal class VisualScoreCounter : BasicCustomCounter, INoteEventHandler
    {

        [Inject] private CoreGameHUDController coreGameHUD;
        [Inject] private RelativeScoreAndImmediateRankCounter relativeScoreAndImmediateRank;

        // Ring vars
        private readonly string multiplierImageSpriteName = "Circle";
        private readonly Vector2 ringOffset = new Vector2(0.0f, 0.0f);
        private readonly Vector3 ringSize = Vector3.one * 1.175f;
        private ImageView progressRing;

        // Classic Mode vars
        private readonly Vector3 offset = new Vector3(0.1f, 1.8f, 0);
        private readonly Vector2 rankScoreOffset = new Vector2(2.0f, 6.0f);
        private RankModel.Rank prevImmediateRank = RankModel.Rank.SSS;
        private float prevRelativeScore = 0.0f;
        private TextMeshProUGUI rankText;
        private TextMeshProUGUI relativeScoreText;

        // Percent Mode vars
        private TMP_Text percentMajorText;
        private TMP_Text percentMinorText;
        private int prevPercentMajor;
        private int prevPercentMinor;

        // Accessors

        public static FieldAccessor<ScoreUIController, TextMeshProUGUI>.Accessor ScoreUIText = FieldAccessor<ScoreUIController, TextMeshProUGUI>.GetAccessor("_scoreText");
        public static FieldAccessor<CoreGameHUDController, GameObject>.Accessor SongProgressPanelGO = FieldAccessor<CoreGameHUDController, GameObject>.GetAccessor("_songProgressPanelGO");
        public static FieldAccessor<CoreGameHUDController, GameObject>.Accessor RelativeScoreGO = FieldAccessor<CoreGameHUDController, GameObject>.GetAccessor("_relativeScoreGO");
        public static FieldAccessor<CoreGameHUDController, GameObject>.Accessor ImmediateRankGO = FieldAccessor<CoreGameHUDController, GameObject>.GetAccessor("_immediateRankGO");

        public override void CounterInit()
        {

            if (Configuration.Instance.percentMode)
            {
                InitPercentMode();
            } else
            {
                InitClassicMode();
            }
        }

        public override void CounterDestroy()
        {
            if (Configuration.Instance.percentMode)
            {
                //relativeScoreAndImmediateRank.relativeScoreOrImmediateRankDidChangeEvent -= UpdatePercentModeText;
            } else
            {
                //relativeScoreAndImmediateRank.relativeScoreOrImmediateRankDidChangeEvent -= UpdateClassicModeText;
            }
            //relativeScoreAndImmediateRank.relativeScoreOrImmediateRankDidChangeEvent -= UpdateRing;
        }

        private void InitClassicMode()
        {

            // Yeah this is required.
            // If the Score Counter is all alone on its own Canvas with nothing to accompany them,
            // I need to give 'em a friend or else they get shy and hide away in the void.
            _ = CanvasUtility.CreateTextFromSettings(Settings, null);

            ScoreUIController scoreUIController = coreGameHUD.GetComponentInChildren<ScoreUIController>();
            TextMeshProUGUI old = ScoreUIText(ref scoreUIController);

            // Setup Score Text
            GameObject baseGameScore = RelativeScoreGO(ref coreGameHUD);
            relativeScoreText = baseGameScore.GetComponent<TextMeshProUGUI>();
            relativeScoreText.color = Color.white;

            // Setup Rank Text
            GameObject baseGameRank = ImmediateRankGO(ref coreGameHUD);
            rankText = baseGameRank.GetComponent<TextMeshProUGUI>();
            rankText.color = Color.white;

            // Set up parenting
            Canvas currentCanvas = CanvasUtility.GetCanvasFromID(Settings.CanvasID);
            old.rectTransform.SetParent(currentCanvas.transform, true);
            baseGameScore.transform.SetParent(old.transform, true);

            // Hide score if we're not using it.
            if (!Configuration.Instance.showScore)
            {
                UnityEngine.Object.Destroy(baseGameScore.gameObject);
            }

            // Adjust font sizes.
            // TODO: Pull this from config?
            relativeScoreText.fontSize = 10;
            rankText.fontSize = 30;


            baseGameRank.transform.SetParent(old.transform, true);
            RectTransform pointsTextTransform = old.rectTransform;
            HUDCanvas currentSettings = CanvasUtility.GetCanvasSettingsFromID(Settings.CanvasID);
            Vector2 anchoredPos = CanvasUtility.GetAnchoredPositionFromConfig(Settings) + (offset * (3f / currentSettings.PositionScale));
            pointsTextTransform.localPosition = anchoredPos * currentSettings.PositionScale;
            pointsTextTransform.localPosition = new Vector3(pointsTextTransform.localPosition.x, pointsTextTransform.localPosition.y, 0);
            pointsTextTransform.localEulerAngles = Vector3.zero;

            UnityEngine.Object.Destroy(coreGameHUD.GetComponentInChildren<ImmediateRankUIPanel>());

            InitPercentageRing();

            // Shift text into proper positions
            rankText.rectTransform.anchoredPosition += rankScoreOffset;
            relativeScoreText.rectTransform.anchoredPosition += rankScoreOffset;

            //relativeScoreAndImmediateRank.relativeScoreOrImmediateRankDidChangeEvent += UpdateClassicModeText;
            UpdateClassicModeText();

        }

        private void InitPercentageRing()
        {
            if (Configuration.Instance.showPercentageRing)
            {
                // Create ring
                var canvas = CanvasUtility.GetCanvasFromID(Settings.CanvasID);
                if (canvas != null)
                {
                    HUDCanvas currentSettings = CanvasUtility.GetCanvasSettingsFromID(Settings.CanvasID);
                    Vector2 ringAnchoredPos = CanvasUtility.GetAnchoredPositionFromConfig(Settings) * currentSettings.PositionScale;

                    ImageView backgroundImage = CreateRing(canvas);
                    backgroundImage.rectTransform.anchoredPosition = ringAnchoredPos;
                    backgroundImage.CrossFadeAlpha(0.05f, 1f, false);
                    backgroundImage.transform.localScale = ringSize / 10;
                    backgroundImage.type = Image.Type.Simple;

                    progressRing = CreateRing(canvas);
                    progressRing.rectTransform.anchoredPosition = ringAnchoredPos;
                    progressRing.transform.localScale = ringSize / 10;

                }
                //relativeScoreAndImmediateRank.relativeScoreOrImmediateRankDidChangeEvent += UpdateRing;
                UpdateRing();
            }
        }
        

        private void InitPercentMode()
        {

            // Required. We need to get a handle to the game's default score counter and destroy it.
            _ = CanvasUtility.CreateTextFromSettings(Settings, null);
            ScoreUIController scoreUIController = coreGameHUD.GetComponentInChildren<ScoreUIController>();
            TextMeshProUGUI old = ScoreUIText(ref scoreUIController);

            GameObject baseGameScore = RelativeScoreGO(ref coreGameHUD);
            GameObject baseGameRank = ImmediateRankGO(ref coreGameHUD);

            // Set up parenting
            Canvas currentCanvas = CanvasUtility.GetCanvasFromID(Settings.CanvasID);
            old.rectTransform.SetParent(currentCanvas.transform, true);
            baseGameScore.transform.SetParent(old.transform, true);
            baseGameRank.transform.SetParent(old.transform, true);

            // Destroy Score
            UnityEngine.Object.Destroy(baseGameScore.gameObject);

            // Destroy Rank
            UnityEngine.Object.Destroy(baseGameRank.gameObject);

            if (!Configuration.Instance.showScore)
            {
                old.fontSize = 0;
            }

            percentMajorText = CanvasUtility.CreateTextFromSettings(Settings);
            percentMajorText.fontSize = 7;
            percentMinorText = CanvasUtility.CreateTextFromSettings(Settings);
            percentMinorText.fontSize = 3;

            RectTransform pointsTextTransform = old.rectTransform;
            HUDCanvas currentSettings = CanvasUtility.GetCanvasSettingsFromID(Settings.CanvasID);
            Vector2 anchoredPos = CanvasUtility.GetAnchoredPositionFromConfig(Settings) + (offset * (3f / currentSettings.PositionScale));
            pointsTextTransform.localPosition = anchoredPos * currentSettings.PositionScale;
            pointsTextTransform.localPosition = new Vector3(pointsTextTransform.localPosition.x, pointsTextTransform.localPosition.y, 0);
            pointsTextTransform.localEulerAngles = Vector3.zero;
            UnityEngine.Object.Destroy(coreGameHUD.GetComponentInChildren<ImmediateRankUIPanel>());

            InitPercentageRing();

            percentMajorText.rectTransform.anchoredPosition += new Vector2(0.0f, 0.7f);
            percentMinorText.rectTransform.anchoredPosition += new Vector2(0.0f, -3.0f);

            prevPercentMajor = -1;
            prevPercentMinor = -1;
            //relativeScoreAndImmediateRank.relativeScoreOrImmediateRankDidChangeEvent += UpdatePercentModeText;
            UpdatePercentModeText();
        }


        private void UpdatePercentModeText()
        {
            float relativeScore = relativeScoreAndImmediateRank.relativeScore * 100;
            int majorPercent = (int)Math.Floor(relativeScore);
            int minorPercent = (int)((relativeScore % 1) * 100);
            if (majorPercent != prevPercentMajor)
            {
                Color currentColor = GetColorForRelativeScore(relativeScore);
                percentMajorText.text = majorPercent.ToString();
                percentMajorText.color = currentColor;
                prevPercentMajor = majorPercent;
            }
            if (minorPercent != prevPercentMinor)
            {
                Color nextColor;
                if (Configuration.Instance.percentageRingShowsNextRankColor)
                {
                    nextColor = GetColorForRelativeScore(relativeScore + 1);
                } else
                {
                    nextColor = GetColorForRelativeScore(relativeScore);
                }
                if (minorPercent > 9)
                {
                    percentMinorText.text = minorPercent.ToString();
                } else
                {
                    percentMinorText.text = "0" + minorPercent.ToString();
                }
                percentMinorText.color = nextColor;
                prevPercentMinor = minorPercent;
            }
        }


        private void UpdateClassicModeText()
        {
            RankModel.Rank immediateRank = relativeScoreAndImmediateRank.immediateRank;
            float relativeScore = (relativeScoreAndImmediateRank.relativeScore) * 100.0f;
            if (Math.Floor(relativeScore) != Math.Floor(prevRelativeScore))
            {
                Color currentColor = GetColorForRelativeScore(relativeScore);
            }
            prevRelativeScore = relativeScore;
            if (immediateRank != prevImmediateRank)
            {
                rankText.text = RankModel.GetRankName(immediateRank);
                prevImmediateRank = immediateRank;
            }
            int decimalPrecision = 2;
            relativeScoreText.text = $"{relativeScore.ToString($"F{decimalPrecision}")}%";
        }

        private Color GetColorForRelativeScore(float RelativeScore) {

            // 100%
            if (RelativeScore >= 100.0f)
            {
                return Configuration.Instance.Color_100;
            }

            // 99%
            if (RelativeScore >= 99.0f && RelativeScore < 100.0f)
            {
                return Configuration.Instance.Color_99;
            }

            // 98%
            if (RelativeScore >= 98.0f && RelativeScore < 99.0f)
            {
                return Configuration.Instance.Color_98;
            }

            // 97%
            if (RelativeScore >= 97.0f && RelativeScore < 98.0f)
            {
                return Configuration.Instance.Color_97;
            }

            // 96%
            if (RelativeScore >= 96.0f && RelativeScore < 97.0f)
            {
                return Configuration.Instance.Color_96;
            }

            // 95%
            if (RelativeScore >= 95.0f && RelativeScore < 96.0f)
            {
                return Configuration.Instance.Color_95;
            }

            // 94%
            if (RelativeScore >= 94.0f && RelativeScore < 95.0f)
            {
                return Configuration.Instance.Color_94;
            }

            // 93%
            if (RelativeScore >= 93.0f && RelativeScore < 94.0f)
            {
                return Configuration.Instance.Color_93;
            }

            // 92%
            if (RelativeScore >= 92.0f && RelativeScore < 93.0f)
            {
                return Configuration.Instance.Color_92;
            }

            // 91%
            if (RelativeScore >= 91.0f && RelativeScore < 92.0f)
            {
                return Configuration.Instance.Color_91;
            }

            // 90%
            if (RelativeScore >= 90.0f && RelativeScore < 91.0f)
            {
                return Configuration.Instance.Color_90;
            }

            // 89%
            if (RelativeScore >= 89.0f && RelativeScore < 90.0f)
            {
                return Configuration.Instance.Color_89;
            }

            // 88%
            if (RelativeScore >= 88.0f && RelativeScore < 89.0f)
            {
                return Configuration.Instance.Color_88;
            }

            // 80%
            if (RelativeScore >= 80.0f && RelativeScore < 88.0f)
            {
                return Configuration.Instance.Color_80;
            }

            // 65%
            if (RelativeScore >= 65.0f && RelativeScore < 80.0f)
            {
                return Configuration.Instance.Color_65;
            }

            // 50%
            if (RelativeScore >= 50.0f && RelativeScore < 65.0f)
            {
                return Configuration.Instance.Color_50;
            }

            // 35%
            if (RelativeScore >= 35.0f && RelativeScore < 50.0f)
            {
                return Configuration.Instance.Color_35;
            }

            // 20%
            if (RelativeScore >= 20.0f && RelativeScore < 35.0f)
            {
                return Configuration.Instance.Color_20;
            }

            // 0%
            if (RelativeScore >= 0.0f && RelativeScore < 20.0f)
            {
                return Configuration.Instance.Color_0;
            }

            return Color.white;

        }

        public void UpdateRing()
        {
            RankModel.Rank immediateRank = relativeScoreAndImmediateRank.immediateRank;
            float relativeScore = (relativeScoreAndImmediateRank.relativeScore) * 100.0f;
            if (Configuration.Instance.showPercentageRing)
            {
                Color nextColor;
                if (Configuration.Instance.percentageRingShowsNextRankColor)
                {
                    nextColor = GetColorForRelativeScore(relativeScore + 1);
                } else
                {
                    nextColor = GetColorForRelativeScore(relativeScore);
                }
                if (progressRing)
                {
                    progressRing.color = nextColor;
                }
            }
            float ringFillAmount = (relativeScoreAndImmediateRank.relativeScore * 100.0f) % 1;
            progressRing.fillAmount = ringFillAmount;
            progressRing.SetVerticesDirty();
        }

        private ImageView CreateRing(Canvas canvas)
        {
            // Unfortunately, there is no guarantee that I have the CoreGameHUDController, since No Text and Huds
            // completely disables it from spawning. So, to be safe, we recreate this all from scratch.
            GameObject imageGameObject = new GameObject("Ring Image", typeof(RectTransform));
            imageGameObject.transform.SetParent(canvas.transform, false);
            ImageView newImage = imageGameObject.AddComponent<ImageView>();
            newImage.enabled = false;
            newImage.material = Utilities.ImageResources.NoGlowMat;
            newImage.sprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(x => x.name == multiplierImageSpriteName);
            newImage.type = Image.Type.Filled;
            newImage.fillClockwise = true;
            newImage.fillOrigin = 2;
            newImage.fillAmount = 1;
            newImage.fillMethod = Image.FillMethod.Radial360;
            newImage.enabled = true;
            return newImage;
        }

        public void OnNoteCut(NoteData data, NoteCutInfo info)
        {
            if (Configuration.Instance.percentMode)
            {
                UpdatePercentModeText();
            } else
            {
                UpdateClassicModeText();
            }
            UpdateRing();
        }

        public void OnNoteMiss(NoteData data)
        {
            if (Configuration.Instance.percentMode)
            {
                UpdatePercentModeText();
            } else
            {
                UpdateClassicModeText();
            }
            UpdateRing();
        }
    }
}
