using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ConvertTrees : EditorWindow
{
    private int prototypeIndexToConvert = 0; // 0 for Ash_1 (first tree in Terrain prototype list)
    private float conversionPercentage = 0.15f; // Converts only 15% of those trees randomly

    [MenuItem("Tools/Convert Selective Terrain Trees")]
    public static void ShowWindow()
    {
        GetWindow<ConvertTrees>("Selective Tree Converter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Convert Specific Terrain Trees to GameObjects", EditorStyles.boldLabel);
        prototypeIndexToConvert = EditorGUILayout.IntField("Tree Prototype Index", prototypeIndexToConvert);
        conversionPercentage = EditorGUILayout.Slider("Spawn Chance (0 - 1)", conversionPercentage, 0.01f, 1.0f);

        if (GUILayout.Button("Convert Matching Trees"))
        {
            Convert();
        }
    }

    public void Convert()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("No active terrain found in scene!");
            return;
        }

        TerrainData data = terrain.terrainData;
        Transform parent = GameObject.Find("Particle_Tree_GameObjects")?.transform
                           ?? new GameObject("Particle_Tree_GameObjects").transform;

        Undo.RegisterCreatedObjectUndo(parent.gameObject, "Convert Terrain Trees");

        List<TreeInstance> remainingTerrainTrees = new List<TreeInstance>();

        foreach (TreeInstance tree in data.treeInstances)
        {
            // Check if tree matches prototype index AND satisfies random chance
            if (tree.prototypeIndex == prototypeIndexToConvert && Random.value <= conversionPercentage)
            {
                Vector3 pos = Vector3.Scale(tree.position, data.size) + terrain.transform.position;
                GameObject treePrefab = data.treePrototypes[tree.prototypeIndex].prefab;
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, parent);
                instance.transform.position = pos;
                instance.transform.rotation = Quaternion.Euler(0, tree.rotation * Mathf.Rad2Deg, 0);
                instance.transform.localScale = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale);
            }
            else
            {
                // Keep the rest on the terrain batcher
                remainingTerrainTrees.Add(tree);
            }
        }

        Undo.RecordObject(data, "Selective Clear Terrain Trees");
        data.treeInstances = remainingTerrainTrees.ToArray();
        Debug.Log($"Successfully converted targeted trees! Remaining terrain trees: {data.treeInstances.Length}");
    }
}