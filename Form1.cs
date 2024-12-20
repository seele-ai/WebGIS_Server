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
                        new XElement(wms + "ContactInformation",
                            new XElement(wms + "ContactPersonPrimary",
                                new XElement(wms + "ContactPerson", "Claudius Ptolomaeus"),
                                new XElement(wms + "ContactOrganization", "OSGeo")
                            ),
                            new XElement(wms + "ContactPosition", "Chief Geographer"),
                            new XElement(wms + "ContactAddress",
                                new XElement(wms + "AddressType", "Work"),
                                new XElement(wms + "Address", ""), // 空地址
                                new XElement(wms + "City", "Alexandria"),
                                new XElement(wms + "StateOrProvince", "Egypt"),
                                new XElement(wms + "PostCode", ""), // 空邮编
                                new XElement(wms + "Country", "Roman Empire")
                            ),
                            new XElement(wms + "ContactVoiceTelephone"),
                            new XElement(wms + "ContactFacsimileTelephone"),
                            new XElement(wms + "ContactElectronicMailAddress", "claudius.ptolomaeus@mercury.olympus.gov")
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
                new XElement(ows + "westBoundLongitude", "115.417284"),
                new XElement(ows + "eastBoundLongitude", "117.500126"),
                new XElement(ows + "southBoundLatitude", "39.438283"),
                new XElement(ows + "northBoundLatitude", "41.059244")
            ),
            new XElement(wms + "BoundingBox",
                new XAttribute("CRS", "CRS:84"),
                new XAttribute("minx", "115.417284"),
                new XAttribute("miny", "39.438283"),
                new XAttribute("maxx", "117.500126"),
                new XAttribute("maxy", "41.059244")
            ),
            new XElement(wms + "BoundingBox",
                new XAttribute("CRS", "EPSG:4326"),
                new XAttribute("minx", "39.438283"),
                new XAttribute("miny", "115.417284"),
                new XAttribute("maxx", "41.059244"),
                new XAttribute("maxy", "117.500126")
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
     
        public void GenerateWmtsCapabilitiesXml(string outputFilePath)
        {
           
        


        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            httpListener.Stop();
            listenerThread.Abort();
            base.OnFormClosing(e);
        }
    }
}
