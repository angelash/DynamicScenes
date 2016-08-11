using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine.SceneManagement;

public class DynamicScenes : MonoBehaviour
{
    private SceneData m_sceneData;
    public Transform m_avatar;

    private int mapCenterWidth, mapCenterHeight, mapSectionXNum, mapSectionYNum;

    private List<ZoneData> m_loadedZones = new List<ZoneData>();
    private List<ZoneData> m_waitForRemove = new List<ZoneData>();

    private int m_loadCounter;
    private float m_updateCounter;
    private int m_loadRange = 1;
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
        mapCenterWidth = 500;
        mapCenterHeight = 500;
        mapSectionXNum = 8;
        mapSectionYNum = 8;
        m_sceneData = new SceneData() { Id = 1, Width = 4000, Height = 4000 };
        m_sceneData.Cells = new ZoneData[mapSectionXNum, mapSectionYNum];
        for (int i = 0; i < mapSectionXNum; i++)
        {
            for (int j = 0; j < mapSectionYNum; j++)
            {
                var cell = new ZoneData();
                cell.X = j * 500;
                cell.Y = i * 500;
                cell.PrefabName = string.Format("Demo01_{0}_{1}", i + 1, j + 1);
                cell.SceneName = string.Format("Demo01_{0}_{1}", i + 1, j + 1);
                m_sceneData.Cells[i, j] = cell;
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
            CenterZoneX = (int)Math.Floor(pos.x / mapCenterWidth);
            CenterZoneY = (int)Math.Floor(pos.z / mapCenterHeight);
            currentStartX = CenterZoneX * mapCenterWidth;
            currentStartY = CenterZoneY * mapCenterHeight;
            currentEndX = currentStartX + mapCenterWidth;
            currentEndY = currentStartY + mapCenterHeight;
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
        for (int x = m_startZoneX; x <= m_endZoneX; x++)
        {
            for (int y = m_startZoneY; y <= m_endZoneY; y++)
            {
                sb.AppendFormat("{0},{1}; ", x, y);
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

    private IEnumerator LoadZone(ZoneData zone, int total)
    {
        if (!zone.Loaded)
        {
            yield return SceneManager.LoadSceneAsync(zone.SceneName, LoadSceneMode.Additive);

            var async = Resources.LoadAsync<GameObject>("Demo01/Demo01/" + zone.PrefabName);
            yield return async;
            zone.Prefab = GameObject.Instantiate(async.asset) as GameObject;
            var x = (zone.X / 500 + 1);
            var y = (zone.Y / 500 + 1);
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
                var x = (loadedZone.X / 500);
                var y = (loadedZone.Y / 500);

                Terrain left = GetLoadedTerrain(x - 1, y);
                Terrain right = GetLoadedTerrain(x + 1, y);
                Terrain top = GetLoadedTerrain(x, y + 1);
                Terrain bottom = GetLoadedTerrain(x, y - 1);

                loadedZone.Terrain.SetNeighbors(left, top, right, bottom);
            }
        }
    }

    private void UnloadScene(ZoneData scene)
    {
        if (scene.Loaded)
        {
            GameObject.Destroy(scene.Prefab);
            SceneManager.UnloadScene(scene.SceneName);
            scene.Loaded = false;
        }
    }

    private Terrain GetLoadedTerrain(int x, int y)
    {
        if ((x >= 0 && x < mapSectionXNum) && (y >= 0 && y < mapSectionYNum))
        {
            var zone = m_sceneData.Cells[y, x];
            if (zone.Loaded)
                return zone.Terrain;
        }

        return null;
    }
}

public class SceneData
{
    public int Id { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public ZoneData[,] Cells;
}

public class ZoneData
{
    public int X { get; set; }
    public int Y { get; set; }
    public string PrefabName { get; set; }
    public string SceneName { get; set; }
    public bool Loaded { get; set; }
    public GameObject Prefab { get; set; }
    public Scene Scene { get; set; }
    public Terrain Terrain { get; set; }
}