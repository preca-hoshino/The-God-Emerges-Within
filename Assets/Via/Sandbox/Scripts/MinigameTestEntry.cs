using System;
using UnityEngine;
using Via.Shared;
using Via.Minigame.Runtime;
using Via.Minigame.Presentation;

namespace Via.Development
{
    public sealed class MinigameTestEntry : MonoBehaviour
    {
        [SerializeField] private MinigameController controller;
        [SerializeField] private CanvasPointerInput pointerInput;
        [SerializeField] private Font uiFont;
        private bool resultPanel;
        private string message = "点击虚线范围内放置吸引点。橙色为起点，绿色为生长端。";

        private void Start()
        {
            if (controller == null || controller.Configuration == null || pointerInput == null)
            {
                Debug.LogError("Test entry references are missing.", this);
                enabled = false;
                return;
            }
            controller.OnLevelCompleted += Completed;
            controller.OnOpponentCompleted += OpponentCompleted;
            controller.StartLevel(new LevelStartRequest(controller.Configuration.LevelId, Guid.NewGuid().ToString("N")));
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.OnLevelCompleted -= Completed;
                controller.OnOpponentCompleted -= OpponentCompleted;
            }
        }

        private void Completed(LevelResult result)
        {
            message = "已确认：" + result.ParadigmId + "（测试入口已接收结果）";
            Debug.Log($"Level completed: {result.LevelId}, {result.ResultId}, session {result.SessionId}", this);
        }

        private void OpponentCompleted(LevelResult result)
        {
            message = "医生已达成：" + result.ParadigmId + "（测试入口已接收对手结果）";
        }

        private void OnGUI()
        {
            if (controller == null || !controller.IsOpen) return;
            Font previousFont = GUI.skin.font;
            bool previousEnabled = GUI.enabled;
            try
            {
                if (uiFont != null) GUI.skin.font = uiFont;
                DrawPanel();
            }
            finally
            {
                GUI.skin.font = previousFont;
                GUI.enabled = previousEnabled;
            }
        }

        private void DrawPanel()
        {
            GUI.Label(new Rect(24, 12, Screen.width - 48, 24), "意识井 · 基础玩法测试 | 工具：吸引点 | 目标：直线 / 折线");
            GUI.enabled = !resultPanel && !controller.IsCompleted;
            if (GUI.Button(new Rect(24, 42, 100, 30), "重新开始")) controller.ResetLevel();
            if (GUI.Button(new Rect(136, 42, 100, 30), controller.IsPaused ? "继续" : "暂停"))
            {
                if (controller.IsPaused) controller.ResumeLevel(); else controller.PauseLevel();
            }
            GUI.enabled = controller.CanEdit && !resultPanel;
            if (GUI.Button(new Rect(248, 42, 100, 30), "提交检查"))
            {
                var outcome = controller.CheckResult();
                message = outcome == null ? "尚未达成有效目标，或路径仍在生长，请继续修改。" : "已达成：" + outcome.displayName;
                if (outcome != null) { resultPanel = true; pointerInput.IsBlocked = true; controller.PauseLevel(); }
            }
            GUI.enabled = true;
            GUI.Label(new Rect(24, 80, Screen.width - 48, 24), message);
            if (!resultPanel) return;
            Rect panel = new Rect(Screen.width / 2f - 180, Screen.height / 2f - 70, 360, 140);
            GUI.Box(panel, "确认当前结果？");
            if (GUI.Button(new Rect(panel.x + 24, panel.y + 55, 140, 36), "返回修改"))
            {
                resultPanel = false; pointerInput.IsBlocked = false; controller.ResumeLevel();
            }
            if (GUI.Button(new Rect(panel.x + 196, panel.y + 55, 140, 36), "确认结果"))
            {
                controller.ResumeLevel();
                controller.ConfirmResult();
                resultPanel = false;
                pointerInput.IsBlocked = false;
            }
        }
    }
}
