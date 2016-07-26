using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DynamicScenes : MonoBehaviour
{
    private SceneData m_sceneData;
    public Transform m_avatar;

    Canvas MapSurface;
    int mapSectionWidth, mapSectionHeight, mapSectionXNum, mapSectionYNum;

    void Start()
    {
        m_sceneData = new SceneData() { Id = 1 };
        m_sceneData.Cells = new List<SceneCellData>();

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                var cell = new SceneCellData();
                cell.X = j * 500;
                cell.Y = i * 500;
                cell.PrefabName = string.Format("Demo01_{0}_{1}.prefab", i + 1, j + 1);
                cell.UnityName = string.Format("Demo01_{0}_{1}.unity", i + 1, j + 1);
                m_sceneData.Cells.Add(cell);
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
            ChangeMapSection();
        }
    }

    int _leaderSectionX;
    /// <summary>
    /// 主角所处的切片X值
    /// </summary>
    public int leaderSectionX
    {
        get { return _leaderSectionX; }
        set { if (_leaderSectionX != value) { _leaderSectionX = value; ChangeMapSection(); } }
    }
    int _leaderSectionY;
    /// <summary>
    /// 主角所处的切片Y值
    /// </summary>
    public int leaderSectionY
    {
        get { return _leaderSectionY; }
        set { if (_leaderSectionY != value) { _leaderSectionY = value; ChangeMapSection(); } }
    }

    //Image mapSection;
    int startSectionX, startSectionY, endSectionX, endSectionY;
    /// <summary>
    /// 初始化地图地表层
    /// </summary>
    //private void InitMapSurface(XElement args)
    //{
    //    MapSurface = new Canvas()
    //    {
    //        Width = Convert.ToDouble(args.Attribute("Width").Value),
    //        Height = Convert.ToDouble(args.Attribute("Height").Value),
    //    };
    //    Add(MapSurface);
    //    mapSectionWidth = Convert.ToInt32(args.Attribute("SectionWidth").Value);
    //    mapSectionHeight = Convert.ToInt32(args.Attribute("SectionHeight").Value);
    //    mapSectionXNum = (int)(MapSurface.Width / mapSectionWidth);
    //    mapSectionYNum = (int)(MapSurface.Height / mapSectionHeight);
    //}
    private void ChangeMapSection()
    {
        //MapSurface.Children.Clear();
        if (leaderSectionX == 0)
        {
            startSectionX = 0; endSectionX = 2;
        }
        else if (leaderSectionX == mapSectionXNum - 1)
        {
            startSectionX = leaderSectionX - 2; endSectionX = leaderSectionX;
        }
        else
        {
            startSectionX = leaderSectionX - 1; endSectionX = leaderSectionX + 1;
        }
        if (leaderSectionY == 0)
        {
            startSectionY = 0; endSectionY = 2;
        }
        else if (leaderSectionY == mapSectionYNum - 1)
        {
            startSectionY = leaderSectionY - 2; endSectionY = leaderSectionY;
        }
        else
        {
            startSectionY = leaderSectionY - 1; endSectionY = leaderSectionY + 1;
        }
        for (int x = startSectionX; x <= endSectionX; x++)
        {
            for (int y = startSectionY; y <= endSectionY; y++)
            {
                //mapSection = new Image()
                //{
                //    Source = Super.getImage(string.Format("Map/{0}/Surface/{1}_{2}.jpg", mapCode.ToString(), x, y)),
                //    Width = mapSectionWidth,
                //    Height = mapSectionHeight,
                //    Stretch = Stretch.Fill,
                //};
                //MapSurface.Children.Add(mapSection);
                //Canvas.SetLeft(mapSection, x * mapSectionWidth);
                //Canvas.SetTop(mapSection, y * mapSectionHeight);
            }
        }
    }

    //public void checkCache(int x, int y)
    //{

    //    //      //////////////////
    //    //      1-4,1-3,1-2,1-1
    //    //      2-4,2-3,2-2,2-1 
    //    //      3-4,3-3,3-2,3-1
    //    //      4-4,4-3,4-2,4-1 
    //    //      ////////////////// 2,2
    //    cacheNewTemp.Clear();
    //    cacheNew.Clear();
    //    int leftx = x;
    //    int lefty = y + 1;
    //    int leftUpx = x - 1;
    //    int leftUpy = y + 1;
    //    int leftButtomx = x + 1;
    //    int leftButtomy = y + 1;
    //    int topx = x - 1;
    //    int topy = y;
    //    int buttomx = x + 1;
    //    int buttomy = y;
    //    int rightx = x;
    //    int righty = y - 1;
    //    int rightUpx = x - 1;
    //    int rightUpy = y - 1;
    //    int rightButtomx = x + 1;
    //    int rightButtomy = y - 1;
    //    if (cacheInit)
    //    {

    //        if (leftx >= 0 && lefty >= 0 && leftx < m_ZoneCount && lefty < m_ZoneCount)
    //        {
    //            getCahce(leftx, lefty);
    //        }
    //        if (leftUpx >= 0 && leftUpy >= 0 && leftUpx < m_ZoneCount && leftUpy < m_ZoneCount)
    //        {
    //            getCahce(leftUpx, leftUpy);
    //        }
    //        if (leftButtomx >= 0 && leftButtomy >= 0 && leftButtomx < m_ZoneCount && leftButtomy < m_ZoneCount)
    //        {
    //            getCahce(leftButtomx, leftButtomy);
    //        }
    //        if (topx >= 0 && topy >= 0 && topx < m_ZoneCount && topy < m_ZoneCount)
    //        {
    //            getCahce(topx, topy);
    //        }
    //        if (buttomx >= 0 && buttomy >= 0 && buttomx < m_ZoneCount && buttomy < m_ZoneCount)
    //        {
    //            getCahce(buttomx, buttomy);
    //        }
    //        if (rightx >= 0 && righty >= 0 && rightx < m_ZoneCount && righty < m_ZoneCount)
    //        {
    //            getCahce(rightx, righty);
    //        }
    //        if (rightUpx >= 0 && rightUpy >= 0 && rightUpx < m_ZoneCount && rightUpy < m_ZoneCount)
    //        {
    //            getCahce(rightUpx, rightUpy);
    //        }
    //        if (rightButtomx >= 0 && rightButtomy >= 0 && rightButtomx < m_ZoneCount && rightButtomy < m_ZoneCount)
    //        {
    //            getCahce(rightButtomx, rightButtomy);
    //        }
    //        string key = x + "," + y;
    //        cacheNewTemp.Add(key, key);

    //        getRemoveCahce();

    //    }
    //    else
    //    {
    //        if (leftx >= 0 && lefty >= 0 && leftx < m_ZoneCount && lefty < m_ZoneCount)
    //        {
    //            addCache(leftx, lefty);
    //        }
    //        if (leftUpx >= 0 && leftUpy >= 0 && leftUpx < m_ZoneCount && leftUpy < m_ZoneCount)
    //        {
    //            addCache(leftUpx, leftUpy);
    //        }
    //        if (leftButtomx >= 0 && leftButtomy >= 0 && leftButtomx < m_ZoneCount && leftButtomy < m_ZoneCount)
    //        {
    //            addCache(leftButtomx, leftButtomy);
    //        }
    //        if (topx >= 0 && topy >= 0 && topx < m_ZoneCount && topy < m_ZoneCount)
    //        {
    //            addCache(topx, topy);
    //        }
    //        if (buttomx >= 0 && buttomy >= 0 && buttomx < m_ZoneCount && buttomy < m_ZoneCount)
    //        {
    //            addCache(buttomx, buttomy);
    //        }
    //        if (rightx >= 0 && righty >= 0 && rightx < m_ZoneCount && righty < m_ZoneCount)
    //        {
    //            addCache(rightx, righty);
    //        }
    //        if (rightUpx >= 0 && rightUpy >= 0 && rightUpx < m_ZoneCount && rightUpy < m_ZoneCount)
    //        {
    //            addCache(rightUpx, rightUpy);
    //        }
    //        if (rightButtomx >= 0 && rightButtomy >= 0 && rightButtomx < m_ZoneCount && rightButtomy < m_ZoneCount)
    //        {
    //            addCache(rightButtomx, rightButtomy);
    //        }
    //        string key = x + "," + y;
    //        cache.Add(key, key);
    //        cacheInit = true;
    //    }

    //    getLoadNewCahce();
    //}

    //public void getRemoveCahce()
    //{
    //    Hashtable rtmp = new Hashtable();
    //    IDictionaryEnumerator enumerator = cache.GetEnumerator();
    //    while (enumerator.MoveNext())
    //    {
    //        Debug.Log("cache key = " + enumerator.Key.ToString());
    //        if (!cacheNewTemp.Contains(enumerator.Key.ToString()))
    //        {
    //            rtmp.Add(enumerator.Key.ToString(), enumerator.Key.ToString());
    //        }
    //    }
    //    IDictionaryEnumerator enumerator2 = rtmp.GetEnumerator();
    //    while (enumerator2.MoveNext())
    //    {
    //        Debug.Log("del cache key = " + enumerator2.Key.ToString());
    //        if (cache.Contains(enumerator2.Key.ToString()))
    //        {
    //            cache.Remove(enumerator2.Key.ToString());
    //            string[] s = enumerator2.Key.ToString().Split(',');
    //            StartCoroutine(UnloadZone(int.Parse(s[0]), int.Parse(s[1])));
    //        }
    //    }
    //    rtmp.Clear();
    //    rtmp = null;
    //}
    //public void getLoadNewCahce()
    //{
    //    IDictionaryEnumerator enumerator = cacheNew.GetEnumerator();
    //    while (enumerator.MoveNext())
    //    {
    //        string[] s = enumerator.Key.ToString().Split(',');
    //        StartCoroutine(LoadZone(int.Parse(s[0]), int.Parse(s[1])));
    //    }
    //}
    //public void getCahce(int x, int y)
    //{
    //    string key = x + "," + y;
    //    cacheNewTemp.Add(key, key);
    //    if (!cache.Contains(key))
    //    {
    //        cache.Add(key, key);
    //        cacheNew.Add(key, key);
    //    }
    //}
    //public void addCache(int x, int y)
    //{
    //    string key = x + "," + y;
    //    cache.Add(key, key);
    //    StartCoroutine(LoadZone(x, y));
    //}
    //public void removeCache(int x, int y)
    //{
    //    string key = x + "," + y;
    //    StartCoroutine(UnloadZone(x, y));
    //}
}

public class SceneData
{
    public int Id { get; set; }
    public List<SceneCellData> Cells = new List<SceneCellData>();
}

public class SceneCellData
{
    public int X { get; set; }
    public int Y { get; set; }
    public string PrefabName { get; set; }
    public string UnityName { get; set; }
}