using UnityEngine;

namespace Via.Story.Configuration
{
    // Serialized references to the CSV text assets. Writers edit the CSVs; this asset wires them in.
    [CreateAssetMenu(menuName = "Via/Story Table Source")]
    public sealed class StoryTableSource : ScriptableObject
    {
        [SerializeField] private string entryNodeId = "F001";
        [SerializeField] private TextAsset flow;
        [SerializeField] private TextAsset dialogue;
        [SerializeField] private TextAsset presentation;
        [SerializeField] private TextAsset levelResults;
        [SerializeField] private TextAsset audio;

        public string EntryNodeId => entryNodeId;
        public TextAsset Flow => flow;
        public TextAsset Dialogue => dialogue;
        public TextAsset Presentation => presentation;
        public TextAsset LevelResults => levelResults;
        public TextAsset Audio => audio;
    }
}
