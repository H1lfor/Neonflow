using System;

namespace Neonflow.VectorUI
{
    [Serializable]
    public class VectorLayouts
    {
        public VectorScreen[] screens;
    }

    [Serializable]
    public class VectorScreen
    {
        public string id;
        public bool background;
        public VectorNode[] nodes;
    }

    [Serializable]
    public class VectorNode
    {
        public string id, kind, text, secondary, valueText, path, color, weight, align, target;
        public float x, y, width, height, fontSize, cut, opacity = 1, value, rotation;
        public bool italic, accent, selected;
    }

    [Serializable]
    public class VectorManifest
    {
        public VectorAsset[] assets;
    }

    [Serializable]
    public class VectorAsset
    {
        public string path, role;
        public float[] border;
    }

    [Serializable]
    public class VectorAnimations
    {
        public VectorSequence[] sequences;
    }

    [Serializable]
    public class VectorSequence
    {
        public string id, clock;
        public int frameCount, fps, width, height;
        public bool loop;
        public string[] frames;
    }
}
