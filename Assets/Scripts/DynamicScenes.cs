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

    int mapSectionWidth, mapSectionHeight, mapSectionXNum, mapSectionYNum;

    void Start()
    {
        mapSectionWidth = 500;
        mapSectionHeight = 500;
        mapSectionXNum = 8;
        mapSectionYNum = 8;
        m_sceneData = new SceneData() { Id = 1, Width = 4000, Height = 4000 };
        m_sceneData.Cells = new SceneCellData[mapSectionXNum, mapSectionYNum];
        for (int i = 0; i < mapSectionXNum; i++)
        {
            for (int j = 0; j < mapSectionYNum; j++)
            {
                var cell = new SceneCellData();
                cell.X = j * 500;
                cell.Y = i * 500;
                cell.PrefabName = string.Format("Demo01_{0}_{1}", i + 1, j + 1);
                cell.SceneName = string.Format("Demo01_{0}_{1}", i + 1, j + 1);
                m_sceneData.Cells[i, j] = cell;
            }
        }
    }

    float counter;
    void Update()
    {
        counter += Time.deltaTime;
        if (counter > 1)
        {
            counter = 0;
            UpdateAvatarSection();
        }
    }

    float currentStartX, currentStartY, currentEndX, currentEndY;

    void UpdateAvatarSection()
    {
        var pos = m_avatar.position;
        if (currentStartX < pos.x && pos.x < currentEndX && currentStartY < pos.z && pos.z < currentEndY)
        {

        }
        else
        {
            leaderSectionX = (int)Math.Floor(pos.x / mapSectionWidth);
            leaderSectionY = (int)Math.Floor(pos.z / mapSectionHeight);
            currentStartX = leaderSectionX * mapSectionWidth;
            currentStartY = leaderSectionY * mapSectionHeight;
            currentEndX = currentStartX + mapSectionWidth;
            currentEndY = currentStartY + mapSectionHeight;
            ChangeMapSection();

            Debug.Log(leaderSectionX + " " + leaderSectionY);
        }
    }

    int _leaderSectionX;
    /// <summary>
    /// 主角所处的切片X值
    /// </summary>
    public int leaderSectionX
    {
        get { return _leaderSectionX; }
        set { _leaderSectionX = value; }
    }
    int _leaderSectionY;
    /// <summary>
    /// 主角所处的切片Y值
    /// </summary>
    public int leaderSectionY
    {
        get { return _leaderSectionY; }
        set { _leaderSectionY = value; }
    }

    //Image mapSection;
    int startSectionX, startSectionY, endSectionX, endSectionY;

    private void ChangeMapSection()
    {
        if (leaderSectionX == 0)
        {
            startSectionX = 0;
            endSectionX = 2;
        }
        else if (leaderSectionX == mapSectionXNum - 1)
        {
            startSectionX = leaderSectionX - 2;
            endSectionX = leaderSectionX;
        }
        else
        {
            startSectionX = leaderSectionX - 1;
            endSectionX = leaderSectionX + 1;
        }

        if (leaderSectionY == 0)
        {
            startSectionY = 0;
            endSectionY = 2;
        }
        else if (leaderSectionY == mapSectionYNum - 1)
        {
            startSectionY = leaderSectionY - 2;
            endSectionY = leaderSectionY;
        }
        else
        {
            startSectionY = leaderSectionY - 1;
            endSectionY = leaderSectionY + 1;
        }

        var sb = new StringBuilder();
        waitForRemove.Clear();
        waitForRemove.AddRange(loadedSceneCell);
        loadedSceneCell.Clear();
        for (int x = startSectionX; x <= endSectionX; x++)
        {
            for (int y = startSectionY; y <= endSectionY; y++)
            {
                sb.AppendFormat("{0},{1}; ", x, y);
                var cell = m_sceneData.Cells[y, x];
                waitForRemove.Remove(cell);
                loadedSceneCell.Add(cell);
            }
        }
        UpdateSceneModel();
        Debug.Log(sb.ToString());
        Debug.Log("waitForRemove: " + waitForRemove.Count);
    }

    void UpdateSceneModel()
    {
        for (int i = 0; i < waitForRemove.Count; i++)
        {
            UnloadScene(waitForRemove[i]);
        }
        for (int i = 0; i < loadedSceneCell.Count; i++)
        {
            StartCoroutine(LoadScene(loadedSceneCell[i]));
        }
    }

    List<SceneCellData> loadedSceneCell = new List<SceneCellData>();
    List<SceneCellData> waitForRemove = new List<SceneCellData>();

    private IEnumerator LoadScene(SceneCellData scene)
    {
        if (!scene.Loaded)
        {
            yield return SceneManager.LoadSceneAsync(scene.SceneName, LoadSceneMode.Additive);

            var async = Resources.LoadAsync<GameObject>("Demo01/Demo01/" + scene.PrefabName);
            yield return async;
            scene.Prefab = GameObject.Instantiate(async.asset) as GameObject;
            scene.Loaded = true;
        }
    }

    private void UnloadScene(SceneCellData scene)
    {
        if (scene.Loaded)
        {
            GameObject.Destroy(scene.Prefab);
            SceneManager.UnloadScene(scene.SceneName);
            scene.Loaded = false;
        }
    }
}

public class SceneData
{
    public int Id { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public SceneCellData[,] Cells;
}

public class SceneCellData
{
    public int X { get; set; }
    public int Y { get; set; }
    public string PrefabName { get; set; }
    public string SceneName { get; set; }
    public bool Loaded { get; set; }
    public GameObject Prefab { get; set; }
    public Scene Scene { get; set; }
}