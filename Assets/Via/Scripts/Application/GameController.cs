using System;
using System.IO;
using UnityEngine;
using Via.Shared;
using Via.Persistence;
using Via.Story.Configuration;
using Via.Story.Domain;

namespace Via.Application
{
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private StoryTableSource tableSource;
        [SerializeField] private MonoBehaviour presentationPlayer;
        [SerializeField] private MonoBehaviour minigameSession;
        private CheckpointStore store;
        public GameFlow Flow { get; private set; }
        public StoryTables Tables { get; private set; }
        public bool Restoring { get; private set; }
        public string StatusMessage { get; private set; }
        public string LoadError { get; private set; }
        public bool HasCheckpoint => store != null && store.Exists;

        private void Awake() { EnsureInitialized(); }

        // Awake does not rerun after a domain reload during play; retry lazily so the game never sits in
        // a half-initialised state where Flow is null and every button silently does nothing.
        public bool EnsureInitialized()
        {
            if (Flow != null) return true;
            try
            {
                if (!(minigameSession is IMinigameSession session)) throw new InvalidOperationException("Bind an IMinigameSession component.");
                if (!(presentationPlayer is IPresentationPlayer player)) throw new InvalidOperationException("Bind an IPresentationPlayer component.");
                var tables = StoryTableLoader.Load(tableSource);
                Tables = tables;
                Flow = new GameFlow(tables, session, player);
                store = new CheckpointStore(Path.Combine(UnityEngine.Application.persistentDataPath, "via-checkpoint.json"));
                LoadError = null;
                enabled = true;
                return true;
            }
            catch (Exception exception)
            {
                Flow = null;
                LoadError = exception.Message;
                Debug.LogException(exception, this);
                enabled = false;
                return false;
            }
        }

        public void StartGame()
        {
            if (!EnsureInitialized()) { StatusMessage = "剧情表加载失败：" + LoadError; return; }
            StatusMessage = null;
            Flow.StartGame();
        }

        public void ContinueGame()
        {
            if (!EnsureInitialized()) { StatusMessage = "读档失败：" + LoadError; return; }
            if (!HasCheckpoint) { StatusMessage = "读档失败：没有可用的存档。"; return; }
            try
            {
                var checkpoint = store.Load();
                Restoring = true;
                Flow.Restore(checkpoint.nodeId, checkpoint.records, checkpoint.levelResults);
                StatusMessage = checkpoint.levelResults == null || checkpoint.levelResults.Length == 0
                    ? "已恢复剧情检查点（旧存档缺少关卡结果，结局分支将使用默认选择）。"
                    : "已恢复剧情检查点；小游戏从关卡入口重新开始。";
            }
            catch (Exception exception) { StatusMessage = "读档失败：" + exception.Message; Debug.LogWarning(StatusMessage, this); }
            finally { Restoring = false; }
        }

        public void SaveCheckpoint()
        {
            if (!EnsureInitialized() || Flow.CurrentNode == null) return;
            try
            {
                var records = new ChoiceRecord[Flow.History.Records.Count];
                Flow.History.Records.CopyTo(records, 0);
                store.Save(new GameCheckpoint
                {
                    nodeId = Flow.CurrentNode.NodeId,
                    records = records,
                    levelResults = Flow.CaptureLevelResults()
                });
                StatusMessage = "已保存剧情检查点（不包含当前画布操作）。";
            }
            catch (Exception exception) { StatusMessage = "存档失败：" + exception.Message; Debug.LogWarning(StatusMessage, this); }
        }

        private void OnDestroy() { Flow?.Dispose(); }
    }
}
