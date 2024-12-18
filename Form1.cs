using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpMap.Layers;
using SharpMap.Data.Providers;
using SharpMap.CoordinateSystems;
using System.Xml;
using SharpMap.Forms;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using GeoAPI.CoordinateSystems;
using NetTopologySuite;
using System.IO;
using System.Net;
using System.Threading;
using System.Web;
using SharpMap;

namespace WindowsFormsApp4
{
    public partial class Form1 : Form
    {
        private SharpMap.Forms.MapBox mapBox;

        //服务器监听
        private HttpListener httpListener;
        private Thread listenerThread;


        public Form1()
        {
            InitializeComponent();
            InitializeMap();
            InitializeMenu();
            var gss = new NtsGeometryServices();
            var css = new SharpMap.CoordinateSystems.CoordinateSystemServices(
                new CoordinateSystemFactory(),
                new CoordinateTransformationFactory(),
                SharpMap.Converters.WellKnownText.SpatialReference.GetAllReferenceSystems());

            GeoAPI.GeometryServiceProvider.Instance = gss;
            SharpMap.Session.Instance
                .SetGeometryServices(gss)
                .SetCoordinateSystemServices(css)
                .SetCoordinateSystemRepository(css);
            //LoadData();

            //服务器
            StartHttpServer();
        }
        private void InitializeMap()
        {
            mapBox = new SharpMap.Forms.MapBox
            {
                Dock = DockStyle.Fill
            };
            this.Controls.Add(mapBox);
        }
        private void InitializeMenu()
        {

            openShapefileMenuItem.Click += OpenShapefileMenuItem_Click;

            layerManagerMenuItem.Click += LayerManagerMenuItem_Click;


        }
        private void OpenShapefileMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Shapefiles (*.shp)|*.shp";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string shapeFilePath = openFileDialog.FileName;
                    string sldPath = GetSldFilePath(shapeFilePath);
                    AddLayer(shapeFilePath, sldPath);
                    string filePath = "D:\\datasource\\WebGIS_Server\\xml\\wms.xml";
                    GenerateCapabilitieswmsXml(filePath);
                }
            }
        }
        private string GetSldFilePath(string shapeFilePath)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "SLD files (*.sld)|*.sld";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    return openFileDialog.FileName;
                }
            }
            return null;
        }
        private void AddLayer(string shapeFilePath, string sldPath)
        {
            VectorLayer vlay = new VectorLayer(System.IO.Path.GetFileNameWithoutExtension(shapeFilePath));
            vlay.DataSource = new SharpMap.Data.Providers.ShapeFile(shapeFilePath, true);

            if (!string.IsNullOrEmpty(sldPath))
            {
                ApplySldStyle(vlay, sldPath);
            }

            mapBox.Map.Layers.Add(vlay);
            mapBox.Map.ZoomToExtents();
            mapBox.Refresh();
        }

        private void ApplySldStyle(VectorLayer layer, string sldPath)
        {
            XmlDocument sldDoc = new XmlDocument();
            sldDoc.Load(sldPath);

            // 解析 Fill 和 Stroke 样式
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(sldDoc.NameTable);
            nsmgr.AddNamespace("sld", "http://www.opengis.net/sld");
            nsmgr.AddNamespace("se", "http://www.opengis.net/se");

            XmlNode fillNode = sldDoc.SelectSingleNode("//sld:Fill/sld:CssParameter[@name='fill']", nsmgr);
            if (fillNode != null)
            {
                string fillColor = fillNode.InnerText;
                layer.Style.Fill = new SolidBrush(ColorTranslator.FromHtml(fillColor));
            }

            XmlNode strokeNode = sldDoc.SelectSingleNode("//sld:Stroke/sld:CssParameter[@name='stroke']", nsmgr);
            if (strokeNode != null)
            {
                string strokeColor = strokeNode.InnerText;
                layer.Style.Outline = new Pen(ColorTranslator.FromHtml(strokeColor));
                layer.Style.EnableOutline = true;
            }
        }
        private void LayerManagerMenuItem_Click(object sender, EventArgs e)
        {
            // 实现图层管理功能
            LayerManagerForm layerManagerForm = new LayerManagerForm(mapBox.Map.Layers);
            layerManagerForm.Show();
        }
        public class LayerManagerForm : Form
        {
            private CheckedListBox layerListBox;
            private IList<ILayer> layers;
            private MapBox mapBox;
            private Button deleteLayerButton;

            public LayerManagerForm(IList<ILayer> layers)
            {
                this.layers = layers;
                //this.mapBox = mapBox;
                InitializeComponents();
            }

            private void InitializeComponents()
            {
                layerListBox = new CheckedListBox
                {
                    Dock = DockStyle.Top,
                    Height = 200
                };

                foreach (var layer in layers)
                {
                    layerListBox.Items.Add(layer.LayerName, layer.Enabled);
                }

                layerListBox.ItemCheck += LayerListBox_ItemCheck;
                deleteLayerButton = new Button
                {
                    Text = "删除选中图层",
                    Dock = DockStyle.Bottom
                };
                //deleteLayerButton.Click += DeleteLayerButton_Click;
                this.Controls.Add(layerListBox);
                //this.Controls.Add(deleteLayerButton);
            }

            private void LayerListBox_ItemCheck(object sender, ItemCheckEventArgs e)
            {
                layers[e.Index].Enabled = e.NewValue == CheckState.Checked;
            }
            private void DeleteLayerButton_Click(object sender, EventArgs e)
            {
                var selectedIndices = layerListBox.CheckedIndices;
                foreach (int index in selectedIndices)
                {
                    layers.RemoveAt(index);
                }

                // 重新加载图层列表
                layerListBox.Items.Clear();
                foreach (var layer in layers)
                {
                    layerListBox.Items.Add(layer.LayerName, layer.Enabled);
                }

                if (mapBox != null)
                {
                    mapBox.Refresh();
                }
                else
                {
                    // 处理空对象的情况，例如记录错误或初始化 mapBox
                }
            }
        }

        private void sliceButton_Click(object sender, EventArgs e)
        {
            //string outputDirectory = "./WMTS_Tiles"; // 你可以根据需要更改输出目录
            //GenerateWmtsTiles(mapBox.Map, outputDirectory);

            string outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WMTS_Tiles");
            GenerateWmtsTiles(mapBox.Map, outputDirectory);
            string filePath = "D:\\datasource\\WebGIS_Server\\xml\\wmts.xml";
            GenerateCapabilitieswmtsXml(filePath);
        }
        private void GenerateWmtsTiles(SharpMap.Map map, string outputDirectory)
        {
            int tileSize = 256; // 每个瓦片的大小
            int zoomLevels = 5; // 生成的缩放级别数量
            int totalTiles = 0;
            int processedTiles = 0;

            // 计算总瓦片数量
            for (int zoom = 0; zoom < zoomLevels; zoom++)
            {
                double resolution = map.Envelope.Width / (tileSize * Math.Pow(2, zoom));
                int tilesX = (int)Math.Ceiling(map.Envelope.Width / (tileSize * resolution));
                int tilesY = (int)Math.Ceiling(map.Envelope.Height / (tileSize * resolution));
                totalTiles += tilesX * tilesY;
            }

            progressBar.Maximum = totalTiles;

            for (int zoom = 0; zoom < zoomLevels; zoom++)
            {
                double resolution = map.Envelope.Width / (tileSize * Math.Pow(2, zoom));
                int tilesX = (int)Math.Ceiling(map.Envelope.Width / (tileSize * resolution));
                int tilesY = (int)Math.Ceiling(map.Envelope.Height / (tileSize * resolution));


                for (int x = 0; x < tilesX; x++)
                {
                    for (int y = 0; y < tilesY; y++)
                    {
                        var tileEnvelope = new GeoAPI.Geometries.Envelope(
                            map.Envelope.MinX + x * tileSize * resolution,
                            map.Envelope.MinX + (x + 1) * tileSize * resolution,
                            map.Envelope.MinY + y * tileSize * resolution,
                            map.Envelope.MinY + (y + 1) * tileSize * resolution);

                        var tileMap = new SharpMap.Map(new Size(tileSize, tileSize))
                        {
                            SRID = map.SRID,
                            //BackgroundLayer = map.BackgroundLayer,
                            BackColor = map.BackColor
                        };

                        foreach (var layer in map.Layers)
                        {
                            tileMap.Layers.Add(layer);
                        }

                        tileMap.ZoomToBox(tileEnvelope);

                        using (var bitmap = new Bitmap(tileSize, tileSize))
                        {
                            using (var graphics = Graphics.FromImage(bitmap))
                            {
                                tileMap.RenderMap(graphics);
                            }

                            string tilePath = Path.Combine(outputDirectory, $"{zoom}/{x}/{y}.png");
                            Directory.CreateDirectory(Path.GetDirectoryName(tilePath));
                            bitmap.Save(tilePath, System.Drawing.Imaging.ImageFormat.Png);
                            //bitmap.Save("D:/大三上/网络基础与WebGIS/小组作业/4.webGIS服务器/WindowsFormsApp4/WMTS_Tiles/1.png", System.Drawing.Imaging.ImageFormat.Png);
                        }

                        processedTiles++;
                        progressBar.Value = processedTiles;
                    }
                }
            }
            MessageBox.Show("切片完成！");


        }
        private void StartHttpServer()
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://localhost:8080/");
            listenerThread = new Thread(new ThreadStart(ListenForRequests));
            listenerThread.Start();
        }

        private void ListenForRequests()
        {
            httpListener.Start();
            while (true)
            {
                HttpListenerContext context = httpListener.GetContext();
                ProcessRequest(context);
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            string url = context.Request.Url.AbsolutePath;
            string query = context.Request.Url.Query;
            string[] parts = url.Split('/');
            if (parts.Length > 1 && parts[1].Equals("wms", StringComparison.OrdinalIgnoreCase))
            {
                ProcessWmsRequest(context, query);
            }
            else if (parts.Length > 1 && parts[1].Equals("wmts", StringComparison.OrdinalIgnoreCase))
            {
                ProcessWmtsRequest(context, query, parts);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.Close();
            }
        }
        private void ProcessWmsRequest(HttpListenerContext context, string query)
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(query);
            string requestType = queryParams["REQUEST"];

            if (requestType.Equals("GetCapabilities", StringComparison.OrdinalIgnoreCase))
            {
                string capabilitiesXml = GetWmsCapabilities();
                context.Response.ContentType = "text/xml";
                using (StreamWriter writer = new StreamWriter(context.Response.OutputStream))
                {
                    writer.Write(capabilitiesXml);
                }
            }
            else if (requestType.Equals("GetMap", StringComparison.OrdinalIgnoreCase))
            {
                ProcessGetMapRequest(context, queryParams);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            context.Response.Close();
        }
        private void ProcessGetMapRequest(HttpListenerContext context, System.Collections.Specialized.NameValueCollection queryParams)
        {
            string layers = queryParams["LAYERS"];
            string styles = queryParams["STYLES"];
            string crs = queryParams["CRS"];
            string bbox = queryParams["BBOX"];
            int width = int.Parse(queryParams["WIDTH"]);
            int height = int.Parse(queryParams["HEIGHT"]);
            string format = queryParams["FORMAT"];

            // 解析 BBOX 参数
            string[] bboxParts = bbox.Split(',');
            double minX = double.Parse(bboxParts[0]);
            double minY = double.Parse(bboxParts[1]);
            double maxX = double.Parse(bboxParts[2]);
            double maxY = double.Parse(bboxParts[3]);

            // 创建地图对象
            var map = new SharpMap.Map(new Size(width, height))
            {
                SRID = GetSridFromCrs(crs),
                BackColor = Color.White
            };

            // 添加图层
            foreach (var layerName in layers.Split(','))
            {
                var layer = mapBox.Map.Layers.FirstOrDefault(l => l.LayerName == layerName);
                if (layer != null)
                {
                    map.Layers.Add(layer);
                }
            }

            // 设置地图范围
            map.ZoomToBox(new GeoAPI.Geometries.Envelope(minX, maxX, minY, maxY));

            // 渲染地图
            using (var bitmap = new Bitmap(width, height))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    map.RenderMap(graphics);
                }

                // 将地图图像写入响应
                context.Response.ContentType = format;
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, GetImageFormat(format));
                    ms.WriteTo(context.Response.OutputStream);
                }
            }
        }

        private int GetSridFromCrs(string crs)
        {
            // 解析 CRS 参数并返回相应的 SRID
            if (crs.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase))
            {
                return int.Parse(crs.Substring(5));
            }
            throw new ArgumentException("Unsupported CRS: " + crs);
        }

        private System.Drawing.Imaging.ImageFormat GetImageFormat(string format)
        {
            switch (format.ToLower())
            {
                case "image/png":
                    return System.Drawing.Imaging.ImageFormat.Png;
                case "image/jpeg":
                    return System.Drawing.Imaging.ImageFormat.Jpeg;
                default:
                    throw new ArgumentException("Unsupported format: " + format);
            }
        }

        private void ProcessWmtsRequest(HttpListenerContext context, string query, string[] parts)
        {
            var queryParams = HttpUtility.ParseQueryString(query);
            string requestType = queryParams["REQUEST"];

            if (requestType.Equals("GetCapabilities", StringComparison.OrdinalIgnoreCase))
            {
                string capabilitiesXml = GetWmtsCapabilities();
                context.Response.ContentType = "text/xml";
                using (StreamWriter writer = new StreamWriter(context.Response.OutputStream))
                {
                    writer.Write(capabilitiesXml);
                }
            }
            else if (requestType.Equals("GetTile", StringComparison.OrdinalIgnoreCase))
            {
                int zoom = int.Parse(queryParams["TILEMATRIX"]);
                int x = int.Parse(queryParams["TILECOL"]);
                int y = int.Parse(queryParams["TILEROW"]);

                string tilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WMTS_Tiles", $"{zoom}\\{x}\\{y}.png");
                if (File.Exists(tilePath))
                {
                    context.Response.ContentType = "image/png";
                    using (FileStream fs = File.OpenRead(tilePath))
                    {
                        fs.CopyTo(context.Response.OutputStream);
                    }
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            context.Response.Close();
        }

        private string GetWmsCapabilities()
        {

            // 返回 WMS GetCapabilities 响应的 XML 字符串
            string filePath = "D:\\datasource\\WebGIS_Server\\xml\\wms.xml";
            return File.ReadAllText(filePath);
        }
        public void GenerateCapabilitieswmsXml(string outputFilePath)
        {
            // 创建 XML 文档
            XmlDocument xmlDoc = new XmlDocument();

            // 创建 WMS 服务的根节点
            XmlElement rootElement = xmlDoc.CreateElement("WMS_Capabilities");
            rootElement.SetAttribute("version", "1.3.0");  // WMS 版本，可以根据需要修改
            xmlDoc.AppendChild(rootElement);

            // 创建 Service 元素（包含服务的基本信息）
            XmlElement serviceElement = xmlDoc.CreateElement("Service");
            rootElement.AppendChild(serviceElement);

            XmlElement nameElement = xmlDoc.CreateElement("Name");
            nameElement.InnerText = "OGC:WMS";
            serviceElement.AppendChild(nameElement);

            XmlElement titleElement = xmlDoc.CreateElement("Title");
            titleElement.InnerText = "Sample WMS Service";
            serviceElement.AppendChild(titleElement);

            // 创建 Capabilities 元素（包含具体图层信息）
            XmlElement capabilityElement = xmlDoc.CreateElement("Capability");
            rootElement.AppendChild(capabilityElement);

            // 创建 Layers 元素，包含图层信息
            XmlElement layersElement = xmlDoc.CreateElement("Layer");
            capabilityElement.AppendChild(layersElement);

            
            // 为每个图层添加 Layer 元素
            foreach (var layer in mapBox.Map.Layers)
            {
                if (layer is VectorLayer vectorLayer)
                {
                    XmlElement layerElement = xmlDoc.CreateElement("Layer");
                    layersElement.AppendChild(layerElement);

                    // 图层名称
                    XmlElement layerNameElement = xmlDoc.CreateElement("Name");
                    layerNameElement.InnerText = vectorLayer.LayerName;
                    layerElement.AppendChild(layerNameElement);

                    // 图层标题
                    XmlElement layerTitleElement = xmlDoc.CreateElement("Title");
                    layerTitleElement.InnerText = vectorLayer.LayerName;  // 假设标题与名称相同
                    layerElement.AppendChild(layerTitleElement);

                    // 图层样式（如果有样式的话）
                    if (vectorLayer.Style != null)
                    {
                        XmlElement styleElement = xmlDoc.CreateElement("Style");
                        XmlElement styleNameElement = xmlDoc.CreateElement("Name");
                        styleNameElement.InnerText = vectorLayer.Style.ToString();  // 假设样式是简单的名称
                        styleElement.AppendChild(styleNameElement);
                        layerElement.AppendChild(styleElement);
                    }

                    // 其他信息（例如坐标参考系统）
                    XmlElement crsElement = xmlDoc.CreateElement("CRS");
                    crsElement.InnerText = "EPSG:4326";  // 假设你使用的是 WGS84 坐标系统
                    layerElement.AppendChild(crsElement);
                }
            }

            // 保存 XML 文件
            xmlDoc.Save(outputFilePath);
        }
        private string GetWmtsCapabilities()
        {
            // 返回 WMTS GetCapabilities 响应的 XML 字符串
            string filePath = "D:\\datasource\\WebGIS_Server\\xml\\wmts.xml";
            
            return File.ReadAllText(filePath);
        }

        public void GenerateCapabilitieswmtsXml(string outputFilePath)
        {
            // 创建 XML 文档
            XmlDocument xmlDoc = new XmlDocument();

            // 创建 WMS 服务的根节点
            XmlElement rootElement = xmlDoc.CreateElement("WMS_Capabilities");
            rootElement.SetAttribute("version", "1.3.0");  // WMS 版本，可以根据需要修改
            xmlDoc.AppendChild(rootElement);

            // 创建 Service 元素（包含服务的基本信息）
            XmlElement serviceElement = xmlDoc.CreateElement("Service");
            rootElement.AppendChild(serviceElement);

            XmlElement nameElement = xmlDoc.CreateElement("Name");
            nameElement.InnerText = "OGC:WMS";
            serviceElement.AppendChild(nameElement);

            XmlElement titleElement = xmlDoc.CreateElement("Title");
            titleElement.InnerText = "Sample WMS Service";
            serviceElement.AppendChild(titleElement);

            // 创建 Capabilities 元素（包含具体图层信息）
            XmlElement capabilityElement = xmlDoc.CreateElement("Capability");
            rootElement.AppendChild(capabilityElement);

            // 创建 Layers 元素，包含图层信息
            XmlElement layersElement = xmlDoc.CreateElement("Layer");
            capabilityElement.AppendChild(layersElement);


            // 为每个图层添加 Layer 元素
            foreach (var layer in mapBox.Map.Layers)
            {
                if (layer is VectorLayer vectorLayer)
                {
                    XmlElement layerElement = xmlDoc.CreateElement("Layer");
                    layersElement.AppendChild(layerElement);

                    // 图层名称
                    XmlElement layerNameElement = xmlDoc.CreateElement("Name");
                    layerNameElement.InnerText = vectorLayer.LayerName;
                    layerElement.AppendChild(layerNameElement);

                    // 图层标题
                    XmlElement layerTitleElement = xmlDoc.CreateElement("Title");
                    layerTitleElement.InnerText = vectorLayer.LayerName;  // 假设标题与名称相同
                    layerElement.AppendChild(layerTitleElement);

                    // 图层样式（如果有样式的话）
                    if (vectorLayer.Style != null)
                    {
                        XmlElement styleElement = xmlDoc.CreateElement("Style");
                        XmlElement styleNameElement = xmlDoc.CreateElement("Name");
                        styleNameElement.InnerText = vectorLayer.Style.ToString();  // 假设样式是简单的名称
                        styleElement.AppendChild(styleNameElement);
                        layerElement.AppendChild(styleElement);
                    }

                    // 其他信息（例如坐标参考系统）
                    XmlElement crsElement = xmlDoc.CreateElement("CRS");
                    crsElement.InnerText = "EPSG:4326";  // 假设你使用的是 WGS84 坐标系统
                    layerElement.AppendChild(crsElement);
                }
            }
            // 保存 XML 文件
            xmlDoc.Save(outputFilePath);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            httpListener.Stop();
            listenerThread.Abort();
            base.OnFormClosing(e);
        }
    }
}
