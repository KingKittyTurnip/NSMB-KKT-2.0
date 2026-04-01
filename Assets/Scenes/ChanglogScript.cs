using NUnit.Framework;
using Quantum;
using System;
using UnityEngine;

public class ChangelogScript : MonoBehaviour
{
    public ChangelogEntry[] VersionChanges;

    [Serializable]
    public class ChangelogEntry {
        public string Version = "beta 2.0.0";
        public string VanillaVersionBase = "2.1.0";
        public string[] MajorChanges;
        public string[] Changes;
    }
}
