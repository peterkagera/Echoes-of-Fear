#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Splines;
using System.Collections.Generic;

public class ClearTreesOnRoad : EditorWindow
{
    private Terrain terrain;
    private SplineContainer roadSpline;
    private float clearRadius = 4.0f;

    [MenuItem("Tools/Clear Trees On Road")]
    public static void ShowWindow()
    {
        GetWindow<ClearTreesOnRoad>("Clear Trees On Road");
    }

    private void OnGUI()
    {
        GUILayout.Label("Road Tree Clearer", EditorStyles.boldLabel);

        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        roadSpline = (SplineContainer)EditorGUILayout.ObjectField("Road Spline", roadSpline, typeof(SplineContainer), true);
        clearRadius = EditorGUILayout.FloatField("Clear Radius (Meters)", clearRadius);

        if (GUILayout.Button("Remove Trees From Road"))
        {
            if (terrain == null || roadSpline == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign both the Terrain and Road Spline Container.", "OK");
                return;
            }

            RemoveTrees();
        }
    }

    private void RemoveTrees()
    {
        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = data.size;

        List<TreeInstance> keptTrees = new List<TreeInstance>();
        int removedCount = 0;

        foreach (TreeInstance tree in data.treeInstances)
        {
            // Convert normalized tree position to world space
            Vector3 worldTreePos = Vector3.Scale(tree.position, terrainSize) + terrainPos;

            // Find closest point on spline
            Vector3 localTreePos = roadSpline.transform.InverseTransformPoint(worldTreePos);
            SplineUtility.GetNearestPoint(roadSpline.Spline, localTreePos, out var nearestLocalPoint, out _);
            Vector3 nearestWorldPoint = roadSpline.transform.TransformPoint(nearestLocalPoint);

            // Calculate horizontal distance (ignore elevation differences)
            worldTreePos.y = 0;
            nearestWorldPoint.y = 0;

            if (Vector3.Distance(worldTreePos, nearestWorldPoint) >= clearRadius)
            {
                keptTrees.Add(tree);
            }
            else
            {
                removedCount++;
            }
        }

        Undo.RegisterCompleteObjectUndo(data, "Clear Trees On Road");
        data.treeInstances = keptTrees.ToArray();
        terrain.Flush();

        Debug.Log($"Successfully removed {removedCount} trees near the road!");
    }
}
#endif