using UnityEngine;
using UnityEditor;

public class ConvertTrees : EditorWindow
{
    [MenuItem("Tools/Convert Terrain Trees to GameObjects")]
    public static void Convert()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("No active terrain found in scene!");
            return;
        }

        TerrainData data = terrain.terrainData;
        Transform parent = new GameObject("Tree_GameObjects").transform;

        Undo.RegisterCreatedObjectUndo(parent.gameObject, "Convert Terrain Trees");

        foreach (TreeInstance tree in data.treeInstances)
        {
            Vector3 pos = Vector3.Scale(tree.position, data.size) + terrain.transform.position;
            GameObject treePrefab = data.treePrototypes[tree.prototypeIndex].prefab;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, parent);
            instance.transform.position = pos;
            instance.transform.rotation = Quaternion.Euler(0, tree.rotation * Mathf.Rad2Deg, 0);
            instance.transform.localScale = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale);
        }

        Undo.RecordObject(data, "Clear Terrain Trees");
        data.treeInstances = new TreeInstance[0];
        Debug.Log("Successfully converted terrain trees to active GameObjects!");
    }
}