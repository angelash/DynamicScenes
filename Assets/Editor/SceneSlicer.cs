using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

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
    private Vector2 m_scrollPos;
    private GameObject m_scenePrefab;
    /// <summary>
    /// 检测scenePrefab字段变动
    /// </summary>
    private GameObject m_lastScenePrefab;
    private int m_selected;
    private string m_filePath;

    private GameObject m_borderNode;//用来存放分割线
    private List<GameObject> m_cellPrefabList = new List<GameObject>();

    private void OnGUI()
    {
        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
        EditorGUILayout.BeginVertical();
        m_scenePrefab = EditorGUILayout.ObjectField("scene prefab", m_scenePrefab, typeof(GameObject), false) as GameObject;
        if (m_lastScenePrefab != m_scenePrefab)
        {
            m_lastScenePrefab = m_scenePrefab;
            m_filePath = AssetDatabase.GetAssetPath(m_scenePrefab);
            if (!string.IsNullOrEmpty(m_filePath))
            {
                m_filePath = Path.GetDirectoryName(m_filePath) + "/" + m_scenePrefab.name;
            }
        }
        m_selected = GUILayout.SelectionGrid(m_selected, new string[] { "2x2", "4x4", "8x8", "16x16", "32x32", "64x64" }, 6);
        m_filePath = EditorGUILayout.TextField("File path to store data", m_filePath);
        EditorGUILayout.Separator();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Begin slice"))
        {
            if (m_scenePrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "scene prefab empty.", "OK");
            }
            else
            {
                //var node = GameObject.Instantiate(m_scenePrefab.transform.FindChild("Cacti"));
                SliceScene(m_scenePrefab, (int)Math.Pow(2, m_selected + 1));
            }
        }
        if (GUILayout.Button("Clean up"))
        {
            CleanUpBorder();
            CleanUpCellPrefab();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Separator();
        if (GUILayout.Button("Show Transform Info"))
        {
            Debug.Log(string.Concat(Selection.activeTransform, ": ", Selection.activeTransform.position, " ", Selection.activeTransform.rotation, " ", Selection.activeTransform.localScale));
        }
        if (GUILayout.Button("Normalize Prefab"))
        {
            NormalizePrefab(m_scenePrefab);
        }

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
            var cellNodeDic = new Dictionary<Transform, Transform>[dimension, dimension];//单元格对应原prefab节点的映射（节点有重名）

            var rubbishNode = new GameObject();//用来存放复制节点时多出来的子节点，然后一起删掉
            rubbishNode.name = "rubbish";
            rubbishNode.SetActive(false);

            CreateBorder(startPoint, dimension, cellX, cellZ);

            CleanUpCellPrefab();
            //初始化临时容器
            for (int i = 0; i < dimension; i++)
            {
                for (int j = 0; j < dimension; j++)
                {
                    cellPos[i, j] = new Vector2(startPoint.x + j * cellX, startPoint.z + i * cellZ);
                    cellEndPos[i, j] = new Vector2(startPoint.x + (j + 1) * cellX, startPoint.z + (i + 1) * cellZ);
                    var go = new GameObject();
                    go.name = string.Format("{0}_{1}_{2}", scenePrefab.name, i + 1, j + 1);
                    cellPrefab[i, j] = go;
                    m_cellPrefabList.Add(go);
                    cellNodeDic[i, j] = new Dictionary<Transform, Transform>();
                    cellNodeDic[i, j][scenePrefab.transform] = go.transform;
                }
            }

            var children = GetAllChildren(scenePrefab.transform);
            foreach (var child in children)
            {
                if (child.gameObject == sceneTerrain.gameObject)
                    continue;
                var isConstructorNode = false;//是否为结构节点
                //先判断是否为结构节点
                if (child.transform.childCount != 0)
                {
                    isConstructorNode = true;
                    if (child.gameObject.GetComponent<MeshRenderer>() != null
                    || child.gameObject.GetComponent<Light>() != null
                    || child.gameObject.GetComponent<Animator>() != null
                    || child.gameObject.GetComponent<ParticleSystem>() != null)
                    {
                        Debug.LogError("child contain component: " + child + " childCount: " + child.transform.childCount);//违反设定，结构节点不能作为显示元素
                        foreach (var item in child.transform)
                        {
                            Debug.LogError(item);
                        }
                    }
                }

                //根据规则处理每个节点的复制
                var hasHandle = false;
                for (int i = 0; i < dimension; i++)
                {
                    if (hasHandle)
                        break;
                    for (int j = 0; j < dimension; j++)
                    {
                        if (isConstructorNode)//结构节点每个单元格都加上去
                        {
                            var node = GameObject.Instantiate(child);
                            CleanUpNewTran(node, rubbishNode.transform);
                            node.name = child.name;
                            node.transform.parent = FindCellPrefabParentByTargetTran(child, cellPrefab[i, j].transform, cellNodeDic[i, j]);
                            CopyTranInfo(child, node);
                            cellNodeDic[i, j][child] = node;
                        }
                        else
                        {//显示节点需要按照坐标分配
                            var x = child.position.x;
                            var z = child.position.z;
                            if (cellPos[i, j].x < x && x < cellEndPos[i, j].x && cellPos[i, j].y < z && z < cellEndPos[i, j].y)
                            {
                                var node = GameObject.Instantiate(child);
                                CleanUpNewTran(node, rubbishNode.transform);
                                node.name = child.name;
                                node.transform.parent = FindCellPrefabParentByTargetTran(child, cellPrefab[i, j].transform, cellNodeDic[i, j]);
                                CopyTranInfo(child, node);
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

            //保存切割后prefab
            for (int i = 0; i < m_cellPrefabList.Count; i++)
            {
                var item = m_cellPrefabList[i];
                var prefabPath = string.Concat(m_filePath, "/", item.name, ".prefab");
                if (!Directory.Exists(m_filePath))
                    Directory.CreateDirectory(m_filePath);
                PrefabUtility.CreatePrefab(prefabPath, item);
                m_cellPrefabList[i] = PrefabUtility.ConnectGameObjectToPrefab(item, AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            }
            AssetDatabase.SaveAssets();

            //复制一份到新建的独立场景中
            for (int i = 0; i < m_cellPrefabList.Count; i++)
            {
                var item = m_cellPrefabList[i];
                var prefabPath = string.Concat(m_filePath, "/", item.name, ".prefab");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var go = GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                EditorSceneManager.SaveScene(scene, string.Concat(m_filePath, "/", item.name, ".unity"));
            }

            //删除原来的prefab实例
            for (int i = 0; i < m_cellPrefabList.Count; i++)
            {
                GameObject.DestroyImmediate(m_cellPrefabList[i]);
            }

        }
        else
        {
            Debug.Log("no Terrain");
        }
    }

    private void NormalizePrefab(GameObject scenePrefab)
    {
        var prefabPath = AssetDatabase.GetAssetPath(scenePrefab);
        var tempPrefab = GameObject.Instantiate(scenePrefab);
        tempPrefab.name = scenePrefab.name;

        var children = GetAllChildren(scenePrefab.transform);

        var rubbishNode = new GameObject();//用来存放复制节点时多出来的子节点，然后一起删掉
        rubbishNode.name = "rubbish";
        rubbishNode.SetActive(false);

        foreach (var child in children)
        {
            if (child.transform.childCount != 0)
            {
                if (child.gameObject.GetComponent<MeshRenderer>() != null
                || child.gameObject.GetComponent<Light>() != null
                || child.gameObject.GetComponent<Animator>() != null
                || child.gameObject.GetComponent<ParticleSystem>() != null)
                {
                    Debug.Log("child contain component: " + child + " childCount: " + child.transform.childCount);//违反设定，结构节点不能作为显示元素
                    foreach (Transform item in child.transform)
                    {
                        Debug.Log(item);
                        item.parent = rubbishNode.transform;
                    }
                }
            }
        }

        PrefabUtility.CreatePrefab(prefabPath, tempPrefab, ReplacePrefabOptions.ReplaceNameBased);
        AssetDatabase.SaveAssets();

        GameObject.DestroyImmediate(tempPrefab);
        GameObject.DestroyImmediate(rubbishNode);
    }

    private void CopyTranInfo(Transform srcTran, Transform tarTran)
    {
        tarTran.localPosition = srcTran.localPosition;
        tarTran.localRotation = srcTran.localRotation;
        tarTran.localScale = srcTran.localScale;
    }

    private void CreateBorder(Vector3 startPoint, int dimension, float cellX, float cellZ)
    {
        CleanUpBorder();

        m_borderNode = new GameObject();
        m_borderNode.name = "border";
        var totalX = cellX * dimension;
        var totalZ = cellZ * dimension;

        for (int i = 0; i < dimension; i++)//竖向分割线
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.parent = m_borderNode.transform;
            cube.transform.position = new Vector3(startPoint.x + cellX * i, startPoint.y, startPoint.z + totalZ / 2);
            cube.transform.localScale = new Vector3(1, 1, totalZ);
        }
        var cubeX = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeX.transform.parent = m_borderNode.transform;
        cubeX.transform.position = new Vector3(startPoint.x + totalX, startPoint.y, startPoint.z + totalZ / 2);
        cubeX.transform.localScale = new Vector3(1, 1, totalZ);

        for (int i = 0; i < dimension; i++)//横向分割线
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.parent = m_borderNode.transform;
            cube.transform.position = new Vector3(startPoint.x + totalX / 2, startPoint.y, startPoint.z + cellZ * i);
            cube.transform.localScale = new Vector3(totalX, 1, 1);
        }
        var cubeZ = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeZ.transform.parent = m_borderNode.transform;
        cubeZ.transform.position = new Vector3(startPoint.x + totalX / 2, startPoint.y, startPoint.z + totalZ);
        cubeZ.transform.localScale = new Vector3(totalX, 1, 1);
    }

    private void CleanUpBorder()
    {
        if (m_borderNode)
            GameObject.DestroyImmediate(m_borderNode);
    }

    private void CleanUpCellPrefab()
    {
        foreach (var item in m_cellPrefabList)
        {
            if (item)
            {
                AssetDatabase.DeleteAsset(string.Concat(m_filePath, "/", item.name, ".prefab"));
                GameObject.DestroyImmediate(item);
            }
        }
        m_cellPrefabList.Clear();
        if (Directory.Exists(m_filePath))
        {
            Directory.Delete(m_filePath, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
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

    private Transform FindCellPrefabParentByTargetTran(Transform sourceTran, Transform cellTran, Dictionary<Transform, Transform> cellNodeDic)
    {
        if (cellNodeDic.ContainsKey(sourceTran.parent))
            return cellNodeDic[sourceTran.parent];
        else
            Debug.LogError("parent not exist: " + sourceTran.parent);
        return null;
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