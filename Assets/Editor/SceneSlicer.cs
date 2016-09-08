using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using GameLoader.Utils.XML;
using System.Security;
using GameLoader.Utils;
using System.Reflection;

public class SceneSlicer
{
    [MenuItem("Slice Map/Slice Map")]
    public static void SliceMap()
    {
        var wizard = EditorWindow.GetWindow<SliceMapWizard>();
        wizard.Show();
    }
}

public class SliceMapWizard : EditorWindow
{
    private Vector2 m_scrollPos;
    private GameObject m_scenePrefab;
    private GameObject m_instanceScenePrefab;
    /// <summary>
    /// 检测scenePrefab字段变动
    /// </summary>
    private GameObject m_lastScenePrefab;
    private int m_selected;
    private string m_filePath;
    private float m_scaleInLightmap;

    private GameObject m_borderNode;//用来存放分割线
    private List<GameObject> m_cellPrefabList = new List<GameObject>();
    private List<Scene> m_cellSceneList = new List<Scene>();
    public CtrlSliceTerrain m_ctrl = new CtrlSliceTerrain();
    public bool overwrite = true;

    private const string DATA_DYNAMIC_MAP = "Assets/Resources/data/dynamic_maps/";

    private void OnFocus()
    {
        if (m_instanceScenePrefab == null)
        {
            var name = EditorSceneManager.GetActiveScene().name;
            m_instanceScenePrefab = FindGameObject(name);

            if (m_instanceScenePrefab)
            {
                var go = PrefabUtility.GetPrefabParent(m_instanceScenePrefab);
                m_scenePrefab = go as GameObject;
                Debug.Log(AssetDatabase.GetAssetPath(go));
            }
        }
    }

    private void OnGUI()
    {
        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
        EditorGUILayout.BeginVertical();
        m_scenePrefab = EditorGUILayout.ObjectField("scene prefab", m_scenePrefab, typeof(GameObject), false) as GameObject;
        if (m_lastScenePrefab != m_scenePrefab)
        {
            m_instanceScenePrefab = GameObject.Find(m_scenePrefab.name);
            var terr = m_scenePrefab.GetComponentInChildren<Terrain>();
            SerializedObject so = new SerializedObject(terr);
            m_scaleInLightmap = so.FindProperty("m_ScaleInLightmap").floatValue;
            Debug.Log("m_scaleInLightmap: " + m_scaleInLightmap);
            m_lastScenePrefab = m_scenePrefab;
            m_filePath = AssetDatabase.GetAssetPath(m_scenePrefab);
            if (!string.IsNullOrEmpty(m_filePath))
            {
                m_filePath = Path.GetDirectoryName(m_filePath) + "/" + m_scenePrefab.name + "_p";
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
                SliceMap(m_scenePrefab, GetDimension());
            }
        }
        if (GUILayout.Button("Clean up"))
        {
            if (EditorUtility.DisplayDialog("警告", "确定删除所有切块资源吗?", "是", "否"))
            {
                UnloadPrefabs();
                UnloadScenes();
                CleanUpBorder();
                CleanUpCellPrefab();
            }
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
        EditorGUILayout.Separator();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Load All"))
        {
            LoadScenes();
            LoadPrefabs();
        }
        if (GUILayout.Button("Load Scenes"))
        {
            LoadScenes();
        }
        if (GUILayout.Button("UnLoad Scenes"))
        {
            UnloadScenes();
        }
        if (GUILayout.Button("Load Prefabs"))
        {
            LoadPrefabs();
        }
        if (GUILayout.Button("UnLoad Prefabs"))
        {
            UnloadPrefabs();
        }
        if (GUILayout.Button("unLoad All"))
        {
            UnloadPrefabs();
            UnloadScenes();
        }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Set ScaleInLightmap"))
        {
            SetScaleInLightmap();
        }
        if (GUILayout.Button("Bake Lightmap"))
        {
            BakeLightmap();
        }
        if (GUILayout.Button("Export Data"))
        {
            ExportData(m_scenePrefab, GetDimension());
        }
        if (GUILayout.Button("Export lightmap Data"))
        {
            ExportLightmapData();
        }
        if (GUILayout.Button("Export single lightmap Data"))
        {
            ExportSingleLightmapData(m_instanceScenePrefab);
        }
        if (GUILayout.Button("Export lightmap Index Data"))
        {
            ExportLightmapIndexData(m_scenePrefab);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private DateTime m_lightmapBakeTimer;
    private void BakeLightmap()
    {
        m_lightmapBakeTimer = DateTime.Now;
        Lightmapping.BakeAsync();
        Lightmapping.completed = OnLightmappingBakeComplated;
    }

    private void OnLightmappingBakeComplated()
    {
        if (Lightmapping.giWorkflowMode == Lightmapping.GIWorkflowMode.OnDemand)
        {
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.Iterative;//打开自动烘焙，在烘焙完成后自动关闭，以解决5.4.0f3烘焙bug
        }
        else
        {
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
            var ts = DateTime.Now - m_lightmapBakeTimer;

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            LoadScenes();
            LoadPrefabs();
            foreach (var item in m_cellSceneList)
            {
                EditorSceneManager.SaveScene(item);
            }

            Debug.Log("lightmap baking time: " + ts);
            //刷新一下Scenes,保证导出光照索引没问题
            UnloadPrefabs();
            UnloadScenes();
            LoadScenes();
            LoadPrefabs();
        }
    }

    private int GetDimension()
    {
        return (int)Math.Pow(2, m_selected + 1);
    }

    //Check for any errors with User Input
    private bool CheckForErrors()
    {
        if (CtrlSliceTerrain.resolutionPerPatch < 8)
        {
            this.ShowNotification(new GUIContent("Resolution Per Patch must be 8 or greater"));
            GUIUtility.keyboardControl = 0; // Added to shift focus to original window rather than the notification
            return false;
        }
        else if (!Mathf.IsPowerOfTwo(CtrlSliceTerrain.resolutionPerPatch))
        {
            this.ShowNotification(new GUIContent("Resolution Per Patch must be a power of 2"));
            GUIUtility.keyboardControl = 0;
            return false;
        }
        else if (m_ctrl.newHeightMapResolution < 33)
        {
            this.ShowNotification(new GUIContent("Error with Heightmap Resolution - See Console for More Information"));
            GUIUtility.keyboardControl = 0;
            Debug.Log("The Heightmap Resolution for the new terrains must be 33 or larger. Currently it is " + m_ctrl.newHeightMapResolution.ToString() + ".\nThe new Heightmap Resolution is calculated as"
            + "follows: New Resolution = ((Old Resolution - 1) / New Dimension Width) + 1 -- For example, a 4x4 grid has a New Dimension Width of 4.\n You can rectify this problem by"
            + "either increasing the heightmap resolution of the base terrain, or reducing the number of new terrains to be created.");
            return false;
        }
        else if (m_ctrl.newAlphaMapResolution < 16)
        {
            this.ShowNotification(new GUIContent("Error with AlphaMap Resolution - See Console for More Information"));
            GUIUtility.keyboardControl = 0;
            Debug.Log("The Alpha Map Resolution of the new terrains is too small. Value must be 16 or greater. Current value is " + m_ctrl.newAlphaMapResolution.ToString()
            + ".\nPlease increase the Base Terrains alpha map resolution or reduce the number of new terrains to be created.");
            return false;
        }
        else if (m_ctrl.newBaseMapResolution < 16)
        {
            this.ShowNotification(new GUIContent("Error with BaseMap Resolution - See Console for More Information"));
            GUIUtility.keyboardControl = 0;
            Debug.Log("The Base Map Resolution of the new terrains is too small. Value must be 16 or greater. Current value is " + m_ctrl.newBaseMapResolution.ToString()
            + ".\nPlease increase the Base Terrains base map resolution or reduce the number of new terrains to be created.");
            return false;
        }
        else if (m_ctrl.baseData.detailResolution % m_ctrl.size != 0)
        {
            this.ShowNotification(new GUIContent("Error with Detail Resolution - See Console for More Information"));
            GUIUtility.keyboardControl = 0;
            Debug.Log("The Base Terrains detail resolution does not divide perfectly. Please change the detail resolution or number of terrains to be created to rectify this issue.");
            return false;
        }
        else if (!overwrite && AssetDatabase.LoadAssetAtPath(m_ctrl.fileName + "/" + CtrlSliceTerrain.baseTerrain.name + "_Data_" + 1 + "_" + 1 + ".asset", typeof(TerrainData)) != null)
        {

            this.ShowNotification(new GUIContent("Terrain Data with this name already exist. Please check 'Overwrite' if you wish to overwrite the existing Data"));
            GUIUtility.keyboardControl = 0;
            return false;
        }
        else
            return true;
    }

    private void LoadPrefabs()
    {
        if (m_cellSceneList.Count == 0)
        {
            LoadScenes();
        }
        else
        {
            foreach (var scene in m_cellSceneList)
            {
                var gos = scene.GetRootGameObjects();
                if (gos.Length > 0)
                {
                    if (!m_cellPrefabList.Contains(gos[0]))
                        m_cellPrefabList.Add(gos[0]);
                }
                else
                {
                    EditorSceneManager.SetActiveScene(scene);
                    var prefabPath = string.Concat(m_filePath, "/", scene.name, ".prefab");
                    var go = GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                    go.name = scene.name;
                    if (!m_cellPrefabList.Contains(go))
                        m_cellPrefabList.Add(go);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }
        Debug.Log("Loaded Prefabs: " + m_cellPrefabList.Count);
    }

    private void LoadScenes()
    {
        var files = Directory.GetFiles(m_filePath, "*.unity");
        foreach (var item in files)
        {
            var path = FullPath2AssetPath(item);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var gos = scene.GetRootGameObjects();
            if (gos.Length > 0)
            {
                if (!m_cellPrefabList.Contains(gos[0]))
                    m_cellPrefabList.Add(gos[0]);
            }
            if (!m_cellSceneList.Contains(scene))
                m_cellSceneList.Add(scene);
        }
        Debug.Log("Loaded Scenes: " + m_cellSceneList.Count);
    }

    private void SetScaleInLightmap()
    {
        for (int i = 0; i < m_cellPrefabList.Count; i++)
        {
            var terr = m_cellPrefabList[i].GetComponentInChildren<Terrain>();
            SerializedObject so = new SerializedObject(terr);
            so.FindProperty("m_ScaleInLightmap").floatValue = m_scaleInLightmap;
            so.ApplyModifiedProperties();
            Debug.Log(terr + ": " + so.FindProperty("m_ScaleInLightmap").floatValue);
        }
    }

    private void SliceMap(GameObject scenePrefab, int dimension)
    {
        Debug.Log("dimension " + dimension);
        if (m_instanceScenePrefab)
            m_instanceScenePrefab.SetActive(true);
        Terrain sceneTerrain = scenePrefab.GetComponentInChildren<Terrain>();

        if (sceneTerrain != null)
        {
            CtrlSliceTerrain.baseTerrain = sceneTerrain;
            m_ctrl.fileName = m_filePath;
            CtrlSliceTerrain.enumValue = (Size)dimension;
            m_ctrl.StoreData();
            m_ctrl.createPressed = true;
            if (CheckForErrors())
            {
                SlicePrefab(scenePrefab, dimension, sceneTerrain);
                ExportData(scenePrefab, dimension);
                //ExportLightmapData();
            }
            else
                m_ctrl.createPressed = false;
        }
        else
        {
            Debug.Log("no Terrain");
        }
    }

    private void SlicePrefab(GameObject scenePrefab, int dimension, Terrain sceneTerrain)
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

        if (!Directory.Exists(m_filePath))
            Directory.CreateDirectory(m_filePath);

        m_ctrl.CreateTerrainData();
        m_ctrl.CopyTerrainData();
        m_ctrl.SetNeighbors();

        //初始化临时容器
        for (int y = 0; y < dimension; y++)
        {
            for (int x = 0; x < dimension; x++)
            {
                cellPos[y, x] = new Vector2(startPoint.x + x * cellX, startPoint.z + y * cellZ);
                cellEndPos[y, x] = new Vector2(startPoint.x + (x + 1) * cellX, startPoint.z + (y + 1) * cellZ);
                var go = new GameObject();
                go.name = string.Format("{0}_{1}_{2}", scenePrefab.name, y + 1, x + 1);
                cellPrefab[y, x] = go;
                m_cellPrefabList.Add(go);
                cellNodeDic[y, x] = new Dictionary<Transform, Transform>();
                cellNodeDic[y, x][scenePrefab.transform] = go.transform;
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
            for (int y = 0; y < dimension; y++)
            {
                if (hasHandle)
                    break;
                for (int x = 0; x < dimension; x++)
                {
                    if (isConstructorNode)//结构节点每个单元格都加上去
                    {
                        var node = GameObject.Instantiate(child);
                        CleanUpNewTran(node, rubbishNode.transform);
                        node.name = child.name;
                        node.transform.parent = FindCellPrefabParentByTargetTran(child, cellPrefab[y, x].transform, cellNodeDic[y, x]);
                        CopyTranInfo(child, node);
                        cellNodeDic[y, x][child] = node;
                    }
                    else
                    {//显示节点需要按照坐标分配
                        var childX = child.position.x;
                        var childY = child.position.z;
                        if (cellPos[y, x].x < childX && childX < cellEndPos[y, x].x && cellPos[y, x].y < childY && childY < cellEndPos[y, x].y)
                        {
                            var node = GameObject.Instantiate(child);
                            CleanUpNewTran(node, rubbishNode.transform);
                            node.name = child.name;
                            node.transform.parent = FindCellPrefabParentByTargetTran(child, cellPrefab[y, x].transform, cellNodeDic[y, x]);
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
            m_ctrl.terrainGameObjects[i].transform.parent = item.transform;
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
            m_cellSceneList.Add(scene);
            var go = GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            go.name = item.name;
            EditorSceneManager.SaveScene(scene, string.Concat(m_filePath, "/", item.name, ".unity"));
        }

        //删除原来的prefab实例
        for (int i = 0; i < m_cellPrefabList.Count; i++)
        {
            GameObject.DestroyImmediate(m_cellPrefabList[i]);
        }
        EditorSceneManager.SetActiveScene(EditorSceneManager.GetSceneByName(scenePrefab.name));
        var sp = FindGameObject(scenePrefab.name);
        sp.SetActive(false);
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
        UnloadPrefabs();
        UnloadScenes();
        if (Directory.Exists(m_filePath))
        {
            Directory.Delete(m_filePath, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }
    }

    private void UnloadPrefabs()
    {
        foreach (var item in m_cellPrefabList)
        {
            if (item)
            {
                GameObject.DestroyImmediate(item);
            }
        }
        m_cellPrefabList.Clear();
        foreach (var item in m_cellSceneList)
        {
            EditorSceneManager.MarkSceneDirty(item);
        }
    }

    private void UnloadScenes()
    {
        foreach (var item in m_cellSceneList)
        {
            EditorSceneManager.CloseScene(item, true);
        }
        m_cellSceneList.Clear();
    }

    private void ExportData(GameObject scenePrefab, int dimension)
    {
        var list = new List<ZoneData>();
        var zoneSize = new ZoneData();
        var terr = scenePrefab.GetComponentInChildren<Terrain>();
        var sceneSize = terr.terrainData.size;
        var index = 0;
        zoneSize.id = index++;
        zoneSize.X = dimension;
        zoneSize.Y = dimension;
        zoneSize.Width = sceneSize.x / dimension;
        zoneSize.Height = sceneSize.z / dimension;
        list.Add(zoneSize);
        for (int y = 0; y < dimension; y++)
        {
            for (int x = 0; x < dimension; x++)
            {
                var zoneData = new ZoneData();
                var name = string.Format("{0}_{1}_{2}", scenePrefab.name, y + 1, x + 1);
                zoneData.id = index++;
                zoneData.X = x;
                zoneData.Y = y;
                zoneData.PrefabName = name + ".prefab";
                list.Add(zoneData);
            }
        }

        string fileName = string.Concat(DATA_DYNAMIC_MAP, scenePrefab.name, ConstString.XML_SUFFIX);
        SaveXMLList(fileName, list);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log(fileName + ": " + list.Count);
    }

    private void ExportLightmapIndexData(GameObject scenePrefab)
    {
        Debug.Log(LightmapSettings.lightProbes.name);
        Debug.Log(LightmapSettings.lightmapsMode);
        var index = 0;
        var lightmaps = new List<LightmapIndexData>();
        for (int i = 0; i < LightmapSettings.lightmaps.Length; i++)
        {
            var lightmapSetting = LightmapSettings.lightmaps[i];
            var directionality = AssetDatabase.GetAssetPath(lightmapSetting.lightmapFar).ReplaceFirst("Assets/Resources/", "");
            if (string.IsNullOrEmpty(directionality))
                continue;
            var lightmap = new LightmapIndexData();
            lightmap.id = index++;
            lightmap.Index = i;
            lightmap.Directionality = directionality;
            lightmap.Intensity = AssetDatabase.GetAssetPath(lightmapSetting.lightmapNear).ReplaceFirst("Assets/Resources/", "");
            Debug.Log(lightmap.Intensity);
            Debug.Log(lightmap.Directionality);
            lightmaps.Add(lightmap);
        }

        string fileName = string.Concat(DATA_DYNAMIC_MAP, "lightmapindex_", scenePrefab.name, ConstString.XML_SUFFIX);
        SaveXMLList(fileName, lightmaps);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log(fileName + ": " + lightmaps.Count);
    }

    private void ExportSingleLightmapData(GameObject scenePrefab)
    {
        m_cellPrefabList.Clear();
        m_cellPrefabList.Add(scenePrefab);
        ExportLightmapData();
        m_cellPrefabList.Clear();
    }

    private void ExportLightmapData()
    {
        foreach (var item in m_cellPrefabList)
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            var terr = item.GetComponentInChildren<Terrain>();
            var lightmapAssetDatas = new List<LightmapAssetData>();
            var index = 0;

            foreach (var render in renderers)
            {
                var lightmapAssetData = new LightmapAssetData();
                lightmapAssetData.id = index++;
                lightmapAssetData.name = render.name + render.transform.position;
                lightmapAssetData.Index = render.lightmapIndex == -1 ? 0 : render.lightmapIndex;
                lightmapAssetData.x = render.lightmapScaleOffset.x;
                lightmapAssetData.y = render.lightmapScaleOffset.y;
                lightmapAssetData.z = render.lightmapScaleOffset.z;
                lightmapAssetData.w = render.lightmapScaleOffset.w;
                lightmapAssetDatas.Add(lightmapAssetData);
            }
            var terrLightmapAssetData = new LightmapAssetData();
            terrLightmapAssetData.id = index++;
            terrLightmapAssetData.name = terr.name;
            terrLightmapAssetData.Index = terr.lightmapIndex == -1 ? 0 : terr.lightmapIndex;
            terrLightmapAssetData.x = terr.lightmapScaleOffset.x;
            terrLightmapAssetData.y = terr.lightmapScaleOffset.y;
            terrLightmapAssetData.z = terr.lightmapScaleOffset.z;
            terrLightmapAssetData.w = terr.lightmapScaleOffset.w;
            lightmapAssetDatas.Add(terrLightmapAssetData);

            string fileName = string.Concat(DATA_DYNAMIC_MAP, "lightmapdata_", item.name, ConstString.XML_SUFFIX);
            SaveXMLList(fileName, lightmapAssetDatas);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log(fileName + ": " + lightmapAssetDatas.Count);
        }
    }

    private GameObject FindGameObject(string name)
    {
        var gos = EditorSceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < gos.Length; i++)//GameObject.Find找不到inactive的GameObject
        {
            if (gos[i].name == name)
            {
                return gos[i];
            }
        }
        return null;
    }

    private List<T> LoadXML<T>(string path)
    {
        var text = path.LoadFile();
        return LoadXMLText<T>(text);
    }

    private List<T> LoadXMLText<T>(string text)
    {
        List<T> list = new List<T>();
        try
        {
            if (String.IsNullOrEmpty(text))
            {
                return list;
            }
            Type type = typeof(T);
            var xml = XMLParser.LoadXML(text);
            Dictionary<Int32, Dictionary<String, String>> map = XMLParser.LoadIntMap(xml, text);
            var props = type.GetProperties(~System.Reflection.BindingFlags.Static);
            foreach (var item in map)
            {
                var obj = type.GetConstructor(Type.EmptyTypes).Invoke(null);
                foreach (var prop in props)
                {
                    if (prop.Name == "id")
                        prop.SetValue(obj, item.Key, null);
                    else
                        try
                        {
                            if (item.Value.ContainsKey(prop.Name))
                            {
                                var value = CommonUtils.GetValue(item.Value[prop.Name], prop.PropertyType);
                                prop.SetValue(obj, value, null);
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggerHelper.Debug("LoadXML error: " + item.Value[prop.Name] + " " + prop.PropertyType);
                            LoggerHelper.Except(ex);
                        }
                }
                list.Add((T)obj);
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Except(ex);
            LoggerHelper.Error("error text: \n" + text);
        }
        return list;
    }

    private void SaveXMLList<T>(string path, List<T> data, string attrName = "record")
    {
        var root = new SecurityElement("root");
        var i = 0;
        var props = typeof(T).GetProperties();
        foreach (var item in data)
        {
            var xml = new SecurityElement(attrName);
            foreach (var prop in props)
            {
                var type = prop.PropertyType;
                String result = String.Empty;
                object obj = prop.GetGetMethod().Invoke(item, null);
                if (obj == null)
                    continue;
                //var obj = prop.GetValue(item, null);
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    result = typeof(CommonUtils).GetMethod("PackMap")
                    .MakeGenericMethod(type.GetGenericArguments())
                    .Invoke(null, new object[] { obj, ':', ',' }).ToString();
                }
                else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    result = typeof(CommonUtils).GetMethod("PackList")
                    .MakeGenericMethod(type.GetGenericArguments())
                    .Invoke(null, new object[] { obj, ',' }).ToString();
                }
                else
                {
                    result = obj.ToString();
                }
                xml.AddChild(new SecurityElement(prop.Name, result));
            }
            root.AddChild(xml);
            i++;
        }
        XMLParser.SaveText(path, root.ToString());
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

    private string FullPath2AssetPath(string fullPath)
    {
        return fullPath.Replace("\\", "/").Replace(Application.dataPath, "Assets");
    }
}