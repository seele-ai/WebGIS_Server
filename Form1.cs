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
                    string wmsCapabilitiesPath = "D:\\datasource\\WebGIS_Server\\xml\\wms.xml";
                    GenerateWmsCapabilitiesXml(wmsCapabilitiesPath);
                   
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
            string wmtsCapabilitiesPath = "D:\\datasource\\WebGIS_Server\\xml\\wmts.xml";
            GenerateWmtsCapabilitiesXml(wmtsCapabilitiesPath);
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
        
        private string GetWmtsCapabilities()
        {
            // 返回 WMTS GetCapabilities 响应的 XML 字符串
            string filePath = "D:\\datasource\\WebGIS_Server\\xml\\wmts.xml";
            return File.ReadAllText(filePath);
        }

        private XmlDocument CreateBaseCapabilitiesXml(string serviceType, string version)
        {
            XmlDocument xmlDoc = new XmlDocument();

            // 创建 XML 声明
            XmlDeclaration xmlDeclaration = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
            xmlDoc.AppendChild(xmlDeclaration);

            // 根据服务类型定义命名空间
            string ns = serviceType == "WMS" ? "http://www.opengis.net/wms" : "http://www.opengis.net/wmts/1.0";
            string xsi = "http://www.w3.org/2001/XMLSchema-instance";
            string schemaLocation = serviceType == "WMS"
                ? "http://www.opengis.net/wms http://schemas.opengis.net/wms/1.3.0/capabilities_1_3_0.xsd"
                : "http://www.opengis.net/wmts/1.0 http://schemas.opengis.net/wmts/1.0/wmtsGetCapabilities_response.xsd";

            // 创建根元素
            XmlElement rootElement = xmlDoc.CreateElement($"{serviceType}_Capabilities", ns);
            rootElement.SetAttribute("version", version);
            rootElement.SetAttribute("xmlns", ns);
            rootElement.SetAttribute("xmlns:xsi", xsi);
            rootElement.SetAttribute("xsi:schemaLocation", schemaLocation);
            xmlDoc.AppendChild(rootElement);

            return xmlDoc;
        }

        public void GenerateWmsCapabilitiesXml(string outputFilePath)
        {
            XmlDocument xmlDoc = CreateBaseCapabilitiesXml("WMS", "1.3.0");
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("wms", "http://www.opengis.net/wms");

            XmlElement root = xmlDoc.DocumentElement;

            // 服务部分
            XmlElement serviceElement = xmlDoc.CreateElement("Service", root.NamespaceURI);
            root.AppendChild(serviceElement);

            XmlElement nameElement = xmlDoc.CreateElement("Name", root.NamespaceURI);
            nameElement.InnerText = "WMS";
            serviceElement.AppendChild(nameElement);

            XmlElement titleElement = xmlDoc.CreateElement("Title", root.NamespaceURI);
            titleElement.InnerText = "Sample WMS Service";
            serviceElement.AppendChild(titleElement);

            // Capability 部分
            XmlElement capabilityElement = xmlDoc.CreateElement("Capability", root.NamespaceURI);
            root.AppendChild(capabilityElement);

            // 添加请求信息（简化版）
            XmlElement requestElement = xmlDoc.CreateElement("Request", root.NamespaceURI);
            capabilityElement.AppendChild(requestElement);

            XmlElement getCapabilities = xmlDoc.CreateElement("GetCapabilities", root.NamespaceURI);
            getCapabilities.SetAttribute("Format", "text/xml");
            XmlElement dcpType = xmlDoc.CreateElement("DCPType", root.NamespaceURI);
            XmlElement httpElement = xmlDoc.CreateElement("HTTP", root.NamespaceURI);
            XmlElement getElement = xmlDoc.CreateElement("Get", root.NamespaceURI);
            XmlElement onlineResource = xmlDoc.CreateElement("OnlineResource", root.NamespaceURI);
            onlineResource.SetAttribute("xlink:type", "simple");
            onlineResource.SetAttribute("xlink:href", "http://localhost:8080/wms");
            getElement.AppendChild(onlineResource);
            httpElement.AppendChild(getElement);
            dcpType.AppendChild(httpElement);
            getCapabilities.AppendChild(dcpType);
            requestElement.AppendChild(getCapabilities);

            XmlElement getMap = xmlDoc.CreateElement("GetMap", root.NamespaceURI);
            XmlElement format1 = xmlDoc.CreateElement("Format",root.NamespaceURI);
            format1.InnerText = "image/png";
            XmlElement format2 = xmlDoc.CreateElement("Format", root.NamespaceURI);
            format2.InnerText = "image/jpeg";
            dcpType = xmlDoc.CreateElement("DCPType", root.NamespaceURI);
            httpElement = xmlDoc.CreateElement("HTTP", root.NamespaceURI);
            getElement = xmlDoc.CreateElement("Get", root.NamespaceURI);
            onlineResource = xmlDoc.CreateElement("OnlineResource", root.NamespaceURI);
            onlineResource.SetAttribute("xlink:type", "simple");
            onlineResource.SetAttribute("xlink:href", "http://localhost:8080/wms");
            getElement.AppendChild(onlineResource);
            httpElement.AppendChild(getElement);
            dcpType.AppendChild(httpElement);
            getMap.AppendChild(format1);
            getMap.AppendChild(format2);
            getMap.AppendChild(dcpType);
            requestElement.AppendChild(getMap);

            // 添加异常处理
            XmlElement exceptionElement = xmlDoc.CreateElement("Exception", root.NamespaceURI);
            capabilityElement.AppendChild(exceptionElement);

            XmlElement exceptionFormat = xmlDoc.CreateElement("Format", root.NamespaceURI);
            exceptionFormat.InnerText = "XML";
            exceptionElement.AppendChild(exceptionFormat);

            XmlElement exceptionFormat2 = xmlDoc.CreateElement("Format", root.NamespaceURI);
            exceptionFormat2.InnerText = "INIMAGE";
            exceptionElement.AppendChild(exceptionFormat2);

            // 添加图层信息
            XmlElement layersElement = xmlDoc.CreateElement("Layer", root.NamespaceURI);
            capabilityElement.AppendChild(layersElement);

            

            XmlElement baseTitle = xmlDoc.CreateElement("Title", root.NamespaceURI);
            baseTitle.InnerText = "Base Layer";
            layersElement.AppendChild(baseTitle);
            XmlElement crsElement1 = xmlDoc.CreateElement("CRS", root.NamespaceURI);
            crsElement1.InnerText = "EPSG:4326"; // 根据实际 CRS 调整
            layersElement.AppendChild(crsElement1);

            // 边界框
            XmlElement bboxElement1 = xmlDoc.CreateElement("EX_GeographicBoundingBox", "http://www.opengis.net/ows/1.1");
            XmlElement westBound1 = xmlDoc.CreateElement("westBoundLongitude", "http://www.opengis.net/ows/1.1");
            westBound1.InnerText = mapBox.Map.Envelope.MinX.ToString();
            XmlElement eastBound1 = xmlDoc.CreateElement("eastBoundLongitude", "http://www.opengis.net/ows/1.1");
            eastBound1.InnerText = mapBox.Map.Envelope.MaxX.ToString();
            XmlElement southBound1 = xmlDoc.CreateElement("southBoundLatitude", "http://www.opengis.net/ows/1.1");
            southBound1.InnerText = mapBox.Map.Envelope.MinY.ToString();
            XmlElement northBound1 = xmlDoc.CreateElement("northBoundLatitude", "http://www.opengis.net/ows/1.1");
            northBound1.InnerText = mapBox.Map.Envelope.MaxY.ToString();

            bboxElement1.AppendChild(westBound1);
            bboxElement1.AppendChild(eastBound1);
            bboxElement1.AppendChild(southBound1);
            bboxElement1.AppendChild(northBound1);
            layersElement.AppendChild(bboxElement1);
            XmlElement bjBoundingBoxAttr1 = xmlDoc.CreateElement("BoundingBox", root.NamespaceURI);
            bjBoundingBoxAttr1.SetAttribute("CRS", "EPSG:4326");
            bjBoundingBoxAttr1.SetAttribute("miny", "39.4");
            bjBoundingBoxAttr1.SetAttribute("minx", "115.4");
            bjBoundingBoxAttr1.SetAttribute("maxy", "41.0");
            bjBoundingBoxAttr1.SetAttribute("maxx", "117.5");
            layersElement.AppendChild(bjBoundingBoxAttr1);

            // 遍历地图图层
            foreach (var layer in mapBox.Map.Layers.OfType<VectorLayer>())
            {
                XmlElement layerElement = xmlDoc.CreateElement("Layer", root.NamespaceURI);
                layersElement.AppendChild(layerElement);

                // 图层名称
                XmlElement layerName = xmlDoc.CreateElement("Name", root.NamespaceURI);
                layerName.InnerText = layer.LayerName;
                layerElement.AppendChild(layerName);

                // 图层标题
                XmlElement layerTitle = xmlDoc.CreateElement("Title", root.NamespaceURI);
                layerTitle.InnerText = layer.LayerName; // 根据需要自定义
                layerElement.AppendChild(layerTitle);

                // CRS
                XmlElement crsElement = xmlDoc.CreateElement("CRS", root.NamespaceURI);
                crsElement.InnerText = "EPSG:4326"; // 根据实际 CRS 调整
                layerElement.AppendChild(crsElement);

                // 边界框
                XmlElement bboxElement = xmlDoc.CreateElement("EX_GeographicBoundingBox", "http://www.opengis.net/ows/1.1");
                XmlElement westBound = xmlDoc.CreateElement("westBoundLongitude", "http://www.opengis.net/ows/1.1");
                westBound.InnerText = mapBox.Map.Envelope.MinX.ToString();
                XmlElement eastBound = xmlDoc.CreateElement("eastBoundLongitude", "http://www.opengis.net/ows/1.1");
                eastBound.InnerText = mapBox.Map.Envelope.MaxX.ToString();
                XmlElement southBound = xmlDoc.CreateElement("southBoundLatitude", "http://www.opengis.net/ows/1.1");
                southBound.InnerText = mapBox.Map.Envelope.MinY.ToString();
                XmlElement northBound = xmlDoc.CreateElement("northBoundLatitude", "http://www.opengis.net/ows/1.1");
                northBound.InnerText = mapBox.Map.Envelope.MaxY.ToString();

                bboxElement.AppendChild(westBound);
                bboxElement.AppendChild(eastBound);
                bboxElement.AppendChild(southBound);
                bboxElement.AppendChild(northBound);
                layerElement.AppendChild(bboxElement);

                XmlElement bjBoundingBoxAttr = xmlDoc.CreateElement("BoundingBox", root.NamespaceURI);
                bjBoundingBoxAttr.SetAttribute("CRS", "EPSG:4326");
                bjBoundingBoxAttr.SetAttribute("miny", "39.4");
                bjBoundingBoxAttr.SetAttribute("minx", "115.4");
                bjBoundingBoxAttr.SetAttribute("maxy", "41.0");
                bjBoundingBoxAttr.SetAttribute("maxx", "117.5");
                layerElement.AppendChild(bjBoundingBoxAttr);
                // 样式
                XmlElement styleElement = xmlDoc.CreateElement("Style", root.NamespaceURI);
                layerElement.AppendChild(styleElement);

                XmlElement styleName = xmlDoc.CreateElement("Name", root.NamespaceURI);
                styleName.InnerText = "default";
                styleElement.AppendChild(styleName);

                XmlElement styleTitle = xmlDoc.CreateElement("Title", root.NamespaceURI);
                styleTitle.InnerText = "Default Style";
                styleElement.AppendChild(styleTitle);
            }

            // 保存 XML
            xmlDoc.Save(outputFilePath);
        }

        public void GenerateWmtsCapabilitiesXml(string outputFilePath)
        {
            XmlDocument xmlDoc = CreateBaseCapabilitiesXml("WMTS", "1.0.0");

            // 定义必要的命名空间
            string ows = "http://www.opengis.net/ows/1.1";
            string wmts = "http://www.opengis.net/wmts/1.0";
            string xlink = "http://www.w3.org/1999/xlink";

            XmlElement root = xmlDoc.DocumentElement;

            // 添加必要的命名空间
            root.SetAttribute("xmlns:ows", ows);
            root.SetAttribute("xmlns:xlink", xlink);
            root.SetAttribute("xmlns:gml", "http://www.opengis.net/gml");
            root.SetAttribute("xmlns:wmts", wmts);

            // 服务识别
            XmlElement serviceIdentification = xmlDoc.CreateElement("ows:ServiceIdentification", ows);
            root.AppendChild(serviceIdentification);

            XmlElement title = xmlDoc.CreateElement("ows:Title", ows);
            title.InnerText = "Sample WMTS Service";
            serviceIdentification.AppendChild(title);

            XmlElement serviceType = xmlDoc.CreateElement("ows:ServiceType", ows);
            serviceType.InnerText = "OGC WMTS";
            serviceIdentification.AppendChild(serviceType);

            XmlElement serviceTypeVersion = xmlDoc.CreateElement("ows:ServiceTypeVersion", ows);
            serviceTypeVersion.InnerText = "1.0.0";
            serviceIdentification.AppendChild(serviceTypeVersion);

            // 服务提供者
            XmlElement serviceProvider = xmlDoc.CreateElement("ows:ServiceProvider", ows);
            root.AppendChild(serviceProvider);

            XmlElement providerName = xmlDoc.CreateElement("ows:ProviderName", ows);
            providerName.InnerText = "Sample Provider";
            serviceProvider.AppendChild(providerName);

            // 操作元数据
            XmlElement operationsMetadata = xmlDoc.CreateElement("ows:OperationsMetadata", ows);
            root.AppendChild(operationsMetadata);
            // 在此处定义 GetCapabilities, GetTile 等操作

            // 内容部分
            XmlElement contents = xmlDoc.CreateElement("Contents", wmts);
            root.AppendChild(contents);

            foreach (var layer in mapBox.Map.Layers.OfType<VectorLayer>())
            {
                XmlElement layerElement = xmlDoc.CreateElement("Layer", wmts);
                contents.AppendChild(layerElement);

                // 标识符
                XmlElement identifier = xmlDoc.CreateElement("ows:Identifier", ows);
                identifier.InnerText = layer.LayerName;
                layerElement.AppendChild(identifier);

                // 标题
                XmlElement layerTitle = xmlDoc.CreateElement("ows:Title", ows);
                layerTitle.InnerText = layer.LayerName; // 根据需要自定义
                layerElement.AppendChild(layerTitle);

                // WGS84 边界框
                XmlElement wgs84BoundingBox = xmlDoc.CreateElement("ows:WGS84BoundingBox", ows);
                layerElement.AppendChild(wgs84BoundingBox);

                XmlElement lowerCorner = xmlDoc.CreateElement("ows:LowerCorner", ows);
                lowerCorner.InnerText = $"{mapBox.Map.Envelope.MinX} {mapBox.Map.Envelope.MinY}";
                wgs84BoundingBox.AppendChild(lowerCorner);

                XmlElement upperCorner = xmlDoc.CreateElement("ows:UpperCorner", ows);
                upperCorner.InnerText = $"{mapBox.Map.Envelope.MaxX} {mapBox.Map.Envelope.MaxY}";
                wgs84BoundingBox.AppendChild(upperCorner);

                // 支持的 CRS
                XmlElement supportedCRS = xmlDoc.CreateElement("ows:SupportedCRS", ows);
                supportedCRS.InnerText = "urn:ogc:def:crs:EPSG::4326"; // 根据需要调整
                layerElement.AppendChild(supportedCRS);

                // TileMatrixSetLink
                XmlElement tileMatrixSetLink = xmlDoc.CreateElement("TileMatrixSetLink", wmts);
                layerElement.AppendChild(tileMatrixSetLink);

                XmlElement tileMatrixSet = xmlDoc.CreateElement("TileMatrixSet", wmts);
                tileMatrixSet.InnerText = "GoogleMapsCompatible"; // 定义您的 TileMatrixSet
                tileMatrixSetLink.AppendChild(tileMatrixSet);

                // 格式
                XmlElement format = xmlDoc.CreateElement("Format", wmts);
                format.InnerText = "image/png";
                layerElement.AppendChild(format);

                // 样式
                XmlElement style = xmlDoc.CreateElement("Style", wmts);
                layerElement.AppendChild(style);

                XmlElement defaultStyle = xmlDoc.CreateElement("Default", wmts);
                defaultStyle.InnerText = "default";
                style.AppendChild(defaultStyle);

                XmlElement styleIdentifier = xmlDoc.CreateElement("ows:Identifier", ows);
                styleIdentifier.InnerText = "default";
                style.AppendChild(styleIdentifier);
            }

            // TileMatrixSet 定义
            XmlElement tileMatrixSets = xmlDoc.CreateElement("TileMatrixSet", wmts);
            contents.AppendChild(tileMatrixSets);

            // 示例 TileMatrixSet（Google Maps 兼容）
            XmlElement tileMatrixSetElement = xmlDoc.CreateElement("TileMatrixSet", wmts);
            tileMatrixSets.AppendChild(tileMatrixSetElement);

            XmlElement identifierTM = xmlDoc.CreateElement("ows:Identifier", ows);
            identifierTM.InnerText = "GoogleMapsCompatible";
            tileMatrixSetElement.AppendChild(identifierTM);

            XmlElement supportedCRSTM = xmlDoc.CreateElement("ows:SupportedCRS", ows);
            supportedCRSTM.InnerText = "urn:ogc:def:crs:EPSG::3857";
            tileMatrixSetElement.AppendChild(supportedCRSTM);

            // 定义不同缩放级别的 TileMatrix 元素
            for (int z = 0; z <= 5; z++) // 根据需要调整缩放级别
            {
                XmlElement tileMatrix = xmlDoc.CreateElement("TileMatrix", wmts);
                tileMatrixSetElement.AppendChild(tileMatrix);

                XmlElement identifierZ = xmlDoc.CreateElement("ows:Identifier", ows);
                identifierZ.InnerText = z.ToString();
                tileMatrix.AppendChild(identifierZ);

                XmlElement scaleDenominator = xmlDoc.CreateElement("ScaleDenominator", wmts);
                scaleDenominator.InnerText = (156543.03392804062 / Math.Pow(2, z)).ToString("F6");
                tileMatrix.AppendChild(scaleDenominator);

                XmlElement topLeftCorner = xmlDoc.CreateElement("TopLeftCorner", wmts);
                topLeftCorner.InnerText = "-20037508.3427892 20037508.3427892"; // 根据需要调整
                tileMatrix.AppendChild(topLeftCorner);

                XmlElement tileWidth = xmlDoc.CreateElement("TileWidth", wmts);
                tileWidth.InnerText = "256";
                tileMatrix.AppendChild(tileWidth);

                XmlElement tileHeight = xmlDoc.CreateElement("TileHeight", wmts);
                tileHeight.InnerText = "256";
                tileMatrix.AppendChild(tileHeight);

                XmlElement matrixWidth = xmlDoc.CreateElement("MatrixWidth", wmts);
                matrixWidth.InnerText = $"{Math.Pow(2, z)}";
                tileMatrix.AppendChild(matrixWidth);

                XmlElement matrixHeight = xmlDoc.CreateElement("MatrixHeight", wmts);
                matrixHeight.InnerText = $"{Math.Pow(2, z)}";
                tileMatrix.AppendChild(matrixHeight);
            }

            // 保存 XML
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
