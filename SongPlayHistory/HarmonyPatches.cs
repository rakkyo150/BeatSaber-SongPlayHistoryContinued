using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Object;

namespace SongPlayHistoryContinued
{
    [HarmonyPatch(typeof(LevelListTableCell))]
    [HarmonyPatch("SetDataFromLevelAsync", MethodType.Normal)]
    internal class SetDataFromLevelAsync
    {
        private static Sprite _thumbsUp;
        private static Sprite _thumbsDown;

        public static bool Prepare()
        {
            _thumbsUp ??= LoadSpriteFromResource($"SongPlayHistoryContinued.Assets.ThumbsUp.png");
            _thumbsDown ??= LoadSpriteFromResource($"SongPlayHistoryContinued.Assets.ThumbsDown.png");

            return SPHModel.ScanVoteData();
        }

        [HarmonyAfter(new string[] { "com.kyle1413.BeatSaber.SongCore" })]
        public static void Postfix(LevelListTableCell __instance, IPreviewBeatmapLevel level, bool isFavorite,
            Image ____favoritesBadgeImage,
            TextMeshProUGUI ____songBpmText)
        {
            if (float.TryParse(____songBpmText?.text, out float bpm))
            {
                ____songBpmText.text = bpm.ToString("0");
            }

            Image voteIcon = null;
            foreach (var image in __instance.GetComponentsInChildren<Image>())
            {
                // For performance reason, avoid using Linq.
                if (image.name == "Vote")
                {
                    voteIcon = image;
                    break;
                }
            }
            if (voteIcon == null)
            {
                voteIcon = Instantiate(____favoritesBadgeImage, __instance.transform);
                voteIcon.name = "Vote";
                voteIcon.rectTransform.sizeDelta = new Vector2(2.5f, 2.5f);
                voteIcon.color = new Color(1f, 1f, 1f, 0.3f);
            }
            voteIcon.enabled = false;

            if (!isFavorite && SPHModel.Votes.TryGetValue(level.levelID.Replace("custom_level_", "").ToLower(), out var vote))
            {
                voteIcon.sprite = vote.voteType == "Upvote" ? _thumbsUp : _thumbsDown;
                voteIcon.enabled = true;
            }
        }

        public static void OnUnpatch()
        {
            foreach (var image in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (image.name == "Vote")
                {
                    Destroy(image.gameObject);
                }
            }
        }

        private static Sprite LoadSpriteFromResource(string resourcePath)
        {
            try
            {
                using var stream = Assembly.GetCallingAssembly().GetManifestResourceStream(resourcePath);
                var resource = new byte[stream.Length];
                stream.Read(resource, 0, (int)stream.Length);

                var texture = new Texture2D(2, 2);
                texture.LoadImage(resource);

                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 0));
                return sprite;
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error("Error while loading a resource.\n" + ex.ToString());
                return null;
            }
        }
    }

    [HarmonyPatch(typeof(LevelStatsView))]
    [HarmonyPatch("ShowStats", MethodType.Normal)]
    class LevelStatsViewPatches : LevelStatsView
    {
        static void Postfix(ref TextMeshProUGUI ____maxComboText)
        {
            Plugin.Log?.Debug("Change Max Combo text");
            var beatmap = BeatSaberUI.LevelDetailViewController?.selectedDifficultyBeatmap;
            SetMaxComboAndMiss(beatmap, ____maxComboText);
        }

        static void SetMaxComboAndMiss(IDifficultyBeatmap beatmap, TextMeshProUGUI ____maxComboText)
        {
            var config = PluginConfig.Instance;
            var stats = SPHModel.GetPlayerStats(beatmap);
            var records = SPHModel.GetRecords(beatmap);

            if (beatmap == null)
            {
                Plugin.Log?.Debug("No Beatmap Data");
                return;
            }

            if (records.Count() == 0)
            {
                Plugin.Log?.Debug("No Play Records");
                return;
            }
            
            if (config.SortByDate)
            {
                records = records.OrderByDescending(s => s.ModifiedScore).ToList();
            }

            string maxCombo = stats.validScore ? (stats.fullCombo ? "FULL COMBO" : $"{stats.maxCombo} ({records.First().Miss}miss)") : "-";
            ____maxComboText.text = maxCombo;
        }
    }
}
