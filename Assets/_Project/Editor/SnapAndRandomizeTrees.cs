using UnityEngine;
using UnityEditor;

public class SnapAndRandomizeTrees : MonoBehaviour
{
    [MenuItem("Tools/Ground and Randomize Selected Trees")]
    public static void SnapAndRandomize()
    {
        GameObject[] selectedTrees = Selection.gameObjects;

        if (selectedTrees.Length == 0)
        {
            Debug.LogWarning("No trees selected! Highlight your tree objects in the Hierarchy first.");
            return;
        }

        Undo.RecordObjects(selectedTrees, "Snap and Randomize Trees");

        foreach (GameObject tree in selectedTrees)
        {
            // 1. Raycast downward from above to find exact ground point
            if (Physics.Raycast(tree.transform.position + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f))
            {
                tree.transform.position = hit.point;
            }

            // 2. Wider uniform scale range (0.35x for short saplings up to 1.6x for tall trees)
            float overallScale = Random.Range(0.35f, 1.6f);

            // 3. Slight extra height variation (Y-axis stretching)
            float heightMultiplier = Random.Range(0.85f, 1.25f);

            tree.transform.localScale = new Vector3(
                overallScale,
                overallScale * heightMultiplier,
                overallScale
            );

            // 4. Randomize Y-axis rotation
            tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        Debug.Log($"Successfully randomized scale and grounded {selectedTrees.Length} trees!");
    }

    [MenuItem("Tools/Ground Selected Objects")]
    public static void GroundObjects()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0) return;

        Undo.RecordObjects(selected, "Ground Selected Objects");

        foreach (GameObject obj in selected)
        {
            // Raycast down from above the object to hit the terrain/ground
            if (Physics.Raycast(obj.transform.position + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 100f))
            {
                // Align base of bounding box or pivot to ground
                Renderer r = obj.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    float bottomOffset = obj.transform.position.y - r.bounds.min.y;
                    obj.transform.position = hit.point + Vector3.up * bottomOffset;
                }
                else
                {
                    obj.transform.position = hit.point;
                }
            }
        }
    }
}