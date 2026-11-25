using System.Collections.Generic;

namespace SignalLost
{
    /// <summary>Root of StreamingAssets/Data/cutscenes.json.</summary>
    public class CutsceneFile
    {
        public List<CutsceneDef> cutscenes = new List<CutsceneDef>();
    }

    /// <summary>An in-engine cutscene: a list of shots played with letterbox bars. Always skippable.</summary>
    public class CutsceneDef
    {
        public string id;
        public List<CutsceneShot> shots = new List<CutsceneShot>();
    }

    public class CutsceneShot
    {
        /// <summary>Seconds this shot lasts (the line types out within it).</summary>
        public float seconds = 3f;
        /// <summary>Camera pose for the shot, eased in over blendSeconds.</summary>
        public float pitch = 22f;
        public float yaw;
        public float distance = 20f;
        public float lookHeight = 1.5f;
        public float blendSeconds = 1.2f;
        /// <summary>hale, iva, static, narrator, or empty for no line.</summary>
        public string speaker;
        public string line;
        public string sfx;
        /// <summary>Optional world beat: dark, alarm, flood, burst, freeze_shape, release, flash.</summary>
        public string action;
        public int count;
    }
}
