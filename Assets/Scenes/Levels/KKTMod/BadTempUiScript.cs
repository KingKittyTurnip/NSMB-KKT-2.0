using NUnit.Framework;
using Quantum;
using UnityEngine;

public class BadTempUiScript : MonoBehaviour
{

    public SimulationConfig ourconfig;
    public AssetRef<CurrentHazards>[] Rulesets;

    public void SetHazard(int id) {
        ourconfig.CurrentHazards = Rulesets[id];
    }
}
