using UnityEngine;
using Via.Story.Configuration;
using Via.Story.Domain;

namespace Via.Application
{
    // Turns story progression into sound: music cues, ambience beds, beat effects and the growth
    // bed during levels. After a checkpoint restore it resolves the latest cue at or before the
    // node instead of replaying one-shots, so audio state stays consistent with the running scene.
    public sealed class StoryAudioDirector : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private AudioController audioController;
        [SerializeField] private AudioLibrary library;
        [SerializeField] private float musicCrossfade = 1.5f;
        [SerializeField] private float bedFade = 1.5f;

        private GameFlow flow;
        private string lastNodeId;

        private void Start()
        {
            if (controller == null || audioController == null || library == null)
            {
                Debug.LogError("StoryAudioDirector 需要绑定 GameController、AudioController 与 AudioLibrary。", this);
                enabled = false;
                return;
            }
            if (controller.Flow == null)
            {
                Debug.LogWarning("剧情未成功加载，音频导演停止。", this);
                enabled = false;
                return;
            }
            flow = controller.Flow;
            flow.Changed += OnFlowChanged;
            ApplyState(true);
        }

        private void OnDisable()
        {
            if (flow != null) { flow.Changed -= OnFlowChanged; flow = null; }
        }

        private void OnFlowChanged() => ApplyState(false);

        private void ApplyState(bool initial)
        {
            var tables = controller.Tables;
            if (tables == null) return;
            if (controller.Restoring) initial = true;

            var node = flow.CurrentNode;
            if (node == null)
            {
                SetTitleMusic(initial);
                audioController.StopAmbience(initial ? 0f : 0.8f);
                audioController.SetGrowthBed(library.GrowthBed, false, bedFade);
                lastNodeId = null;
                return;
            }

            bool entered = !initial && node.NodeId != lastNodeId;
            if (entered) PlayNodeEffects(tables, node);
            ResolveCues(tables, node, out string music, out string musicNode, out string ambience, out string ambienceNode);
            ApplyMusic(music, musicNode, node.NodeId, initial, entered);
            ApplyAmbience(ambience, ambienceNode, node.NodeId, initial, entered);
            audioController.SetGrowthBed(library.GrowthBed, flow.Stage == FlowStage.Minigame, bedFade);
            lastNodeId = node.NodeId;
        }

        // SFX only fire on a real node change; pause and candy confirmation also raise Changed.
        private void PlayNodeEffects(StoryTables tables, FlowRow node)
        {
            if (!tables.TryGetAudioCue(node.NodeId, out var cue)) return;
            foreach (string id in AudioCueRow.Split(cue.Sfx))
            {
                if (library.TryGet(id, out var entry) && entry.clip != null) audioController.PlaySfx(entry.clip);
                else Debug.LogWarning("音频库缺少音效：" + id, this);
            }
        }

        // Nearest cue at or before the node: restoring mid-act must not leave earlier music running.
        // The cue's own node is kept so one-shot content only fires when that exact node is entered.
        private void ResolveCues(StoryTables tables, FlowRow node, out string music, out string musicNode, out string ambience, out string ambienceNode)
        {
            music = null;
            musicNode = null;
            ambience = null;
            ambienceNode = null;
            if (!tables.TryGetFlowIndex(node.NodeId, out int index)) return;
            for (int i = index; i >= 0 && (music == null || ambience == null); i--)
            {
                var row = tables.FlowOrder[i];
                if (!tables.TryGetAudioCue(row.NodeId, out var cue)) continue;
                if (music == null && cue.HasMusic) { music = cue.Music; musicNode = row.NodeId; }
                if (ambience == null && cue.HasAmbience) { ambience = cue.Ambience; ambienceNode = row.NodeId; }
            }
        }

        private void SetTitleMusic(bool initial)
        {
            if (library.SoftMusic == null) return;
            if (initial || audioController.CurrentMusic == null) audioController.PlayMusicNow(library.SoftMusic, true);
            else audioController.PlayStoryMusic(library.SoftMusic, true, musicCrossfade, 0f, musicCrossfade);
        }

        private void ApplyMusic(string cue, string cueNode, string nodeId, bool initial, bool entered)
        {
            if (string.IsNullOrEmpty(cue)) return;
            // "stop" acts like a one-shot command of its own node; lingering must not fade repeatedly.
            if (cue == "stop")
            {
                if (initial || (entered && nodeId == cueNode)) audioController.FadeOutMusic(musicCrossfade);
                return;
            }
            var clip = library.GetMusic(cue);
            if (clip == null) { Debug.LogWarning("音频库缺少音乐：" + cue, this); return; }
            bool loop = cue != "ad" && cue != "soft_end";
            // One-shot tracks (the ad, the ending splice) fire only when their own node is entered;
            // pausing, restoring or lingering in the segment must not restart them.
            if (!loop && !(entered && nodeId == cueNode)) return;
            if (initial)
            {
                audioController.PlayMusicNow(clip, loop);
                return;
            }
            // The ad punches in after a short gap; the soft ending splices into the running loop.
            float fadeOut = cue == "ad" ? 0.6f : musicCrossfade;
            float delay = cue == "ad" ? 0.4f : 0f;
            float fadeIn = cue == "ad" ? 0.3f : musicCrossfade;
            audioController.PlayStoryMusic(clip, loop, fadeOut, delay, fadeIn);
        }

        private void ApplyAmbience(string cue, string cueNode, string nodeId, bool initial, bool entered)
        {
            if (string.IsNullOrEmpty(cue)) return;
            if (cue == "none")
            {
                audioController.StopAmbience(initial ? 0f : 1f);
                return;
            }
            AudioClip loopClip = null;
            foreach (string id in AudioCueRow.Split(cue))
            {
                if (!library.TryGet(id, out var entry) || entry.clip == null)
                {
                    Debug.LogWarning("音频库缺少环境音：" + id, this);
                    continue;
                }
                if (entry.loop) loopClip = entry.clip;
                // Tails (room tone, TV) sound once when the cue's own node is entered.
                else if (entered && nodeId == cueNode) audioController.PlaySfx(entry.clip);
            }
            if (loopClip != null) audioController.SetAmbience(loopClip);
            else audioController.StopAmbience(initial ? 0f : 0.8f);
        }
    }
}
