using UnityEngine;
using UnityEditor;

public class TerrainTreeScaler : EditorWindow
{
    [MenuItem("Tools/Scale 60 Percent of Ash_1 Trees")]
    public static void ScaleTrees()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("No active terrain found in the scene!");
            return;
        }

        TerrainData data = terrain.terrainData;
        TreeInstance[] instances = data.treeInstances;

        // Find the prototype index for Ash_1
        int protoIndex = -1;
        for (int i = 0; i < data.treePrototypes.Length; i++)
        {
            if (data.treePrototypes[i].prefab != null && data.treePrototypes[i].prefab.name.Contains("Ash_1"))
            {
                protoIndex = i;
                break;
            }
        }

        if (protoIndex == -1)
        {
            Debug.LogError("Ash_1 tree prototype not found on the active terrain!");
            return;
        }

        int modifiedCount = 0;
        for (int i = 0; i < instances.Length; i++)
        {
            if (instances[i].prototypeIndex == protoIndex)
            {
                // Target roughly 60% of the trees randomly for variety
                if (Random.value <= 0.6f)
                {
                    instances[i].heightScale *= 1.8f; // Make them taller
                    instances[i].widthScale *= 1.4f;  // Scale width proportionally
                    modifiedCount++;
                }
            }
        }

        data.treeInstances = instances;
        Debug.Log($"Successfully scaled {modifiedCount} Ash_1 trees while keeping their positions!");
    }
}