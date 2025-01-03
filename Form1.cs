﻿using System;
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
using System.Xml.Linq;
using GeoAPI.Geometries;
using System.Drawing.Imaging;
using System.Globalization;
using static System.Windows.Forms.LinkLabel;
using System.Web.UI.WebControls;

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
                    string wmsCapabilitiesPath = "D:\\datasource\\GISServer\\xml\\wms.xml";
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
            //XmlDocument sldDoc = new XmlDocument();
            //sldDoc.Load(sldPath);

            //// 解析 Fill 和 Stroke 样式
            //XmlNamespaceManager nsmgr = new XmlNamespaceManager(sldDoc.NameTable);
            //nsmgr.AddNamespace("sld", "http://www.opengis.net/sld");
            //nsmgr.AddNamespace("se", "http://www.opengis.net/se");

            //XmlNode fillNode = sldDoc.SelectSingleNode("//se:Fill/se:SvgParameter[@name='fill']", nsmgr);
            //if (fillNode != null)
            //{
            //    string fillColor = fillNode.InnerText;
            //    layer.Style.Fill = new SolidBrush(ColorTranslator.FromHtml(fillColor));
            //}

            //XmlNode strokeNode = sldDoc.SelectSingleNode("//se:Stroke/se:SvgParameter[@name='stroke']", nsmgr);
            //if (strokeNode != null)
            //{
            //    string strokeColor = strokeNode.InnerText;
            //    layer.Style.Outline = new Pen(ColorTranslator.FromHtml(strokeColor));
            //    layer.Style.EnableOutline = true;
            //}
            XmlDocument sldDoc = new XmlDocument();
            sldDoc.Load(sldPath);

            // 解析 Fill 和 Stroke 样式
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(sldDoc.NameTable);
            nsmgr.AddNamespace("sld", "http://www.opengis.net/sld");
            nsmgr.AddNamespace("se", "http://www.opengis.net/se");

            // 解析填充颜色及透明度
            XmlNode fillNode = sldDoc.SelectSingleNode("//se:Fill/se:SvgParameter[@name='fill']", nsmgr);
            XmlNode fillOpacityNode = sldDoc.SelectSingleNode("//se:Fill/se:SvgParameter[@name='fill-opacity']", nsmgr);
            if (fillNode != null)
            {
                string fillColor = fillNode.InnerText;
                float fillOpacity = 1.0f;
                if (fillOpacityNode != null && float.TryParse(fillOpacityNode.InnerText, out float parsedOpacity))
                {
                    fillOpacity = parsedOpacity;
                }
                Color fillColorWithOpacity = Color.FromArgb((int)(fillOpacity * 255), ColorTranslator.FromHtml(fillColor));
                layer.Style.Fill = new SolidBrush(fillColorWithOpacity);
            }

            // 解析边界样式
            XmlNode strokeNode = sldDoc.SelectSingleNode("//se:Stroke/se:SvgParameter[@name='stroke']", nsmgr);
            XmlNode strokeWidthNode = sldDoc.SelectSingleNode("//se:Stroke/se:SvgParameter[@name='stroke-width']", nsmgr);
            XmlNode strokeLinejoinNode = sldDoc.SelectSingleNode("//se:Stroke/se:SvgParameter[@name='stroke-linejoin']", nsmgr);
            XmlNode strokeLinecapNode = sldDoc.SelectSingleNode("//se:Stroke/se:SvgParameter[@name='stroke-linecap']", nsmgr);
            XmlNode strokeOpacityNode = sldDoc.SelectSingleNode("//se:Stroke/se:SvgParameter[@name='stroke-opacity']", nsmgr);

            if (strokeNode != null)
            {
                string strokeColor = strokeNode.InnerText;
                float strokeOpacity = 1.0f;
                if (strokeOpacityNode != null && float.TryParse(strokeOpacityNode.InnerText, out float parsedStrokeOpacity))
                {
                    strokeOpacity = parsedStrokeOpacity;
                }

                float strokeWidth = 1.0f;
                if (strokeWidthNode != null && float.TryParse(strokeWidthNode.InnerText, out float parsedStrokeWidth))
                {
                    strokeWidth = parsedStrokeWidth;
                }

                string strokeLinejoin = strokeLinejoinNode?.InnerText ?? "miter"; // 默认值
                string strokeLinecap = strokeLinecapNode?.InnerText ?? "butt";   // 默认值

                Pen strokePen = new Pen(Color.FromArgb((int)(strokeOpacity * 255), ColorTranslator.FromHtml(strokeColor)), strokeWidth)
                {
                    LineJoin = strokeLinejoin.ToLower() == "bevel" ? System.Drawing.Drawing2D.LineJoin.Bevel :
                               strokeLinejoin.ToLower() == "round" ? System.Drawing.Drawing2D.LineJoin.Round :
                               System.Drawing.Drawing2D.LineJoin.Miter,
                    StartCap = strokeLinecap.ToLower() == "round" ? System.Drawing.Drawing2D.LineCap.Round :
                               strokeLinecap.ToLower() == "square" ? System.Drawing.Drawing2D.LineCap.Square :
                               System.Drawing.Drawing2D.LineCap.Flat
                };

                layer.Style.Outline = strokePen;
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
            private System.Windows.Forms.Button deleteLayerButton;
            
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
                deleteLayerButton = new System.Windows.Forms.Button
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
                }
            }
        }

        private void sliceButton_Click(object sender, EventArgs e)
        {
            //string outputDirectory = "./WMTS_Tiles"; // 你可以根据需要更改输出目录
            //GenerateWmtsTiles(mapBox.Map, outputDirectory);

            string outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WMTS_Tiles");
            GenerateWmtsTiles(mapBox.Map, outputDirectory);
            string wmtsCapabilitiesPath = "D:\\datasource\\GISServer\\xml\\wmts1.xml";
            GenerateWmtsCapabilitiesXml(wmtsCapabilitiesPath);
        }
        private void GenerateWmtsTiles(SharpMap.Map map, string outputDirectory)
        {
            int tileSize = 256; // 每个瓦片的大小（像素）
            int zoomLevels = 11; // 生成的缩放级别数量（0 到 4）
            int processedTiles = 0;

            // 计算地图的总范围（在EPSG:4326坐标系下，全球的经纬度范围）
            double minX = -180; // 经度范围：-180
            double maxX = 180;  // 经度范围：180
            double minY = -90;  // 纬度范围：-90
            double maxY = 90;   // 纬度范围：90

            // 计算地图的宽度和高度
            double mapWidth = maxX - minX;
            double mapHeight = maxY - minY;

            // 设置进度条的最大值
            int totalTiles = 0;
            for (int zoom = 0; zoom < zoomLevels; zoom++)
            {
                int tilesPerSide = (int)Math.Pow(2, zoom);
                totalTiles += tilesPerSide * tilesPerSide*2;
            }
            progressBar.Maximum = totalTiles;
            progressBar.Value = 0;

            // 遍历每个缩放级别
            for (int zoom = 0; zoom < zoomLevels; zoom++)
            {
                int tilesPerSide = (int)Math.Pow(2, zoom); // 每边的瓦片数量为2的zoom次方

                // 计算每个瓦片的地理范围
                double tileWidth = mapWidth / tilesPerSide/2;
                double tileHeight = mapHeight / tilesPerSide;

                // 遍历每一列和每一行的瓦片
                for (int x = 0; x < tilesPerSide*2; x++)
                {
                    for (int y = 0; y < tilesPerSide; y++)
                    {
                        // 更新进度条
                        processedTiles++;
                        progressBar.Value = processedTiles;
                        // 计算当前瓦片的地理范围
                        var tileEnvelope = new GeoAPI.Geometries.Envelope(
                            minX + x * tileWidth,
                            minX + (x + 1) * tileWidth,
                            maxY - (y + 1) * tileHeight, // 注意：y轴是倒过来的，因为Y坐标在地图中是反向的
                            maxY - y * tileHeight
                        );
                        // 预先判断瓦片区域是否包含数据
                        if (!HasDataInEnvelope(tileEnvelope))
                        {
                            continue; // 如果该瓦片没有数据，跳过渲染
                        }
                        // 创建一个新的地图对象用于渲染当前瓦片
                        var tileMap = new SharpMap.Map(new Size(tileSize, tileSize))
                        {
                            SRID = map.SRID, // 保持与主地图相同的坐标参考系
                            BackColor = map.BackColor
                        };

                        // 添加所有图层到当前瓦片的地图对象
                        foreach (var layer in map.Layers)
                        {
                            tileMap.Layers.Add(layer); // 使用Clone()确保每个瓦片独立
                        }

                        // 设置当前瓦片的显示范围
                        tileMap.ZoomToBox(tileEnvelope);

                        // 渲染地图到位图
                        using (var bitmap = new Bitmap(tileSize, tileSize))
                        {
                            using (var graphics = Graphics.FromImage(bitmap))
                            {
                                tileMap.RenderMap(graphics);
                            }
                           
                            // 构建瓦片的保存路径，按照缩放级别/列/行的结构
                            string tilePath = Path.Combine(outputDirectory, zoom.ToString(), x.ToString(), y.ToString() + ".png");
                            Directory.CreateDirectory(Path.GetDirectoryName(tilePath));

                            // 保存瓦片为PNG格式
                            bitmap.Save(tilePath, ImageFormat.Png);
                        }

                        
                    }
                }
            }

            // 切片完成提示
            MessageBox.Show("瓦片生成完成！");
        }
        // 判断图像是否为空白
        
        private bool HasDataInEnvelope(GeoAPI.Geometries.Envelope tileEnvelope)
        {
            // 你可以通过检查图层的有效数据范围来判断
            foreach (var layer in mapBox.Map.Layers)
            {
                // 如果图层的范围和当前瓦片重叠且包含数据，则说明该区域有数据
                if (layer.Envelope.Intersects(tileEnvelope))
                {
                    return true;
                }
            }
            return false;
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

            if (requestType.Equals("GetCapabilities", StringComparison.OrdinalIgnoreCase)|| requestType.Equals("GetCapabilities/rest/info", StringComparison.OrdinalIgnoreCase))
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
            try
            {
                // 获取并解析请求参数
                string layersParam = queryParams["LAYERS"];
                string stylesParam = queryParams["STYLES"];
                string crs = queryParams["CRS"];
                string bbox = queryParams["BBOX"];
                int width = int.Parse(queryParams["WIDTH"], CultureInfo.InvariantCulture);
                int height = int.Parse(queryParams["HEIGHT"], CultureInfo.InvariantCulture);
                string format = queryParams["FORMAT"];

                // 解析 BBOX 参数
                string[] bboxParts = bbox.Split(',');
                if (bboxParts.Length != 4)
                {
                    throw new ArgumentException("BBOX 参数格式不正确。");
                }

                double minX = double.Parse(bboxParts[0], CultureInfo.InvariantCulture);
                double minY = double.Parse(bboxParts[1], CultureInfo.InvariantCulture);
                double maxX = double.Parse(bboxParts[2], CultureInfo.InvariantCulture);
                double maxY = double.Parse(bboxParts[3], CultureInfo.InvariantCulture);

                // 创建地图对象，设置背景为透明
                var map = new SharpMap.Map(new Size(width, height))
                {
                    SRID = GetSridFromCrs(crs),
                    BackColor = Color.Transparent
                };

                // 添加请求的图层
                foreach (var layerName in layersParam.Split(','))
                {
                    var layer = mapBox.Map.Layers.FirstOrDefault(l => l.LayerName.Equals(layerName, StringComparison.OrdinalIgnoreCase));
                    if (layer != null)
                    {
                        map.Layers.Add(layer); // 使用 Clone() 确保每个请求独立
                    }
                    else
                    {
                        // 处理找不到的图层（可选）
                        // 例如，记录日志或返回错误信息
                    }
                }

                // 设置地图范围
                map.ZoomToBox(new Envelope(minX, maxX, minY, maxY));

                // 渲染地图
                using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(Color.Transparent); // 确保背景透明
                        map.RenderMap(graphics);
                    }

                    // 设置响应内容类型
                    ImageFormat imageFormat = GetImageFormat(format);
                    if (imageFormat == null)
                    {
                        throw new ArgumentException($"不支持的图像格式: {format}");
                    }

                    context.Response.ContentType = GetMimeType(imageFormat);

                    // 将图像保存到内存流
                    using (var ms = new MemoryStream())
                    {
                        bitmap.Save(ms, imageFormat);
                        ms.WriteTo(context.Response.OutputStream);
                    }
                }

                // 设置状态码为 200 OK
                context.Response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                // 处理异常，返回错误信息
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    writer.Write($"<ServiceExceptionReport version=\"1.3.0\" xmlns=\"http://www.opengis.net/ogc\"><ServiceException>{WebUtility.HtmlEncode(ex.Message)}</ServiceException></ServiceExceptionReport>");
                }
            }
            finally
            {
                // 关闭响应流
                context.Response.OutputStream.Close();
            }

        }
        private string GetMimeType(ImageFormat format)
        {
            if (format.Equals(ImageFormat.Png))
                return "image/png";
            if (format.Equals(ImageFormat.Jpeg))
                return "image/jpeg";
            if (format.Equals(ImageFormat.Gif))
                return "image/gif";
            if (format.Equals(ImageFormat.Bmp))
                return "image/bmp";
            if (format.Equals(ImageFormat.Tiff))
                return "image/tiff";
            return "application/octet-stream"; // 默认 MIME 类型
        }
        private int GetSridFromCrs(string crs)
        {
            // 解析 CRS 参数并返回相应的 SRID
            if (crs.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase))
            {
                return int.Parse(crs.Substring(5));
            }
            else if (crs.StartsWith("CRS:", StringComparison.OrdinalIgnoreCase))
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

        private void LogError(string message)
        {
            // 使用适合的日志框架，如 log4net, NLog 等
            // 这里简单地写入到文件
            File.AppendAllText("server_logs.txt", $"{DateTime.Now}: {message}{Environment.NewLine}");
        }
        private void ProcessWmtsRequest(HttpListenerContext context, string query, string[] parts)
        {
            var queryParams = HttpUtility.ParseQueryString(query);
            string requestType = queryParams["REQUEST"];

            if (requestType.Equals("GetCapabilities", StringComparison.OrdinalIgnoreCase) || requestType.Equals("GetCapabilities/rest/info", StringComparison.OrdinalIgnoreCase))
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
                string zoom = queryParams["TILEMATRIX"];
                string x = queryParams["TILECOL"];
                string y = queryParams["TILEROW"];
                
                zoom = zoom.Replace("{", "").Replace("}", "");
                x = x.Replace("{", "").Replace("}", "");
                y = y.Replace("{", "").Replace("}", "");

                //int x1 = int.Parse(x);
                //int y1=int.Parse(y);
                //y1 = (int)Math.Pow(2, int.Parse(zoom)) - 1 - y1;

                //int pad = 0;
                //if (int.Parse(zoom) <= 5) pad = 2;
                //else if (int.Parse(zoom) <= 11) pad = 4;
                //else pad = 6;

                //// 使用修正后的路径构建完整路径
                //string tilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "D:\\datasource\\beijing_gis_osm_landuse_a_free_1", $"EPSG_900913_{zoom}", $"{x1.ToString().PadLeft(pad, '0')}_{y1.ToString().PadLeft(pad, '0')}.png");
                string tilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WMTS_Tiles", $"{zoom}", $"{x}",$"{y}.png");
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
            string filePath = "D:\\datasource\\GISServer\\xml\\wms.xml";
            return File.ReadAllText(filePath);
        }
        private string cachedCapabilitiesXml = null;
        private DateTime cacheTimestamp = DateTime.MinValue;
        private readonly TimeSpan cacheDuration = TimeSpan.FromMinutes(10); // 根据需求调整
        private string GetWmtsCapabilities()
        {
            //if (cachedCapabilitiesXml != null && (DateTime.Now - cacheTimestamp) < cacheDuration)
            //{
                //return cachedCapabilitiesXml;
            //}
            // 返回 WMTS GetCapabilities 响应的 XML 字符串
            string filePath = "D:\\datasource\\GISServer\\xml\\wmts1.xml";
            // 生成Capabilities XML的逻辑
            string capabilitiesXml = File.ReadAllText(filePath);
            // 更新缓存
            //cachedCapabilitiesXml = capabilitiesXml;
            //cacheTimestamp = DateTime.Now;
            return capabilitiesXml;
        }
        public void GenerateWmsCapabilitiesXml(string outputFilePath)
        {

            // 定义命名空间
            XNamespace wms = "http://www.opengis.net/wms";
            XNamespace xlink = "http://www.w3.org/1999/xlink";
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            XNamespace ows = "http://www.opengis.net/ows/1.1";

            // 创建 XML 文档
            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(wms + "WMS_Capabilities",
                    new XAttribute(XNamespace.Xmlns + "xlink", xlink),
                    new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                    new XAttribute("version", "1.3.0"),
                    new XAttribute("updateSequence", "580"),
                    new XAttribute(xsi + "schemaLocation", $"{wms} http://localhost:8080/schemas/wms/1.3.0/capabilities_1_3_0.xsd"),

                    // ----- Service 部分 -----
                    new XElement(wms + "Service",
                        new XElement(wms + "Name", "WMS"),
                        new XElement(wms + "Title", "GeoServer Web Map Service"),
                        new XElement(wms + "Abstract", "A compliant implementation of WMS plus most of the SLD extension (dynamic styling). Can also generate PDF, SVG, KML, GeoRSS"),
                        new XElement(wms + "KeywordList",
                            new XElement(wms + "Keyword", "WMS"),
                            new XElement(wms + "Keyword", "GEOSERVER")
                        ),
                        new XElement(wms + "OnlineResource",
                            new XAttribute(xlink + "type", "simple"),
                            new XAttribute(xlink + "href", "http://geoserver.org")
                        ),
                        
                        new XElement(wms + "Fees", "NONE"),
                        new XElement(wms + "AccessConstraints", "NONE")
                    ),

                    // ----- Capability 部分 -----
                    new XElement(wms + "Capability",
                        // <Request>
                        new XElement(wms + "Request",
                            // ----- GetCapabilities Request -----
                            new XElement(wms + "GetCapabilities",
                                new XElement(wms + "Format", "text/xml"),
                                new XElement(wms + "DCPType",
                                    new XElement(wms + "HTTP",
                                        new XElement(wms + "Get",
                                            new XElement(wms + "OnlineResource",
                                                new XAttribute(xlink + "type", "simple"),
                                                new XAttribute(xlink + "href", "http://localhost:8080/wms?SERVICE=WMS")
                                            )
                                        ),
                                        new XElement(wms + "Post",
                                            new XElement(wms + "OnlineResource",
                                                new XAttribute(xlink + "type", "simple"),
                                                new XAttribute(xlink + "href", "http://localhost:8080/wms?SERVICE=WMS")
                                            )
                                        )
                                    )
                                )
                            ),

                            // ----- GetMap Request -----
                            new XElement(wms + "GetMap",
                                new XElement(wms + "Format", "image/png"),
                                new XElement(wms + "Format", "application/atom+xml"),
                                new XElement(wms + "Format", "application/json;type=utfgrid"),
                                new XElement(wms + "Format", "application/pdf"),
                                new XElement(wms + "Format", "application/rss+xml"),
                                new XElement(wms + "Format", "application/vnd.google-earth.kml+xml"),
                                new XElement(wms + "Format", "application/vnd.google-earth.kml+xml;mode=networklink"),
                                new XElement(wms + "Format", "application/vnd.google-earth.kmz"),
                                new XElement(wms + "Format", "image/geotiff"),
                                new XElement(wms + "Format", "image/geotiff8"),
                                new XElement(wms + "Format", "image/gif"),
                                new XElement(wms + "Format", "image/jpeg"),
                                new XElement(wms + "Format", "image/png; mode=8bit"),
                                new XElement(wms + "Format", "image/svg+xml"),
                                new XElement(wms + "Format", "image/tiff"),
                                new XElement(wms + "Format", "image/tiff8"),
                                new XElement(wms + "Format", "image/vnd.jpeg-png"),
                                new XElement(wms + "Format", "image/vnd.jpeg-png8"),
                                new XElement(wms + "Format", "text/html; subtype=openlayers"),
                                new XElement(wms + "Format", "text/html; subtype=openlayers2"),
                                new XElement(wms + "Format", "text/html; subtype=openlayers3"),
                                new XElement(wms + "DCPType",
                                    new XElement(wms + "HTTP",
                                        new XElement(wms + "Get",
                                            new XElement(wms + "OnlineResource",
                                                new XAttribute(xlink + "type", "simple"),
                                                new XAttribute(xlink + "href", "http://localhost:8080/wms?SERVICE=WMS")
                                            )
                                        )
                                    )
                                )
                            ),

                            // ----- GetFeatureInfo Request -----
                            new XElement(wms + "GetFeatureInfo",
                                new XElement(wms + "Format", "text/plain"),
                                new XElement(wms + "Format", "application/vnd.ogc.gml"),
                                new XElement(wms + "Format", "text/xml"),
                                new XElement(wms + "Format", "application/vnd.ogc.gml/3.1.1"),
                                new XElement(wms + "Format", "text/xml; subtype=gml/3.1.1"),
                                new XElement(wms + "Format", "text/html"),
                                new XElement(wms + "Format", "application/json"),
                                new XElement(wms + "DCPType",
                                    new XElement(wms + "HTTP",
                                        new XElement(wms + "Get",
                                            new XElement(wms + "OnlineResource",
                                                new XAttribute(xlink + "type", "simple"),
                                                new XAttribute(xlink + "href", "http://localhost:8080/wms?SERVICE=WMS")
                                            )
                                        )
                                    )
                                )
                            )
                        ),

                        // <Exception>
                        new XElement(wms + "Exception",
                            new XElement(wms + "Format", "XML"),
                            new XElement(wms + "Format", "INIMAGE"),
                            new XElement(wms + "Format", "BLANK"),
                            new XElement(wms + "Format", "JSON")
                        ),

                        // <Layer>
                        new XElement(wms + "Layer",
                            new XElement(wms + "Title", "GeoServer Web Map Service"),
                            new XElement(wms + "Abstract", "A compliant implementation of WMS plus most of the SLD extension (dynamic styling). Can also generate PDF, SVG, KML, GeoRSS"),

                            // <CRS> 元素
                            new XElement(wms + "CRS", "EPSG:2000"),
                            new XElement(wms + "CRS", "EPSG:2001"),
                            new XElement(wms + "CRS", "EPSG:2002"),
                            new XElement(wms + "CRS", "EPSG:2003"),
                            new XElement(wms + "CRS", "EPSG:2004"),
                            new XElement(wms + "CRS", "EPSG:2005"),
                            new XElement(wms + "CRS", "EPSG:2006"),
                            new XElement(wms + "CRS", "EPSG:2007"),
                            new XElement(wms + "CRS", "EPSG:2008"),
                            new XElement(wms + "CRS", "EPSG:2009"),
                            new XElement(wms + "CRS", "EPSG:2010"),
                            new XElement(wms + "CRS", "EPSG:2011"),
                            new XElement(wms + "CRS", "EPSG:2012"),
                            new XElement(wms + "CRS", "EPSG:2013"),
                            new XElement(wms + "CRS", "EPSG:2014"),
                            new XElement(wms + "CRS", "EPSG:2015"),
                            new XElement(wms + "CRS", "EPSG:2016"),
                            new XElement(wms + "CRS", "EPSG:4326"),
                            new XElement(wms + "CRS", "CRS:84"),

                            // <EX_GeographicBoundingBox>
                            new XElement(ows + "EX_GeographicBoundingBox",
                                new XElement(ows + "westBoundLongitude", "-180.0"),
                                new XElement(ows + "eastBoundLongitude", "180.0"),
                                new XElement(ows + "southBoundLatitude", "-90.0"),
                                new XElement(ows + "northBoundLatitude", "90.0")
                            ),

                            // <BoundingBox CRS="CRS:84" minx="-180.0" miny="-90.0" maxx="180.0" maxy="90.0"/>
                            new XElement(wms + "BoundingBox",
                                new XAttribute("CRS", "CRS:84"),
                                new XAttribute("minx", "-180.0"),
                                new XAttribute("miny", "-90.0"),
                                new XAttribute("maxx", "180.0"),
                                new XAttribute("maxy", "90.0")
                            ),

                            // ----- Sub-layer (北京市界) -----
                            mapBox.Map.Layers.Select(layer =>
        new XElement(wms + "Layer",
            new XAttribute("queryable", "1"),
            new XAttribute("opaque", "0"),
            new XElement(wms + "Name", layer.LayerName),
            new XElement(wms + "Title", layer.LayerName),
            new XElement(wms + "Abstract"),
            new XElement(wms + "KeywordList",
                new XElement(wms + "Keyword", "features"),
                new XElement(wms + "Keyword", layer.LayerName)
            ),
            new XElement(wms + "CRS", "EPSG:4326"),
            new XElement(wms + "CRS", "CRS:84"),
            new XElement(ows + "EX_GeographicBoundingBox",
                new XElement(ows + "westBoundLongitude", layer.Envelope.MinX),
                new XElement(ows + "eastBoundLongitude", layer.Envelope.MaxX),
                new XElement(ows + "southBoundLatitude", layer.Envelope.MinY),
                new XElement(ows + "northBoundLatitude", layer.Envelope.MaxY)
            ),
            new XElement(wms + "BoundingBox",
                new XAttribute("CRS", "CRS:84"),
                new XAttribute("minx", layer.Envelope.MinX),
                new XAttribute("miny", layer.Envelope.MinY),
                new XAttribute("maxx", layer.Envelope.MaxX),
                new XAttribute("maxy", layer.Envelope.MaxY)
            ),
            new XElement(wms + "BoundingBox",
                new XAttribute("CRS", "EPSG:4326"),
                new XAttribute("minx", layer.Envelope.MinY),
                new XAttribute("miny", layer.Envelope.MinX),
                new XAttribute("maxx", layer.Envelope.MaxY),
                new XAttribute("maxy", layer.Envelope.MaxX)
            ),
            new XElement(wms + "Style",
                new XElement(wms + "Name", "polygon"),
                new XElement(wms + "Title", "Default Polygon"),
                new XElement(wms + "Abstract", "A sample style that draws a polygon"),
                new XElement(wms + "LegendURL",
                    new XAttribute("width", "20"),
                    new XAttribute("height", "20"),
                    new XElement(wms + "Format", "image/png"),
                    new XElement(wms + "OnlineResource",
                        new XAttribute(xlink + "type", "simple"),
                        new XAttribute(xlink + "href", $"http://yourdomain.com/wms?service=WMS&version=1.3.0&request=GetLegendGraphic&format=image/png&width=20&height=20&layer={Uri.EscapeDataString(layer.LayerName)}")
                    )
                )
            )
        )
        )
                        )
                    )
                )
            );         
            // 保存 XML 文档
            doc.Save(outputFilePath);
        }

        public void GenerateWmtsCapabilitiesXml(string outputFilePath, string supportedCrs = "EPSG:4326")
        {
            // 定义命名空间作为类成员，便于在多个方法中使用
            XNamespace wmts = "http://www.opengis.net/wmts/1.0";
            XNamespace ows = "http://www.opengis.net/ows/1.1";
            XNamespace xlink = "http://www.w3.org/1999/xlink";
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            XNamespace gml = "http://www.opengis.net/gml";

            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(wmts + "Capabilities",
                    new XAttribute(XNamespace.Xmlns + "ows", ows),
                    new XAttribute(XNamespace.Xmlns + "xlink", xlink),
                    new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                    new XAttribute(XNamespace.Xmlns + "gml", gml),
                    new XAttribute(xsi + "schemaLocation", "http://www.opengis.net/wmts/1.0 http://schemas.opengis.net/wmts/1.0/wmtsGetCapabilities_response.xsd"),
                    new XAttribute("version", "1.0.0"),

                    // ----- ServiceIdentification ----- 
                    new XElement(ows + "ServiceIdentification",
                        new XElement(ows + "Title", "GeoServer Web Map Tile Service"),
                        new XElement(ows + "Abstract", "A compliant implementation of WMTS service."),
                        new XElement(ows + "Keywords",
                            new XElement(ows + "Keyword", "WMTS")
                        ),
                        new XElement(ows + "ServiceType", "OGC WMTS"),
                        new XElement(ows + "ServiceTypeVersion", "1.0.0"),
                        new XElement(ows + "Fees", "NONE"),
                        new XElement(ows + "AccessConstraints", "NONE")
                    ),

                    // ----- ServiceProvider ----- 
                    new XElement(ows + "ServiceProvider",
                        new XElement(ows + "ProviderName", "http://geoserver.org/com"),
                        new XElement(ows + "ProviderSite",
                            
                            new XAttribute(xlink + "href", "http://geoserver.org")
                        ),
                        new XElement(ows + "ServiceContact",
                            new XElement(ows + "IndividualName", "Claudius Ptolomaeus"),
                            new XElement(ows + "PositionName", "Chief Geographer"),
                            new XElement(ows + "ContactInfo",
                                new XElement(ows + "Address",
                                    new XElement(ows + "City", "Alexandria"),
                                    new XElement(ows + "Country", "Roman Empire"),
                                    new XElement(ows + "ElectronicMailAddress", "claudius.ptolomaeus@mercury.olympus.gov")
                                )
                            )
                        )
                    ),

                    // ----- OperationsMetadata ----- 
                    new XElement(ows + "OperationsMetadata",
                        // GetCapabilities Operation
                        new XElement(ows + "Operation",
                            new XAttribute("name", "GetCapabilities"),
                            new XElement(ows + "DCP",
                                new XElement(ows + "HTTP",
                                    new XElement(ows + "Get",
                                        
                                        new XAttribute(xlink + "href", "http://localhost:8080/wmts?SERVICE=WMTS&"),
                                        new XElement(ows + "Constraint", new XAttribute("name", "GetEncoding"),
                                            new XElement(ows + "AllowedValues",
                                                new XElement(ows + "Value", "KVP")
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        // GetTile Operation
                        new XElement(ows + "Operation",
                            new XAttribute("name", "GetTile"),
                            new XElement(ows + "DCP",
                                new XElement(ows + "HTTP",
                                    new XElement(ows + "Get",
                                        
                                        new XAttribute(xlink + "href", "http://localhost:8080/wmts?SERVICE=WMTS&"),
                                        new XElement(ows + "Constraint", new XAttribute("name", "GetEncoding"),
                                            new XElement(ows + "AllowedValues",
                                                new XElement(ows + "Value", "KVP")
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    ),

                    // ----- Contents ----- 
                    new XElement(wmts + "Contents",
                        // Dynamically add Layer information
                        mapBox.Map.Layers.Select(layer =>
                        {
                            if (layer?.LayerName == null)
                            {
                                LogError("图层名称为空或无效。");
                                return null; // 或者跳过该图层
                            }

                            return new XElement(wmts + "Layer",
                                new XElement(ows + "Title", layer.LayerName), // 如 "水体"
                                new XElement(ows + "WGS84BoundingBox",
                                    new XElement(ows + "LowerCorner", $"{layer.Envelope.MinX} {layer.Envelope.MinY}"),
                                    new XElement(ows + "UpperCorner", $"{layer.Envelope.MaxX} {layer.Envelope.MaxY}")
                                ),
                                new XElement(ows + "Identifier", layer.LayerName), // 如 "BJ:水体"

                                // Style Definition
                                new XElement(wmts + "Style",
                                    new XAttribute("isDefault", "true"),
                                    new XElement(ows + "Identifier", "default") // 使用统一的样式名称
                                ),

                                // Supported Formats
                                new XElement(wmts + "Format", "image/png"),
                                new XElement(wmts + "Format", "image/jpeg"),

                                // TileMatrixSetLink and TileMatrixSetLimits
                                new XElement(wmts + "TileMatrixSetLink",
                                    new XElement(wmts + "TileMatrixSet", supportedCrs),
                                    new XElement(wmts + "TileMatrixSetLimits",
                                        GenerateTileMatrixLimitsXml(supportedCrs)
                                    )
                                ),

                                // ResourceURL Definitions
                                new XElement(wmts + "ResourceURL", new XAttribute("format", "image/png"), new XAttribute("resourceType", "tile"),
                                     new XAttribute("template", $"http://localhost:8080/wmts?request=GetTile&style={{style}}&TileMatrixSet={{TileMatrixSet}}&TileMatrix={{TileMatrix}}&TileRow={{TileRow}}&TileCol={{TileCol}}&format=image/png")
                                ),
                                new XElement(wmts + "ResourceURL", new XAttribute("format", "image/jpeg"), new XAttribute("resourceType", "tile"),
                                     new XAttribute("template", $"http://localhost:8080/wmts?request=GetTile&style={{style}}&TileMatrixSet={{TileMatrixSet}}&TileMatrix={{TileMatrix}}&TileRow={{TileRow}}&TileCol={{TileCol}}&format=image/jpeg")
                                )
                            );
                        }).Where(layer => layer != null),// 过滤掉可能的 null 图层
                                                         // ----- TileMatrixSet ----- 
                    new XElement(wmts + "TileMatrixSet",
                        new XElement(ows + "Identifier", supportedCrs),
                        new XElement(ows + "SupportedCRS", $"urn:ogc:def:crs:EPSG::4326"),

                       // Define TileMatrix for each zoom level
                       Enumerable.Range(0, 22).Select(z => new XElement(wmts + "TileMatrix",
                new XElement(ows + "Identifier", $"{z}"),
                new XElement(wmts + "ScaleDenominator", GetScaleDenominator(z)),
                new XElement(wmts + "TopLeftCorner", "90.0 -180.0"),
                new XElement(wmts + "TileWidth", 256),
                new XElement(wmts + "TileHeight", 256),
                new XElement(wmts + "MatrixWidth", GetMatrixWidth(z)),
                new XElement(wmts + "MatrixHeight", GetMatrixHeight(z))
                            )
                        )
                    )
                    ),

                    

                    // ----- ServiceMetadataURL ----- 
                    new XElement(wmts + "ServiceMetadataURL",
                        new XAttribute(xlink + "type", "simple"),
                        new XAttribute(xlink + "href", "http://localhost:8080/wmts?SERVICE=WMTS&REQUEST=GetCapabilities&VERSION=1.0.0")
                    ),
                    new XElement(wmts + "ServiceMetadataURL",
                        new XAttribute(xlink + "type", "simple"),
                        new XAttribute(xlink + "href", "http://localhost:8080/wmts?file=wmts.xml")
                    )
                )
            );

            // 保存 XML 文档到指定路径，确保使用 UTF-8 编码
            try
            {
                using (var writer = new StreamWriter(outputFilePath, false, Encoding.UTF8))
                {
                    doc.Save(writer);
                }
            }
            catch (Exception ex)
            {
                LogError($"生成 WMTS Capabilities XML 失败：{ex.Message}\n{ex.StackTrace}");
            }
        }

        private readonly string baseDirectory = "WMTS_tiles"; // 设置文件夹路径

        public IEnumerable<XElement> GenerateTileMatrixLimitsXml(string supportedCrs)
        {
            XNamespace wmts = "http://www.opengis.net/wmts/1.0";
            int maxZoomLevel = 10; // 假设最大缩放级别为5

            return Enumerable.Range(0, maxZoomLevel + 1).Select(z =>
            {
                int tilesPerSide = (int)Math.Pow(2, z);
                var zoomDirectory = Path.Combine(baseDirectory, $"{z}");

                // 获取该缩放级别文件夹中的所有子文件夹
                var subDirectories = Directory.GetDirectories(zoomDirectory);

                // 初始化行列的最小值和最大值
                int minTileCol = int.MaxValue;
                int maxTileCol = int.MinValue;
                int minTileRow = int.MaxValue;
                int maxTileRow = int.MinValue;

                // 遍历每个子文件夹（对应列）
                foreach (var subDir in subDirectories)
                {
                    // 假设文件名格式为：`X_Y.png` 或类似的格式
                    var subDirName = Path.GetFileNameWithoutExtension(subDir);
                    int x = int.Parse(subDirName); 
                    minTileCol = Math.Min(minTileCol, x);
                    maxTileCol = Math.Max(maxTileCol, x);
                        
                    


                    var files = Directory.GetFiles(subDir);
                    // 遍历每个文件（对应行）
                    foreach (var file in files)
                    {
                        // 假设文件名格式为：`X_Y.png` 或类似的格式
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        int y = int.Parse(fileName);
                        minTileRow = Math.Min(minTileRow, y);
                        maxTileRow = Math.Max(maxTileRow, y);

                    }
                }

                // 生成TileMatrixLimits的XML元素
                return new XElement(wmts + "TileMatrixLimits",
                    new XElement(wmts + "TileMatrix", $"{z}"),
                    new XElement(wmts + "MinTileRow", minTileRow.ToString()),
                    new XElement(wmts + "MaxTileRow", maxTileRow.ToString()),
                    new XElement(wmts + "MinTileCol", minTileCol.ToString()),
                    new XElement(wmts + "MaxTileCol", maxTileCol.ToString())
                );
            });
        }
        // 获取ScaleDenominator
        private double GetScaleDenominator(int zoomLevel)
        {
            double[] scaleDenominators = new double[]
            {
            2.795411320143589E8, 1.3977056600717944E8, 6.988528300358972E7, 3.494264150179486E7,
            1.747132075089743E7, 8735660.375448715, 4367830.1877243575, 2183915.0938621787,
            1091957.5469310894, 545978.7734655447, 272989.38673277234, 136494.69336638617,
            68247.34668319309, 34123.67334159654, 17061.83667079827, 8530.918335399136,
            4265.459167699568, 2132.729583849784, 1066.364791924892, 533.182395962446,
            266.591197981223, 133.2955989906115
            };

            return scaleDenominators[zoomLevel];
        }

        // 获取MatrixWidth
        private int GetMatrixWidth(int zoomLevel)
        {
            int[] matrixWidths = new int[]
            {
            2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536,
            131072, 262144, 524288, 1048576, 2097152, 4194304
            };

            return matrixWidths[zoomLevel];
        }

        // 获取MatrixHeight
        private int GetMatrixHeight(int zoomLevel)
        {
            int[] matrixHeights = new int[]
            {
            1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768,
            65536, 131072, 262144, 524288, 1048576, 2097152
            };

            return matrixHeights[zoomLevel];
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            httpListener.Stop();
            listenerThread.Abort();
            base.OnFormClosing(e);
        }
    }
}
