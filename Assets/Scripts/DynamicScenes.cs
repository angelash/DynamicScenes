using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine.SceneManagement;
using GameLoader.Utils;
using GameLoader.Utils.XML;

public class DynamicScenes : MonoBehaviour
{
    private const string DATA_DYNAMIC_MAP = "Assets/Resources/data/dynamic_maps/";

    private string m_demoName = "Demo02";

    private SceneData m_sceneData;
    public Transform m_avatar;

    private float m_zoneWidth, m_zoneHeight;
    private int mapSectionXNum, mapSectionYNum;

    private List<ZoneObjData> m_loadedZones = new List<ZoneObjData>();
    private List<ZoneObjData> m_waitForRemove = new List<ZoneObjData>();

    private int m_loadCounter;
    private float m_updateCounter;
    private int m_loadRange = 2;
    private int m_centerZoneX, m_centerZoneY;
    private int m_startZoneX, m_startZoneY, m_endZoneX, m_endZoneY;
    private float currentStartX, currentStartY, currentEndX, currentEndY;

    /// <summary>
    /// 主角所处的切片X值
    /// </summary>
    public int CenterZoneX
    {
        get { return m_centerZoneX; }
        set { m_centerZoneX = value; }
    }
    /// <summary>
    /// 主角所处的切片Y值
    /// </summary>
    public int CenterZoneY
    {
        get { return m_centerZoneY; }
        set { m_centerZoneY = value; }
    }

    void Start()
    {
        var zoneSize = 62.5f;
        var mapSize = 500;
        m_zoneWidth = zoneSize;
        m_zoneHeight = zoneSize;
        mapSectionXNum = 8;
        mapSectionYNum = 8;
        m_sceneData = new SceneData() { Id = 1, Width = mapSize, Height = mapSize };
        m_sceneData.Cells = new ZoneObjData[mapSectionYNum, mapSectionXNum];
        for (int y = 0; y < mapSectionYNum; y++)
        {
            for (int x = 0; x < mapSectionXNum; x++)
            {
                var cell = new ZoneObjData();
                cell.X = x;
                cell.Y = y;
                cell.PrefabName = string.Format("{2}_{0}_{1}", y + 1, x + 1, m_demoName);
                //cell.SceneName = string.Format("{2}_{0}_{1}", y + 1, x + 1, m_demoName);
                string fileName = string.Concat(DATA_DYNAMIC_MAP, "lightmapdata_", cell.PrefabName, ConstString.XML_SUFFIX);
                cell.LightmapAssetDatas = LoadXML<LightmapAssetData>(fileName);
                m_sceneData.Cells[y, x] = cell;
            }
        }
    }

    void Update()
    {
        m_updateCounter += Time.deltaTime;
        if (m_updateCounter > 1)
        {
            m_updateCounter = 0;
            UpdateAvatarZone();
        }
    }

    private void UpdateAvatarZone()
    {
        var pos = m_avatar.position;
        if (currentStartX < pos.x && pos.x < currentEndX && currentStartY < pos.z && pos.z < currentEndY)
        {

        }
        else
        {
            CenterZoneX = (int)Math.Floor(pos.x / m_zoneWidth);
            CenterZoneY = (int)Math.Floor(pos.z / m_zoneHeight);
            currentStartX = CenterZoneX * m_zoneWidth;
            currentStartY = CenterZoneY * m_zoneHeight;
            currentEndX = currentStartX + m_zoneWidth;
            currentEndY = currentStartY + m_zoneHeight;
            ChangeMapZones();

            Debug.Log(CenterZoneX + " " + CenterZoneY);
        }
    }

    private void ChangeMapZones()
    {
        m_startZoneX = Mathf.Max(CenterZoneX - m_loadRange, 0);
        m_endZoneX = Mathf.Min(CenterZoneX + m_loadRange, mapSectionXNum - 1);
        m_startZoneY = Mathf.Max(CenterZoneY - m_loadRange, 0);
        m_endZoneY = Mathf.Min(CenterZoneY + m_loadRange, mapSectionYNum - 1);

        var sb = new StringBuilder();
        m_waitForRemove.Clear();
        m_waitForRemove.AddRange(m_loadedZones);
        m_loadedZones.Clear();
        for (int y = m_startZoneY; y <= m_endZoneY; y++)
        {
            for (int x = m_startZoneX; x <= m_endZoneX; x++)
            {
                sb.AppendFormat("{0},{1}; ", y, x);
                var cell = m_sceneData.Cells[y, x];
                m_waitForRemove.Remove(cell);
                m_loadedZones.Add(cell);
            }
        }
        UpdateSceneModel();
        Debug.Log(sb.ToString());
        Debug.Log("waitForRemove: " + m_waitForRemove.Count);
    }

    private void UpdateSceneModel()
    {
        for (int i = 0; i < m_waitForRemove.Count; i++)
        {
            UnloadScene(m_waitForRemove[i]);
        }
        m_loadCounter = 0;
        for (int i = 0; i < m_loadedZones.Count; i++)
        {
            StartCoroutine(LoadZone(m_loadedZones[i], m_loadedZones.Count));
        }
    }

    private IEnumerator LoadZone(ZoneObjData zone, int total)
    {
        if (!zone.Loaded)
        {
            //yield return SceneManager.LoadSceneAsync(zone.SceneName, LoadSceneMode.Additive);

            var async = Resources.LoadAsync<GameObject>(m_demoName + "/" + m_demoName + "/" + zone.PrefabName);
            yield return async;
            zone.Prefab = GameObject.Instantiate(async.asset) as GameObject;
            var x = zone.X + 1;
            var y = zone.Y + 1;
            //Debug.Log("Terrain" + (zone.Y / 500 + 1) + "_" + (zone.X / 500 + 1));
            var terr = zone.Prefab.transform.FindChild("Terrain" + y + "_" + x);
            zone.Terrain = terr.GetComponent<Terrain>();
            zone.Loaded = true;
        }
        m_loadCounter++;
        if (m_loadCounter >= total)
        {
            for (int i = 0; i < m_loadedZones.Count; i++)
            {
                var loadedZone = m_loadedZones[i];
                var x = loadedZone.X;
                var y = loadedZone.Y;

                Terrain left = GetLoadedTerrain(y, x - 1);
                Terrain right = GetLoadedTerrain(y, x + 1);
                Terrain top = GetLoadedTerrain(y + 1, x);
                Terrain bottom = GetLoadedTerrain(y - 1, x);

                loadedZone.Terrain.SetNeighbors(left, top, right, bottom);
            }
        }
    }

    private void UnloadScene(ZoneObjData scene)
    {
        if (scene.Loaded)
        {
            GameObject.Destroy(scene.Prefab);
            //SceneManager.UnloadScene(scene.SceneName);
            scene.Loaded = false;
        }
    }

    private Terrain GetLoadedTerrain(int y, int x)
    {
        if ((x >= 0 && x < mapSectionXNum) && (y >= 0 && y < mapSectionYNum))
        {
            var zone = m_sceneData.Cells[y, x];
            if (zone.Loaded)
                return zone.Terrain;
        }

        return null;
    }


    private static List<T> LoadXML<T>(string path)
    {
        var text = path.LoadFile();
        return LoadXMLText<T>(text);
    }

    private static List<T> LoadXMLText<T>(string text)
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

}

public class SceneData
{
    public int Id { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    /// <summary>
    /// [y,x]: y: Height; x: Width
    /// </summary>
    public ZoneObjData[,] Cells;
}

public class ZoneObjData
{
    public int X { get; set; }
    public int Y { get; set; }
    public string PrefabName { get; set; }
    //public string SceneName { get; set; }
    public bool Loaded { get; set; }
    public GameObject Prefab { get; set; }
    public List<LightmapAssetData> LightmapAssetDatas { get; set; }
    //public Scene Scene { get; set; }
    public Terrain Terrain { get; set; }
}