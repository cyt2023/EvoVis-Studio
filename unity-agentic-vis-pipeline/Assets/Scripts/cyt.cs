using UnityEngine;
using IATK;
using System.Linq;

public class cyt : MonoBehaviour
{
    public TextAsset dataSource;

    private View view;
    private float[] px, py, pz;
    private float[] timeNorm;

    private int N = 2000;
    private float speed = 0.2f;
    private float t = 0f;

    void Start()
    {
        if (dataSource == null)
        {
            Debug.LogError("No CSV assigned!");
            return;
        }

        CSVDataSource csv = GetComponent<CSVDataSource>();
        if (csv == null)
            csv = gameObject.AddComponent<CSVDataSource>();

        csv.load(dataSource.text, null);

        // 数据
        var lon = csv["pickup_longitude"].Data;
        var lat = csv["pickup_latitude"].Data;
        var fare = csv["fare_amount"].Data;
        var time = csv["pickup_datetime"].Data;

        int count = Mathf.Min(N, lon.Length);

        lon = lon.Take(count).ToArray();
        lat = lat.Take(count).ToArray();
        fare = fare.Take(count).ToArray();
        time = time.Take(count).ToArray();

        // 空间归一化
        float minLon = lon.Min();
        float maxLon = lon.Max();
        float minLat = lat.Min();
        float maxLat = lat.Max();

        px = lon.Select(v => (v - minLon) / (maxLon - minLon)).ToArray();
        pz = lat.Select(v => (v - minLat) / (maxLat - minLat)).ToArray();

        // 🔥 高度（核心升级）
        float minFare = fare.Min();
        float maxFare = fare.Max();

        py = fare.Select(v => (v - minFare) / (maxFare - minFare)).ToArray();

        // 放大高度（否则太扁）
        py = py.Select(v => v * 2.0f).ToArray();   // 👈 可以调 1~5

        // 时间
        float minT = time.Min();
        float maxT = time.Max();
        timeNorm = time.Select(v => (v - minT) / (maxT - minT)).ToArray();

        // 颜色 = 高度
        Color[] colors = py.Select(v => Color.Lerp(Color.blue, Color.red, v)).ToArray();

        // 构建3D点
        ViewBuilder vb = new ViewBuilder(MeshTopology.Points, "Taxi3D")
            .initialiseDataView(count)
            .setDataDimension(px, ViewBuilder.VIEW_DIMENSION.X)
            .setDataDimension(py, ViewBuilder.VIEW_DIMENSION.Y)   // 👈 新增！
            .setDataDimension(pz, ViewBuilder.VIEW_DIMENSION.Z)
            .setColors(colors);

        vb = vb.createIndicesPointTopology();

        Material mt = new Material(Shader.Find("Unlit/Color"));

        view = vb.updateView().apply(gameObject, mt);
        view.SetSize(0.12f);

        Debug.Log("3D Taxi terrain created!");
    }

    void Update()
    {
        if (view == null) return;

        t += Time.deltaTime * speed;
        if (t > 1f) t = 0f;

        Color[] colors = new Color[timeNorm.Length];

        for (int i = 0; i < timeNorm.Length; i++)
        {
            float diff = Mathf.Abs(timeNorm[i] - t);

            if (diff < 0.1f)
            {
                colors[i] = Color.yellow;  // 当前时间高亮
            }
            else
            {
                colors[i] = Color.Lerp(Color.blue, Color.red, py[i]); // 高度颜色
            }
        }

        view.SetColors(colors);
    }
}