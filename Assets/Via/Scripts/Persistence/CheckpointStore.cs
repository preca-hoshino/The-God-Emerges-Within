using System;
using System.IO;
using System.Text;
using UnityEngine;
using Via.Story.Domain;

namespace Via.Persistence
{
    [Serializable]
    public sealed class GameCheckpoint
    {
        public int version = 1;
        public string nodeId;
        public ChoiceRecord[] records;
        // Required by outcome-driven branches (e.g. the act-9 finale); stored so a checkpoint taken
        // after the level can still resolve them.
        public LevelResultMemory[] levelResults;
    }

    public sealed class CheckpointStore
    {
        private readonly string path;
        public bool Exists => File.Exists(path);
        public CheckpointStore(string path) { this.path = path ?? throw new ArgumentNullException(nameof(path)); }

        public void Save(GameCheckpoint checkpoint)
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(checkpoint, true), Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        public GameCheckpoint Load()
        {
            var checkpoint = JsonUtility.FromJson<GameCheckpoint>(File.ReadAllText(path, Encoding.UTF8));
            if (checkpoint == null || checkpoint.version != 1 || string.IsNullOrWhiteSpace(checkpoint.nodeId) || checkpoint.records == null)
                throw new InvalidDataException("Unsupported or invalid checkpoint.");
            return checkpoint;
        }
    }
}
