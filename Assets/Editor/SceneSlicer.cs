using UnityEngine;
using UnityEditor;
using System.Collections;
using System;
using System.Collections.Generic;

public class SceneSlicer
{
    [MenuItem("SceneSlicer/SliceScene")]
    public static void SliceScene()
    {
        var wizard = EditorWindow.GetWindow<SceneSlicerWizard>();
        wizard.Show();
    }
}

public class SceneSlicerWizard : EditorWindow
{
    private Vector2 scrollPos;
    private GameObject m_scenePrefab;
    private int selected;

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical();
        m_scenePrefab = EditorGUILayout.ObjectField("scene prefab", m_scenePrefab, typeof(GameObject), false) as GameObject;
        selected = GUILayout.SelectionGrid(selected, new string[] { "2x2", "4x4", "8x8", "16x16", "32x32", "64x64" }, 6);
        if (GUILayout.Button("begin slice"))
        {
            if (m_scenePrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "scene prefab empty.", "OK");
            }
            else
            {
                //var node = GameObject.Instantiate(m_scenePrefab.transform.FindChild("Cacti"));
                SliceScene(m_scenePrefab, (int)Math.Pow(2, selected + 1));
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void SliceScene(GameObject scenePrefab, int dimension)
    {
        Debug.Log("dimension " + dimension);
        Terrain sceneTerrain = scenePrefab.GetComponentInChildren<Terrain>();

        if (sceneTerrain != null)
        {
            var startPoint = sceneTerrain.transform.position;
            var sceneSize = sceneTerrain.terrainData.size;

            var cellX = sceneSize.x / dimension;
            var cellZ = sceneSize.z / dimension;

            var cellPos = new Vector2[dimension, dimension];
            var cellEndPos = new Vector2[dimension, dimension];
            var cellPrefab = new GameObject[dimension, dimension];

            var rubbishNode = new GameObject();//用来存放复制节点时多出来的子节点，然后一起删掉
            rubbishNode.name = "rubbish";
            rubbishNode.SetActive(false);

            for (int i = 0; i < dimension; i++)
            {
                for (int j = 0; j < dimension; j++)
                {
                    cellPos[i, j] = new Vector2(startPoint.x + j * cellX, startPoint.z + i * cellZ);
                    cellEndPos[i, j] = new Vector2(startPoint.x + (j + 1) * cellX, startPoint.z + (i + 1) * cellZ);
                    var go = new GameObject();
                    go.name = string.Format("{0}_{1}_{2}", scenePrefab.name, i + 1, j + 1);
                    cellPrefab[i, j] = go;
                }
            }

            var children = GetAllChildren(scenePrefab.transform);
            foreach (var child in children)
            {
                if (child.gameObject == sceneTerrain.gameObject)
                    continue;
                var isConstructorNode = false;//是否为结构节点
                if(child.transform.childCount != 0)
                {
                    isConstructorNode = true;
                    if (child.gameObject.GetComponent<MeshRenderer>() != null
                    || child.gameObject.GetComponent<Light>() != null
                    || child.gameObject.GetComponent<ParticleSystem>() != null)
                    {
                        Debug.LogError("child contain component: " + child);//违反设定，结构节点不能作为显示元素
                    }
                }
                var hasHandle = false;
                for (int i = 0; i < dimension; i++)
                {
                    if (hasHandle)
                        break;
                    for (int j = 0; j < dimension; j++)
                    {
                        if (isConstructorNode)
                        {
                            var node = GameObject.Instantiate(child);
                            CleanUpNewTran(node, rubbishNode.transform);
                            node.name = child.name;
                            node.transform.parent = FindCellPrefabParentByTargetTran(child, cellPrefab[i, j].transform);
                        }
                        else
                        {
                            var x = child.position.x;
                            var z = child.position.z;
                            if (cellPos[i, j].x < x && x < cellEndPos[i, j].x && cellPos[i, j].y < z && z < cellEndPos[i, j].y)
                            {
                                var node = GameObject.Instantiate(child);
                                CleanUpNewTran(node, rubbishNode.transform);
                                node.name = child.name;
                                node.transform.parent = FindCellPrefabParentByTargetTran(child, cellPrefab[i, j].transform);
                                hasHandle = true;
                                break;
                            }
                        }
                    }
                }
            }
            Debug.Log("children " + children.Count);

            Debug.Log(sceneTerrain + " " + startPoint + " " + sceneSize);

            GameObject.DestroyImmediate(rubbishNode);
        }
    }

    private void CleanUpNewTran(Transform node, Transform rubbishNode)
    {
        List<Transform> temp = new List<Transform>();
        foreach (Transform item in node)
        {
            temp.Add(item);
        }
        foreach (var item in temp)
        {
            item.parent = rubbishNode.transform;
        }
    }

    private Transform FindCellPrefabParentByTargetTran(Transform sourceTran, Transform cellTran)
    {
        var stack = new Stack<string>();
        var sourceParent = sourceTran;
        while (sourceParent.parent != null)
        {
            stack.Push(sourceParent.parent.name);
            sourceParent = sourceParent.parent;
        }

        Transform result = cellTran;
        while (stack.Count != 0)
        {
            var child = cellTran.FindChild(stack.Pop());
            if (child != null)
            {
                result = child;
            }
            else
            {
                break;
            }
        }
        return result;
    }

    private List<Transform> GetAllChildren(Transform tran)
    {
        List<Transform> children = new List<Transform>();
        foreach (Transform item in tran)
        {
            children.Add(item);
            children.AddRange(GetAllChildren(item));
        }
        return children;
    }
}