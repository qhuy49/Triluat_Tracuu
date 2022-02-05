using System.Web;
using System.Threading.Tasks;
using System.Data;
using System.Drawing;
using Search_Invoice.Data.Domain;
using System.Text;
using System.IO;
using HtmlAgilityPack;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraPrinting;
using Search_Invoice.Util;
using DevExpress.XtraReports.UI;
using System.Globalization;
using System.Xml;
using System.Drawing.Imaging;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections.Generic;
using DevExpress.XtraReports.Parameters;
using System.Threading;
using Search_Invoice.Data;

namespace Search_Invoice.Services
{
    public class TracuuService2 : ITracuuService2
    {
        private INopDbContext2 _nopDbContext2;
        private ICacheManager _cacheManager;
        private IWebHelper _webHelper;

        public TracuuService2(
                              INopDbContext2 nopDbContext2,
                              ICacheManager cacheManager,
                              IWebHelper webHelper
          )
        {
            this._nopDbContext2 = nopDbContext2;
            this._cacheManager = cacheManager;
            this._webHelper = webHelper;
        }
        public JObject GetInvoiceFromdateTodate(JObject model)
        {
            JObject json = new JObject();
            try
            {
                string mst = model["masothue"].ToString().Replace("-", "");

                DateTime tu_ngay = (DateTime)model["tu_ngay"];
                DateTime den_ngay = (DateTime)model["den_ngay"];
                string ma_dt = model["ma_dt"].ToString();
                _nopDbContext2.setConnect(mst);
                DataTable dt = this._nopDbContext2.ExecuteCmd("SELECT * FROM inv_InvoiceAuth WHERE inv_invoiceIssuedDate >= '" + tu_ngay + "' and inv_invoiceIssuedDate <= '" + den_ngay + "' AND ma_dt ='" + ma_dt + "'");
                //dt.Columns.Add("mst", typeof(string));

                //foreach (DataRow row in dt.Rows)
                //{
                //    row.BeginEdit();
                //    row["mst"] = mst;
                //    row.EndEdit();
                //}
                if (dt.Rows.Count > 0)
                {
                    JArray jar = JArray.FromObject(dt);
                    json.Add("data", jar);
                }
                else
                {
                    json.Add("error", "Không tìm thấy dữ liệu.");
                    return json;
                }
            }
            catch (Exception ex)
            {
                json.Add("error", ex.Message);
            }
            return json;
        }

        //tìm kiếm hóa đơn new version
        public JObject GetInfoInvoice(JObject model)
        {
            JObject json = new JObject();
            try
            {
                string mst = model["masothue"].ToString().Replace("-", "");
              
                string sobaomat = model["sobaomat"].ToString();
                _nopDbContext2.setConnect(mst);
                DataTable dt = this._nopDbContext2.ExecuteCmd("SELECT * FROM inv_InvoiceAuth WHERE sobaomat ='" + sobaomat + "'");
              //  dt.Columns.Add("mst", typeof(string));

                //foreach (DataRow row in dt.Rows)
                //{
                //    row.BeginEdit();
                //    row["mst"] = mst;
                //    row.EndEdit();
                //}
                if (dt.Rows.Count > 0)
                {
                    // tìm kiếm 32
                    dt.Columns.Add("masothue", typeof(string));
                    dt.Columns.Add("auth_id", typeof(string));
                    dt.Columns.Add("sum_tien", typeof(double));
                    dt.Columns.Add("ecd", typeof(string));

                    string connectionString = Util.EncodeXML.Encrypt(_nopDbContext2.GetInvoiceDb().Database.Connection.ConnectionString, "NAMPV18081202");
                    byte[] bytes = Encoding.UTF8.GetBytes(connectionString);
                    string b = Convert.ToBase64String(bytes);
                    foreach (DataRow row in dt.Rows)
                    {
                        row.BeginEdit();
                        row["masothue"] = model["masothue"].ToString();
                        row["auth_id"] = b;
                        row["sum_tien"] = dt.Rows[0]["inv_TotalAmount"];
                        //row["ecd"] = xml;
                        row.EndEdit();
                    }

                    JArray jar = JArray.FromObject(dt);
                    json.Add("data", jar);
                }
                else
                {
                    //tìm kiếm 78
                    json =   GetInfoInvoice78(model);
                    return json;
                    
                    //json.Add("error", "Không tồn tại hóa đơn có số bảo mật: " + sobaomat);
                    //return json;
                }
            }
            catch (Exception ex)
            {
                json.Add("error", ex.Message);
            }
            return json;
        }
        public byte[] PrintInvoiceFromSBM(string sobaomat,string masothue, string folder, string type)
        {
            byte[] results = PrintInvoiceFromSBM(sobaomat,masothue, folder, type, false);
            return results;
        }

        public byte[] PrintInvoiceFromSBM(string sobaomat,string masothue, string folder, string type, bool inchuyendoi)
        {
            _nopDbContext2.setConnect(masothue);
            var db = this._nopDbContext2.GetInvoiceDb();

            byte[] bytes = null;

            string xml = "";
            string msg_tb = "";

            try
            {
                // Guid inv_InvoiceAuth_id = Guid.Parse(id);

                DataTable tblInv_InvoiceAuth = this._nopDbContext2.ExecuteCmd("SELECT * FROM inv_InvoiceAuth WHERE sobaomat='" + sobaomat + "'");
                if (tblInv_InvoiceAuth.Rows.Count == 0)
                {
                    throw new Exception("Không tồn tại hóa đơn có số bảo mật " + sobaomat);
                }
                string inv_InvoiceAuth_id = tblInv_InvoiceAuth.Rows[0]["inv_InvoiceAuth_id"].ToString();
                DataTable tblInv_InvoiceAuthDetail = this._nopDbContext2.ExecuteCmd("SELECT * FROM inv_InvoiceAuthDetail WHERE inv_InvoiceAuth_id = '" + inv_InvoiceAuth_id + "'");
                DataTable tblInvoiceXmlData = this._nopDbContext2.ExecuteCmd("SELECT * FROM InvoiceXmlData WHERE inv_InvoiceAuth_id='" + inv_InvoiceAuth_id + "'");

                //if (masothue == "2700638514" && tblInv_InvoiceAuthDetail.Rows.Count > 9)
                //{
                //    xml = db.Database.SqlQuery<string>("EXECUTE sproc_export_XmlInvoice_BK '" + inv_InvoiceAuth_id + "'").FirstOrDefault<string>();
                //}
                //else
                //{
                    if (tblInvoiceXmlData.Rows.Count > 0)
                    {
                        xml = tblInvoiceXmlData.Rows[0]["data"].ToString();
                    }
                    else
                    {
                        xml = db.Database.SqlQuery<string>("EXECUTE sproc_export_XmlInvoice '" + inv_InvoiceAuth_id + "'").FirstOrDefault<string>();
                    }
                //}
                var invoiceDb = this._nopDbContext2.GetInvoiceDb();
                string inv_InvoiceCode_id = tblInv_InvoiceAuth.Rows[0]["inv_InvoiceCode_id"].ToString();
                int trang_thai_hd = Convert.ToInt32(tblInv_InvoiceAuth.Rows[0]["trang_thai_hd"]);
                string inv_originalId = tblInv_InvoiceAuth.Rows[0]["inv_originalId"].ToString();
                string user_name = _webHelper.GetUser();
                // wb_user wbuser = invoiceDb.WbUsers.Where(c => c.username == user_name).FirstOrDefault<wb_user>();
                DataTable tblCtthongbao = this._nopDbContext2.ExecuteCmd("SELECT * FROM ctthongbao a INNER JOIN dpthongbao b ON a.dpthongbao_id=b.dpthongbao_id WHERE a.ctthongbao_id='" + inv_InvoiceCode_id + "'");
                string hang_nghin = ".";
                string thap_phan = ",";
                DataColumnCollection columns = tblCtthongbao.Columns;
                if (columns.Contains("hang_nghin"))
                {
                    hang_nghin = tblCtthongbao.Rows[0]["hang_nghin"].ToString();
                }
                if (columns.Contains("thap_phan"))
                {
                    thap_phan = tblCtthongbao.Rows[0]["thap_phan"].ToString();
                }
                if (hang_nghin == null || hang_nghin == "")
                {
                    hang_nghin = ".";
                }
                if (thap_phan == "" || thap_phan == null)
                {
                    thap_phan = ",";
                }
                //string hang_nghin = tblCtthongbao.Rows[0]["hang_nghin"].ToString();
                //string thap_phan = tblCtthongbao.Rows[0]["thap_phan"].ToString();

                string cacheReportKey = string.Format(CachePattern.INVOICE_REPORT_PATTERN_KEY + "{0}", tblCtthongbao.Rows[0]["dmmauhoadon_id"]);

                //XtraReport report = _cacheManager.Get<XtraReport>(cacheReportKey);
                XtraReport report = new XtraReport();
                report = null;

                if (report == null)
                {

                    DataTable tblDmmauhd = this._nopDbContext2.ExecuteCmd("SELECT * FROM dmmauhoadon WHERE dmmauhoadon_id='" + tblCtthongbao.Rows[0]["dmmauhoadon_id"].ToString() + "'");
                    string invReport = tblDmmauhd.Rows[0]["report"].ToString();

                    if (invReport.Length > 0)
                    {
                        report = ReportUtil.LoadReportFromString(invReport);
                        _cacheManager.Set(cacheReportKey, report, 30);
                    }
                    else
                    {
                        throw new Exception("Không tải được mẫu hóa đơn");
                    }

                }

                report.Name = "XtraReport1";
                report.ScriptReferencesString = "AccountSignature.dll";

                DataSet ds = new DataSet();

                using (XmlReader xmlReader = XmlReader.Create(new StringReader(report.DataSourceSchema)))
                {
                    ds.ReadXmlSchema(xmlReader);
                    xmlReader.Close();
                }

                using (XmlReader xmlReader = XmlReader.Create(new StringReader(xml)))
                {
                    ds.ReadXml(xmlReader);
                    xmlReader.Close();
                }

                if (ds.Tables.Contains("TblXmlData"))
                {
                    ds.Tables.Remove("TblXmlData");
                }

                DataTable tblXmlData = new DataTable();
                tblXmlData.TableName = "TblXmlData";
                tblXmlData.Columns.Add("data");

                DataRow r = tblXmlData.NewRow();
                r["data"] = xml;
                tblXmlData.Rows.Add(r);
                ds.Tables.Add(tblXmlData);

                string datamember = report.DataMember;

                if (datamember.Length > 0)
                {
                    if (ds.Tables.Contains(datamember))
                    {
                        DataTable tblChiTiet = ds.Tables[datamember];

                        int rowcount = ds.Tables[datamember].Rows.Count;


                    }
                }

                if (trang_thai_hd == 11 || trang_thai_hd == 13 || trang_thai_hd == 17)
                {
                    if (!string.IsNullOrEmpty(inv_originalId))
                    {
                        DataTable tblInv = this._nopDbContext2.ExecuteCmd("SELECT * FROM inv_InvoiceAuth WHERE inv_InvoiceAuth_id='" + inv_originalId + "'");
                        string inv_adjustmentType = tblInv.Rows[0]["inv_adjustmentType"].ToString();

                        string loai = inv_adjustmentType.ToString() == "5" || inv_adjustmentType.ToString() == "19" || inv_adjustmentType.ToString() == "21" ? "điều chỉnh" : inv_adjustmentType.ToString() == "3" ? "thay thế" : inv_adjustmentType.ToString() == "7" ? "xóa bỏ" : "";

                        if (inv_adjustmentType.ToString() == "5" || inv_adjustmentType.ToString() == "7" || inv_adjustmentType.ToString() == "3" || inv_adjustmentType.ToString() == "19" || inv_adjustmentType.ToString() == "21")
                        {
                            msg_tb = "Hóa đơn bị " + loai + " bởi hóa đơn số: " + tblInv.Rows[0]["inv_invoiceNumber"] + " ngày " + string.Format("{0:dd/MM/yyyy}", tblInv.Rows[0]["inv_invoiceIssuedDate"]) + ", mẫu số " + tblInv.Rows[0]["mau_hd"] + " ký hiệu " + tblInv.Rows[0]["inv_invoiceSeries"];

                        }
                    }
                }

                if (Convert.ToInt32(tblInv_InvoiceAuth.Rows[0]["inv_adjustmentType"]) == 7)
                {
                    msg_tb = "";
                }

                if (report.Parameters["MSG_TB"] != null)
                {
                    report.Parameters["MSG_TB"].Value = msg_tb;
                }

                var lblHoaDonMau = report.AllControls<XRLabel>().Where(c => c.Name == "lblHoaDonMau").FirstOrDefault<XRLabel>();

                if (lblHoaDonMau != null)
                {
                    lblHoaDonMau.Visible = false;
                }

                if (inchuyendoi)
                {
                    var tblInChuyenDoi = report.AllControls<XRTable>().Where(c => c.Name == "tblInChuyenDoi").FirstOrDefault<XRTable>();

                    if (tblInChuyenDoi != null)
                    {
                        tblInChuyenDoi.Visible = true;
                    }

                    if (report.Parameters["MSG_HD_TITLE"] != null)
                    {
                        report.Parameters["MSG_HD_TITLE"].Value = "Hóa đơn chuyển đổi từ hóa đơn điện tử";
                    }

                    if (report.Parameters["NGUOI_IN_CDOI"] != null)
                    {
                        report.Parameters["NGUOI_IN_CDOI"].Value = "";
                        report.Parameters["NGUOI_IN_CDOI"].Visible = true;
                    }

                    if (report.Parameters["NGAY_IN_CDOI"] != null)
                    {
                        report.Parameters["NGAY_IN_CDOI"].Value = DateTime.Now;
                        report.Parameters["NGAY_IN_CDOI"].Visible = true;
                    }
                }
                report.Parameters.Add(new Parameter
                {
                    Name = "FM_inv_quantity",
                    Value = "#,#0.#######"
                });

                report.Parameters.Add(new Parameter
                {
                    Name = "FM_inv_unitPrice",
                    Value = "#,#0.#######"
                });

                report.Parameters.Add(new Parameter
                {
                    Name = "FM_inv_TotalAmountWithoutVat",
                    Value = "#,#0.#######"
                });



                report.Parameters.Add(new Parameter
                {
                    Name = "FM_inv_TotalAmount",
                    Value = "#,#0.#######"
                });

                report.DataSource = ds;

                var t = Task.Run(() =>
                {
                    var newCulture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
                    newCulture.NumberFormat.NumberDecimalSeparator = thap_phan;
                    newCulture.NumberFormat.NumberGroupSeparator = hang_nghin;

                    System.Threading.Thread.CurrentThread.CurrentCulture = newCulture;
                    System.Threading.Thread.CurrentThread.CurrentUICulture = newCulture;

                    report.CreateDocument();

                });

                t.Wait();

                //DataTable tblLicenseInfo = this._nopDbContext.ExecuteCmd("SELECT * FROM LicenseInfo WHERE ma_dvcs=N'" + tblInv_InvoiceAuth.Rows[0]["ma_dvcs"] + "' AND key_license IS NOT NULL AND LicenseXmlInfo IS NOT NULL");
                //if (tblLicenseInfo.Rows.Count == 0)
                //{
                //    Bitmap bmp = ReportUtil.DrawStringDemo(report);
                //    int pageCount = report.Pages.Count;

                //    for (int i = 0; i < pageCount; i++)
                //    {
                //        PageWatermark pmk = new PageWatermark();
                //        pmk.Image = bmp;
                //        report.Pages[i].AssignWatermark(pmk);
                //    }
                //}
                //if (masothue == "2700638514")
                //{
                //    if (tblInv_InvoiceAuthDetail.Rows.Count > 9)
                //    {
                //        if (tblInv_InvoiceAuth.Columns.Contains("inv_currencyCode"))
                //        {
                //            if (tblInv_InvoiceAuth.Rows[0]["inv_currencyCode"].ToString().Length > 0)
                //            {
                //                string currencyCode = tblInv_InvoiceAuth.Rows[0]["inv_currencyCode"].ToString();

                //                string fileName = currencyCode == "VND" ? folder + "\\INHD_BK_2700638514_VND.repx" : folder + "\\INHD_BK_2700638514_USD.repx";
                //                string rp_code = currencyCode == "VND" ? "sproc_inct_bangke_VND" : "sproc_inct_bangke_USD";
                //                //if (!File.Exists(fileName))
                //                //{
                //                //    fileName = folder + "\\BangKeDinhKem.repx";
                //                //}
                //                XtraReport rpBangKeTST = null;

                //                if (!File.Exists(fileName))
                //                {
                //                    rpBangKeTST = new XtraReport();
                //                    rpBangKeTST.SaveLayout(fileName);
                //                }
                //                else
                //                {
                //                    rpBangKeTST = XtraReport.FromFile(fileName, true);
                //                }

                //                //rpBangKeTST.ScriptReferencesString = "AccountSignature.dll";
                //                rpBangKeTST.Name = "rpBKTST";
                //                rpBangKeTST.DisplayName = "BangKeTST.repx";

                //                Dictionary<string, string> parameters = new Dictionary<string, string>();
                //                //parameters.Add("ma_dvcs", _webHelper.GetDvcs());
                //                parameters.Add("inv_InvoiceAuth_id", inv_InvoiceAuth_id);

                //                DataSet dataSource = this._nopDbContext2.GetDataSet(rp_code, parameters);

                //                rpBangKeTST.DataSource = dataSource;
                //                rpBangKeTST.DataMember = dataSource.Tables[0].TableName;

                //                rpBangKeTST.CreateDocument();
                //                report.Pages.AddRange(rpBangKeTST.Pages);
                //            }
                //        }
                //    }
                //}

                if (tblInv_InvoiceAuth.Columns.Contains("inv_sobangke"))
                {
                    if (tblInv_InvoiceAuth.Rows[0]["inv_sobangke"].ToString().Length > 0)
                    {
                        string fileName = folder + "\\BangKeDinhKem.repx";

                        XtraReport rpBangKe = null;

                        if (!File.Exists(fileName))
                        {
                            rpBangKe = new XtraReport();
                            rpBangKe.SaveLayout(fileName);
                        }
                        else
                        {
                            rpBangKe = XtraReport.FromFile(fileName, true);
                        }

                        rpBangKe.ScriptReferencesString = "AccountSignature.dll";
                        rpBangKe.Name = "rpBangKe";
                        rpBangKe.DisplayName = "BangKeDinhKem.repx";

                        rpBangKe.DataSource = report.DataSource;

                        rpBangKe.CreateDocument();
                        report.Pages.AddRange(rpBangKe.Pages);
                    }



                }

                if (tblInv_InvoiceAuth.Rows[0]["trang_thai_hd"].ToString() == "7")
                {

                    Bitmap bmp = ReportUtil.DrawDiagonalLine(report);
                    int pageCount = report.Pages.Count;


                    for (int i = 0; i < pageCount; i++)
                    {
                        Page page = report.Pages[i];
                        PageWatermark pmk = new PageWatermark();
                        pmk.Image = bmp;
                        page.AssignWatermark(pmk);
                    }

                    string fileName = folder + "\\BienBanXoaBo.repx";
                    XtraReport rpBienBan = XtraReport.FromFile(fileName, true);

                    rpBienBan.ScriptReferencesString = "AccountSignature.dll";
                    rpBienBan.Name = "rpBienBan";
                    rpBienBan.DisplayName = "BienBanXoaBo.repx";

                    rpBienBan.DataSource = report.DataSource;
                    rpBienBan.DataMember = report.DataMember;

                    rpBienBan.CreateDocument();

                    rpBienBan.PrintingSystem.ContinuousPageNumbering = false;
                    report.PrintingSystem.ContinuousPageNumbering = false;

                    report.Pages.AddRange(rpBienBan.Pages);

                    int idx = pageCount;
                    pageCount = report.Pages.Count;

                    for (int i = idx; i < pageCount; i++)
                    {
                        PageWatermark pmk = new PageWatermark();
                        pmk.ShowBehind = false;
                        report.Pages[i].AssignWatermark(pmk);
                    }

                }

                //if (trang_thai_hd == 19 || trang_thai_hd == 21 || trang_thai_hd == 5)
                //{

                //    string rp_file = trang_thai_hd == 19 || trang_thai_hd == 21 ? "INCT_BBDC_GT.repx" : "INCT_BBDC_DD.repx";
                //    string rp_code = trang_thai_hd == 19 || trang_thai_hd == 21 ? "sproc_inct_hd_dieuchinhgt" : "sproc_inct_hd_dieuchinhdg";

                //    string fileName = folder + "\\" + rp_file;
                //    XtraReport rpBienBan = XtraReport.FromFile(fileName, true);

                //    rpBienBan.ScriptReferencesString = "AccountSignature.dll";
                //    rpBienBan.Name = "rpBienBanDC";
                //    rpBienBan.DisplayName = rp_file;

                //    Dictionary<string, string> parameters = new Dictionary<string, string>();
                //    parameters.Add("ma_dvcs", _webHelper.GetDvcs());
                //    parameters.Add("document_id", id);

                //    DataSet dataSource = this._nopDbContext.GetDataSet(rp_code, parameters);

                //    rpBienBan.DataSource = dataSource;
                //    rpBienBan.DataMember = dataSource.Tables[0].TableName;

                //    rpBienBan.CreateDocument();

                //    rpBienBan.PrintingSystem.ContinuousPageNumbering = false;
                //    report.PrintingSystem.ContinuousPageNumbering = false;

                //    report.Pages.AddRange(rpBienBan.Pages);

                //    Page page = report.Pages[report.Pages.Count - 1];
                //    page.AssignWatermark(new PageWatermark());

                //}

                if (trang_thai_hd == 13 || trang_thai_hd == 17)
                {
                    Bitmap bmp = ReportUtil.DrawDiagonalLine(report);
                    int pageCount = report.Pages.Count;

                    for (int i = 0; i < pageCount; i++)
                    {
                        PageWatermark pmk = new PageWatermark();
                        pmk.Image = bmp;
                        report.Pages[i].AssignWatermark(pmk);
                    }
                }



                MemoryStream ms = new MemoryStream();

                if (type == "Html")
                {
                    report.ExportOptions.Html.EmbedImagesInHTML = true;
                    report.ExportOptions.Html.ExportMode = HtmlExportMode.SingleFilePageByPage;
                    report.ExportOptions.Html.Title = "Hóa đơn điện tử M-Invoice";
                    report.ExportToHtml(ms);

                    string html = Encoding.UTF8.GetString(ms.ToArray());

                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(html);


                    string api = this._webHelper.GetRequest().ApplicationPath.StartsWith("/api") ? "/api" : "";
                    string serverApi = this._webHelper.GetRequest().Url.Scheme + "://" + this._webHelper.GetRequest().Url.Authority + api;

                    var nodes = doc.DocumentNode.SelectNodes("//td/@onmousedown");
                    //td[@onmousedown]

                    if (nodes != null)
                    {
                        foreach (HtmlNode node in nodes)
                        {
                            string eventMouseDown = node.Attributes["onmousedown"].Value;

                            if (eventMouseDown.Contains("showCert('seller')"))
                            {
                                node.SetAttributeValue("id", "certSeller");
                            }
                            if (eventMouseDown.Contains("showCert('buyer')"))
                            {
                                node.SetAttributeValue("id", "certBuyer");
                            }
                            if (eventMouseDown.Contains("showCert('vacom')"))
                            {
                                node.SetAttributeValue("id", "certVacom");
                            }
                            if (eventMouseDown.Contains("showCert('minvoice')"))
                            {
                                node.SetAttributeValue("id", "certMinvoice");
                            }
                        }
                    }

                    HtmlNode head = doc.DocumentNode.SelectSingleNode("//head");

                    HtmlNode xmlNode = doc.CreateElement("script");
                    xmlNode.SetAttributeValue("id", "xmlData");
                    xmlNode.SetAttributeValue("type", "text/xmldata");

                    xmlNode.AppendChild(doc.CreateTextNode(xml));
                    head.AppendChild(xmlNode);

                    xmlNode = doc.CreateElement("script");
                    xmlNode.SetAttributeValue("src", serverApi + "/Content/Scripts/jquery-1.6.4.min.js");
                    head.AppendChild(xmlNode);

                    xmlNode = doc.CreateElement("script");
                    xmlNode.SetAttributeValue("src", serverApi + "/Content/Scripts/jquery.signalR-2.2.3.min.js");
                    head.AppendChild(xmlNode);

                    xmlNode = doc.CreateElement("script");
                    xmlNode.SetAttributeValue("type", "text/javascript");

                    xmlNode.InnerHtml = "$(function () { "
                                       + "  var url = 'http://localhost:19898/signalr'; "
                                       + "  var connection = $.hubConnection(url, {  "
                                       + "     useDefaultPath: false "
                                       + "  });"
                                       + " var invoiceHubProxy = connection.createHubProxy('invoiceHub'); "
                                       + " invoiceHubProxy.on('resCommand', function (result) { "
                                       + " }); "
                                       + " connection.start().done(function () { "
                                       + "      console.log('Connect to the server successful');"
                                       + "      $('#certSeller').click(function () { "
                                       + "         var arg = { "
                                       + "              xml: document.getElementById('xmlData').innerHTML, "
                                       + "              id: 'seller' "
                                       + "         }; "
                                       + "         invoiceHubProxy.invoke('execCommand', 'ShowCert', JSON.stringify(arg)); "
                                       + "         }); "
                                       + "      $('#certVacom').click(function () { "
                                       + "         var arg = { "
                                       + "              xml: document.getElementById('xmlData').innerHTML, "
                                       + "              id: 'vacom' "
                                       + "         }; "
                                       + "         invoiceHubProxy.invoke('execCommand', 'ShowCert', JSON.stringify(arg)); "
                                       + "         }); "
                                       + "      $('#certBuyer').click(function () { "
                                       + "         var arg = { "
                                       + "              xml: document.getElementById('xmlData').innerHTML, "
                                       + "              id: 'buyer' "
                                       + "         }; "
                                       + "         invoiceHubProxy.invoke('execCommand', 'ShowCert', JSON.stringify(arg)); "
                                       + "         }); "
                                       + "      $('#certMinvoice').click(function () { "
                                       + "         var arg = { "
                                       + "              xml: document.getElementById('xmlData').innerHTML, "
                                       + "              id: 'minvoice' "
                                       + "         }; "
                                       + "         invoiceHubProxy.invoke('execCommand', 'ShowCert', JSON.stringify(arg)); "
                                       + "         }); "
                                       + "})"
                                       + ".fail(function () { "
                                       + "     alert('failed in connecting to the signalr server'); "
                                       + "});"
                                       + "});";

                    head.AppendChild(xmlNode);

                    if (report.Watermark != null)
                    {
                        if (report.Watermark.Image != null)
                        {
                            ImageConverter _imageConverter = new ImageConverter();
                            byte[] img = (byte[])_imageConverter.ConvertTo(report.Watermark.Image, typeof(byte[]));

                            string imgUrl = "data:image/png;base64," + Convert.ToBase64String(img);

                            HtmlNode style = doc.DocumentNode.SelectSingleNode("//style");

                            string strechMode = report.Watermark.ImageViewMode == ImageViewMode.Stretch ? "background-size: 100% 100%;" : "";
                            string waterMarkClass = ".waterMark { background-image:url(" + imgUrl + ");background-repeat:no-repeat;background-position:center;" + strechMode + " }";

                            HtmlTextNode textNode = doc.CreateTextNode(waterMarkClass);
                            style.AppendChild(textNode);

                            HtmlNode body = doc.DocumentNode.SelectSingleNode("//body");

                            HtmlNodeCollection pageNodes = body.SelectNodes("div");

                            foreach (var pageNode in pageNodes)
                            {
                                pageNode.Attributes.Add("class", "waterMark");

                                string divStyle = pageNode.Attributes["style"].Value;
                                divStyle = divStyle + "margin-left:auto;margin-right:auto;";

                                pageNode.Attributes["style"].Value = divStyle;
                            }
                        }
                    }

                    ms.SetLength(0);
                    doc.Save(ms);

                    doc = null;
                }
                else if (type == "Image")
                {
                    var options = new ImageExportOptions(ImageFormat.Png)
                    {
                        ExportMode = ImageExportMode.SingleFilePageByPage,
                    };
                    report.ExportToImage(ms, options);
                }
                else
                {
                    report.ExportToPdf(ms);
                }

                bytes = ms.ToArray();
                ms.Close();

                if (bytes == null)
                {
                    throw new Exception("null");
                }

            }
            catch (Exception ex)
            {
                //_logService.Insert("PrintInvoiceFromId", ex.ToString());

                throw new Exception(ex.Message.ToString());

            }

            return bytes;
        }
        public byte[] ExportZipFileXML(string sobaomat, string masothue, string pathReport, ref string fileName, ref string key)
        {
            this._nopDbContext2.setConnect(masothue);
            var invoiceDb = this._nopDbContext2.GetInvoiceDb();
            byte[] result = null;

            //string ma_dvcs = this._webHelper.GetDvcs();
            //dmdvcs dvcs = invoiceDb.Dmdvcss.Where(c => c.ma_dvcs == "VP").FirstOrDefault<dmdvcs>();

            DataTable tblInv_InvoiceAuth = this._nopDbContext2.ExecuteCmd("SELECT * FROM inv_InvoiceAuth WHERE sobaomat='" + sobaomat + "'");
            if(tblInv_InvoiceAuth.Rows.Count <=0)
            {
                return null;
            }
            string mau_hd = tblInv_InvoiceAuth.Rows[0]["mau_hd"].ToString();
            string so_serial = tblInv_InvoiceAuth.Rows[0]["inv_invoiceSeries"].ToString();
            string so_hd = tblInv_InvoiceAuth.Rows[0]["inv_invoiceNumber"].ToString();
            string inv_InvoiceCode_id = tblInv_InvoiceAuth.Rows[0]["inv_InvoiceCode_id"].ToString();

            fileName = masothue + "_invoice_" + mau_hd.Replace("/", "") + "_" + so_serial.Replace("/", "").Trim() + "_" + so_hd;
            Guid inv_InvoiceAuth_id = Guid.Parse(tblInv_InvoiceAuth.Rows[0]["inv_InvoiceAuth_id"].ToString());
            DataTable tblInvoiceXmlData = this._nopDbContext2.ExecuteCmd("SELECT * FROM InvoiceXmlData WHERE inv_InvoiceAuth_id='" + inv_InvoiceAuth_id + "'");

            if (tblInvoiceXmlData.Rows.Count == 0)
            {
                return null;
            }

            DataTable tblCtthongbao = this._nopDbContext2.ExecuteCmd("SELECT * FROM ctthongbao WHERE ctthongbao_id='" + inv_InvoiceCode_id + "'");
            DataTable tblMauHoaDon = this._nopDbContext2.ExecuteCmd("SELECT dmmauhoadon_id,report FROM dmmauhoadon WHERE dmmauhoadon_id='" + tblCtthongbao.Rows[0]["dmmauhoadon_id"].ToString() + "'");

            string xml = tblInvoiceXmlData.Rows[0]["data"].ToString();
            xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>" + xml;
            //key = Guid.NewGuid().ToString();
            //string encodeXML = EncodeXML.Encrypt(xml, key);

            //byte[] dataPdf = this.PrintInvoiceFromId(id, pathReport, "PDF");

            MemoryStream outputMemStream = new MemoryStream();
            ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);

            zipStream.SetLevel(3); //0-9, 9 being the highest level of compression

            // attack file xml
            ZipEntry newEntry = new ZipEntry(masothue + ".xml");
            newEntry.DateTime = DateTime.Now;
            newEntry.IsUnicodeText = true;

            zipStream.PutNextEntry(newEntry);

            byte[] bytes = Encoding.UTF8.GetBytes(xml);

            MemoryStream inStream = new MemoryStream(bytes);
            inStream.WriteTo(zipStream);
            inStream.Close();
            zipStream.CloseEntry();

            // attack file key
            //newEntry = new ZipEntry("key.txt");
            //newEntry.DateTime = DateTime.Now;
            //newEntry.IsUnicodeText = true;

            //zipStream.PutNextEntry(newEntry);
            //byte[] bytekey = Encoding.UTF8.GetBytes(key);
            //inStream = new MemoryStream(bytekey);
            //inStream.WriteTo(zipStream);
            //inStream.Close();
            //zipStream.CloseEntry();

            inStream = new MemoryStream();
            using (StreamWriter sw = new StreamWriter(inStream))
            {
                sw.Write(tblMauHoaDon.Rows[0]["report"].ToString());
                sw.Flush();

                newEntry = new ZipEntry("invoice.repx");
                newEntry.DateTime = DateTime.Now;
                newEntry.IsUnicodeText = true;
                zipStream.PutNextEntry(newEntry);

                inStream.WriteTo(zipStream);
                inStream.Close();
                zipStream.CloseEntry();

                sw.Close();
            }

            zipStream.IsStreamOwner = false;    // False stops the Close also Closing the underlying stream.
            zipStream.Close();          // Must finish the ZipOutputStream before using outputMemStream.

            outputMemStream.Position = 0;


            result = outputMemStream.ToArray();

            outputMemStream.Close();

            return result;
        }


        //tìm  tt hóa đơn 78
        private JObject GetInfoInvoice78(JObject model)
        {
            JObject _res = new JObject();
            //Util.ErrorCodeJObject err = new Util.ErrorCodeJObject();
            string xml = "";

            try
            {
                string masothue = model["masothue"].ToString().Replace("-", "");
                string sobaomat = model["sobaomat"].ToString();
                _nopDbContext2.setConnect(masothue);
                //InvoiceDbContext db = _nopDbContext2.GetInvoiceDb();

                Dictionary<string, object> para = new Dictionary<string, object>
                {
                    {@"sobaomat", sobaomat }
                };
                string sqlQuery = "SELECT TOP 1 " + " "
                                    + "CASE WHEN a.tgtcthue IS NULL THEN SUM(b.tgtien) ELSE a.tgtcthue END AS inv_TotalAmountWithoutVat, " + " "
                                    + "CASE WHEN a.tgtthue IS NULL THEN SUM(b.tthue) ELSE a.tgtthue END AS inv_vatAmount, " + " "
                                    + "a.tgtttbso AS inv_TotalAmount, " + " "
                                    + "CASE WHEN a.ttcktmai IS NULL THEN SUM(b.stckhau) ELSE a.ttcktmai END AS inv_discountAmount, " + " "
                                    + "REPLACE(a.khieu, ' ', '') AS inv_invoiceSeries, " + " "
                                    + "SUBSTRING(REPLACE(a.khieu, ' ', ''),1,1) AS mau_hd, " + " "
                                    + "a.shdon as inv_invoiceNumber, a.tdlap AS inv_invoiceIssuedDate, " + " "
                                    + "  a.mst AS inv_buyerTaxCode,  " + " "
                                    + "a.dchi AS inv_buyerAddressLine ,  " + " "
                                    + "a.hoadon68_id AS inv_InvoiceAuth_id, " + " "
                                    + "a.sbmat AS sobaomat " + " "
                                    + "FROM dbo.hoadon68 a " + " "
                                    + "INNER JOIN dbo.hoadon68_chitiet b ON b.hoadon68_id = a.hoadon68_id " + " "
                                    + "WHERE a.sbmat = @sobaomat " + " "
                                    + "GROUP BY a.tgtcthue, a.tgtthue, a.tgtttbso, a.khieu, a.shdon, a.tdlap,a.mst,a.dchi,a.hoadon68_id,a.sbmat,a.ttcktmai " + " ";
                DataTable dt = _nopDbContext2.ExecuteCmd(sqlQuery, CommandType.Text, para);



                //TracuuHDDTContext tracuu = new TracuuHDDTContext();
                var withoutSpecial = new string(sobaomat.Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)).ToArray());
                if (sobaomat != withoutSpecial)
                {
                    throw new Exception("Chuỗi số bảo mật không đúng định dạng ");
                }
                //TracuuHDDTContext tracuu = conn.getdb();
                //inv_customer_banned checkTraCuu = tracuu.inv_customer_banneds.FirstOrDefault(x =>
                //    x.mst.Replace("-", "").Equals(masothue.Replace("-", "")) && x.type.Equals("KHOATRACUU") && x.is_unblock == false);
                //if (checkTraCuu != null && !string.IsNullOrEmpty(checkTraCuu.mst))
                //{
                //    _res.Add("error", $"Quý khách đang bị khóa tra cứu. Vui lòng liên hệ admin để giải quyết");
                //    return _res;
                //}
                if (dt.Rows.Count == 0)
                {
                    if (string.IsNullOrEmpty(masothue))
                    {
                        _res.Add("error", "nhập mã số thuế đơn vị bán");
                        return _res;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(sobaomat))
                        {
                            _res.Add("error", "nhập số bảo mật");
                            return _res;
                        }
                        else
                        {
                            _res.Add("error", $"Không tồn tại hóa đơn có số bảo mật: {sobaomat} tại mã số thuế: {masothue}");
                            return _res;
                        }
                    }
                }


                dt.Columns.Add("masothue", typeof(string));
                dt.Columns.Add("auth_id", typeof(string));
                dt.Columns.Add("sum_tien", typeof(double));
                dt.Columns.Add("ecd", typeof(string));
                DataTable sumTien = _nopDbContext2.ExecuteCmd($"SELECT 	CASE WHEN a.dvtte =N'VND' THEN ROUND(ISNULL(a.tgtttbso,0),0) ELSE a.tgtttbso END AS sum_total_amount  FROM dbo.hoadon68 a LEFT JOIN dbo.hoadon68_chitiet b ON a.hoadon68_id=b.hoadon68_id WHERE b.hoadon68_id = '{dt.Rows[0]["inv_InvoiceAuth_id"].ToString()}' GROUP BY a.tgtttbso,a.dvtte");
                DataTable tblInvoiceXmlData = _nopDbContext2.ExecuteCmd($"SELECT * FROM dbo.hoadon68_xml WHERE hoadon68_id = '{dt.Rows[0]["inv_InvoiceAuth_id"].ToString()}'");
                //xml = tblInvoiceXmlData.Rows.Count > 0 ? tblInvoiceXmlData.Rows[0]["data"].ToString() : db.Database.SqlQuery<string>($"EXECUTE sproc_export_XmlInvoice '{dt.Rows[0]["hoadon68_id"].ToString()}'").FirstOrDefault();


                string connectionString = Util.EncodeXML.Encrypt(_nopDbContext2.GetInvoiceDb().Database.Connection.ConnectionString, "NAMPV18081202");
                byte[] bytes = Encoding.UTF8.GetBytes(connectionString);
                string b = Convert.ToBase64String(bytes);
                foreach (DataRow row in dt.Rows)
                {
                    row.BeginEdit();
                    row["masothue"] = model["masothue"].ToString();
                    row["auth_id"] = b;
                    row["sum_tien"] = sumTien.Rows[0]["sum_total_amount"];
                    //row["ecd"] = xml;
                    row.EndEdit();
                }

                JArray ar = JArray.FromObject(dt);
                JObject obj = new JObject();
                obj.Add("data", ar);

                //err.code = "00";
                //err.message = null;
                //err.data = obj;
                //_res = JObject.Parse(JsonConvert.SerializeObject(err));
                _res = obj;
            }
            catch (Exception ex)
            {
                //err.code = "99";
                //err.message = ex.Message;
                //err.data = null;
                //_res = JObject.Parse(JsonConvert.SerializeObject(err));
                _res.Add("error", ex.Message);
            }
            return _res;
        }


        //new version in hóa đơn 32 & 78

        public byte[] PrintInvoiceFromSBM(string sobaomat, string masothue, string folder, string type, bool inchuyendoi, out string fileNamePrint)
        {
            _nopDbContext2.setConnect(masothue);
            Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                   {"@sobaomat", sobaomat}
                };
            var sql = "SELECT TOP 1 a.* FROM inv_InvoiceAuth AS a INNER JOIN dbo.InvoiceXmlData AS b ON b.inv_InvoiceAuth_id = a.inv_InvoiceAuth_id WHERE a.sobaomat = @sobaomat";
            DataTable tblInvInvoiceAuth = _nopDbContext2.ExecuteCmd(sql, CommandType.Text, parameters);
            try
            {
                if (tblInvInvoiceAuth.Rows.Count == 0)
                {
                    string xml;
                    var bytes = inHoadon(sobaomat, masothue, folder, type, inchuyendoi, out xml, out fileNamePrint);
                    return bytes;
                }
                else
                {
                    string xml;
                    var bytes = PrintInvoice(sobaomat, masothue, folder, type, inchuyendoi, out xml, out fileNamePrint);
                    return bytes;
                }
            }
            catch (Exception)
            {

                throw new Exception("Không tồn tại hóa đơn có số bảo mật " + sobaomat);
            }

        }



        #region print 78

        private byte[] inHoadon(string sobaomat, string masothue, string folder, string type, bool inchuyendoi, out string xml, out string fileNamePrint)
        {

            _nopDbContext2.setConnect(masothue);
            var db = _nopDbContext2.GetInvoiceDb();
            byte[] bytes;
            xml = "";
            string msgTb = "";
            string mccqt = "";

            try
            {
                DataTable tblInvInvoiceAuth = this._nopDbContext2.ExecuteCmd("SELECT * FROM hoadon68 WHERE sbmat = @sobaomat", CommandType.Text, new Dictionary<string, object>
                {
                    {"@sobaomat", sobaomat }
                });
                if (tblInvInvoiceAuth.Rows.Count == 0)
                {
                    throw new Exception("Không tồn tại hóa đơn có số bảo mật " + sobaomat);
                }
                string invInvoiceAuthId = tblInvInvoiceAuth.Rows[0]["hoadon68_id"].ToString();
                DataTable tblInv_InvoiceAuthDetail = this._nopDbContext2.ExecuteCmd("SELECT * FROM dbo.hoadon68_chitiet WHERE hoadon68_id = '" + invInvoiceAuthId + "'");


                DataTable tblHoadon = this._nopDbContext2.ExecuteCmd("SELECT * FROM hoadon68 WHERE hoadon68_id='" + invInvoiceAuthId + "'");
                DataTable tblInvoiceXmlData = this._nopDbContext2.ExecuteCmd("SELECT * FROM hoadon68_xml WHERE hoadon68_id='" + invInvoiceAuthId + "'");
                DateTime ngayky = new DateTime();

                var invoiceDb = this._nopDbContext2.GetInvoiceDb();
                string inv_InvoiceCode_id = tblInvInvoiceAuth.Rows[0]["cctbao_id"].ToString();
                string inv_originalId = tblInvInvoiceAuth.Rows[0]["hdlket_id"].ToString();
                int trangThaiHd = Convert.ToInt32(tblInvInvoiceAuth.Rows[0]["is_tthdon"]);
                string soHd = tblInvInvoiceAuth.Rows[0]["shdon"].ToString();



                XmlDocument docInvoice = new XmlDocument();
                docInvoice.PreserveWhitespace = true;

                fileNamePrint = $"{masothue}_invoice_{inv_InvoiceCode_id.Trim().Replace("/", "")}_{soHd}";

                if (tblInvoiceXmlData.Rows.Count > 0)
                {
                    xml = tblInvoiceXmlData.Rows[0]["data"].ToString();
                    docInvoice.LoadXml(xml);

                    string ngayk = docInvoice.GetElementsByTagName("SigningTime")[0].InnerText;
                    ngayky = Convert.ToDateTime(ngayk);
                }
                //else
                //{
                //    JObject jsXml = await XmlInvoice(hdon_id.ToString(), 0);
                //    if (jsXml["error"] != null)
                //    {
                //        throw new Exception("Thành tiền bằng chữ không được để trống !");
                //    }
                //    xml = jsXml["xml"].ToString();

                //    byte[] bytesValid = Convert.FromBase64String(xml);

                //    xml = Encoding.UTF8.GetString(bytesValid);
                //    docInvoice.LoadXml(xml);
                //}
                string dvtte = tblHoadon.Rows[0]["dvtte"].ToString();
                string sbmat = tblHoadon.Rows[0]["sbmat"].ToString();
                string cctbao_id = tblHoadon.Rows[0]["cctbao_id"].ToString();
                int trang_thai_hd = Convert.ToInt32(tblHoadon.Rows[0]["tthdon"]);
                int is_trang_thai_hd = Convert.ToInt32(tblHoadon.Rows[0]["is_tthdon"]);
                string sbke = tblHoadon.Rows[0]["sbke"].ToString();

                //DataTable tblHoadonChitiet = this._nopDbContext2.ExecuteCmd("SELECT MAX(cast(tsuat as int)) FROM hoadon68_chitiet WHERE hoadon68_id='" + invInvoiceAuthId + "'");
                //string tsuatMAX = tblHoadonChitiet.Rows[0][0].ToString();

                DataTable tblHoadonChitiet = _nopDbContext2.ExecuteCmd(string.Concat("SELECT *, cast(tsuat as float) as thuesuat, cast(isnull(stt,0) as int) as sothutu FROM hoadon68_chitiet WHERE hoadon68_id='", invInvoiceAuthId, "'"));
                object tsuatMAX = tblHoadonChitiet.Compute("max([thuesuat])", string.Empty);

                XtraReport report = new XtraReport();

                DataTable tblKyhieu = _nopDbContext2.ExecuteCmd("SELECT * FROM quanlykyhieu68 WHERE quanlykyhieu68_id='" + cctbao_id + "'");
                if (tblKyhieu.Rows.Count <= 0)
                {
                    throw new Exception("Không tồn tại ký hiệu hóa đơn");
                }
                int sdmau = 0;
                if (!string.IsNullOrEmpty(tblKyhieu.Rows[0]["sdmau"].ToString()))
                {
                    sdmau = Convert.ToInt32(tblKyhieu.Rows[0]["sdmau"].ToString());
                }
                DataTable tblDmmauhd = _nopDbContext2.ExecuteCmd("SELECT * FROM quanlymau68 WHERE quanlymau68_id='" + tblKyhieu.Rows[0]["quanlymau68_id"] + "'");

                if (tblDmmauhd.Rows.Count <= 0)
                {
                    throw new Exception("Không tồn tại mẫu hóa đơn");
                }

                string invReport = tblDmmauhd.Rows[0]["dulieumau"].ToString();

                if (invReport.Length > 0)
                {
                    report = Util.ReportUtil.LoadReportFromString(invReport);
                    // _cacheManager.Set(cacheReportKey, report, 30);
                }
                else
                {
                    throw new Exception("Không tải được mẫu hóa đơn");
                }

                //   }

                report.Name = "XtraReport1";
                report.ScriptReferencesString = "AccountSignature.dll";

                DataSet ds = new DataSet();
                //string sproc = "sproc_print_invoice";

                //Dictionary<string, string> parametersRP = new Dictionary<string, string>();
                //parametersRP.Add("hoadon68_id", hdon_id.ToString());

                //ds = this._nopDbContext.GetDataSet(sproc, parametersRP);

                using (XmlReader xmlReader2 = XmlReader.Create(new StringReader(report.DataSourceSchema)))
                {
                    ds.ReadXmlSchema(xmlReader2);
                    xmlReader2.Close();
                }

                using (XmlReader xmlReader = XmlReader.Create(new StringReader(xml)))
                {
                    ds.ReadXml(xmlReader);
                    xmlReader.Close();
                }

                if (ds.Tables.Contains("TblXmlData"))
                {
                    ds.Tables.Remove("TblXmlData");
                }

                #region  TTKhac trong chi tiết
                var tagDSHHDVu = docInvoice.GetElementsByTagName("DSHHDVu")[0] as XmlElement;
                var tagTTChung = docInvoice.GetElementsByTagName("TTChung")[0] as XmlElement;
                DataTable tblTToan = ds.Tables["TToan"];

                if (tblTToan != null)
                {
                    DataColumn[] columnsTT = new DataColumn[9]
                    {
                    new DataColumn("TongTienThue10", typeof(decimal)),
                    new DataColumn("TongTienTruocThue10", typeof(decimal)),
                    new DataColumn("TongTien10", typeof(decimal)),
                    new DataColumn("TongTienThue5", typeof(decimal)),
                    new DataColumn("TongTienTruocThue5", typeof(decimal)),
                    new DataColumn("TongTien5", typeof(decimal)),
                    new DataColumn("TongTienTruocThue0", typeof(decimal)),
                    new DataColumn("TongTienTruocThueKCT", typeof(decimal)),
                    new DataColumn("TongTienTruocThueKKK", typeof(decimal))
                    };
                    tblTToan.Columns.AddRange(columnsTT);
                    tblTToan.Rows[0]["TongTienThue10"] = tblHoadon.Rows[0]["tgtthue10"];
                    tblTToan.Rows[0]["TongTienTruocThue10"] = tblHoadon.Rows[0]["tgtcthue10"];
                    tblTToan.Rows[0]["TongTien10"] = tblHoadon.Rows[0]["tgtttbso10"];
                    tblTToan.Rows[0]["TongTienThue5"] = tblHoadon.Rows[0]["tgtthue5"];
                    tblTToan.Rows[0]["TongTienTruocThue5"] = tblHoadon.Rows[0]["tgtcthue5"];
                    tblTToan.Rows[0]["TongTien5"] = tblHoadon.Rows[0]["tgtttbso5"];
                    tblTToan.Rows[0]["TongTienTruocThue0"] = tblHoadon.Rows[0]["tgtcthue0"];
                    tblTToan.Rows[0]["TongTienTruocThueKCT"] = tblHoadon.Rows[0]["tgtcthuekct"];
                    tblTToan.Rows[0]["TongTienTruocThueKKK"] = tblHoadon.Rows[0]["tgtcthuekkk"];
                }

                XmlNodeList nodeListTTKhac2 = tagDSHHDVu.GetElementsByTagName("TTKhac");
                DataTable tblHHDV = ds.Tables["HHDVu"];
                DataColumn[] columns = new DataColumn[3]
                {
                new DataColumn("tthue", typeof(decimal)),
                new DataColumn("stt_rec", typeof(string)),
                new DataColumn("DVTTe", typeof(string))
                };
                tblHHDV.Columns.AddRange(columns);
                if (!tblHHDV.Columns.Contains("tgtien"))
                {
                    tblHHDV.Columns.Add("tgtien", typeof(decimal));
                }
                foreach (DataRow dr2 in tblHHDV.Rows)
                {
                    string stt = (string.IsNullOrEmpty(dr2["STT"].ToString()) ? "0" : dr2["STT"].ToString());
                    DataRow result = (from myRow in tblHoadonChitiet.AsEnumerable()
                                      where myRow.Field<int>("sothutu") == Convert.ToInt32(stt)
                                      select myRow).FirstOrDefault();
                    if (result != null)
                    {
                        dr2.BeginEdit();
                        dr2["tthue"] = result["tthue"];
                        dr2["tgtien"] = result["tgtien"];
                        dr2["stt_rec"] = result["stt_rec"];
                        dr2["DVTTe"] = dvtte;
                        dr2.EndEdit();
                        dr2.AcceptChanges();
                    }
                }
                if (nodeListTTKhac2.Count > 0)
                {
                    for (int l = 0; l < nodeListTTKhac2.Count; l++)
                    {
                        XmlElement tagTTKhac2 = nodeListTTKhac2[l] as XmlElement;
                        XmlNodeList nodeListTTin2 = tagTTKhac2.GetElementsByTagName("TTin");
                        foreach (XmlNode nodeTTin2 in nodeListTTin2)
                        {
                            XmlElement tagTTin2 = nodeTTin2 as XmlElement;
                            XmlNode nodeTTruong2 = tagTTin2.GetElementsByTagName("TTruong")[0];
                            XmlNode nodeKDLieu2 = tagTTin2.GetElementsByTagName("KDLieu")[0];
                            XmlNode nodeDLieu2 = tagTTin2.GetElementsByTagName("DLieu")[0];
                            string kdlieu2 = nodeKDLieu2.InnerText;
                            string ttruong2 = nodeTTruong2.InnerText;
                            string dlieu2 = ((nodeDLieu2 == null) ? string.Empty : nodeDLieu2.InnerText);
                            if (l == 0)
                            {
                                DataColumn col2 = new DataColumn(ttruong2);
                                int num;
                                switch (kdlieu2)
                                {
                                    case "string":
                                        col2.DataType = Type.GetType("System.String");
                                        break;
                                    case "numeric":
                                        col2.DataType = Type.GetType("System.Decimal");
                                        break;
                                    default:
                                        num = ((kdlieu2 == "dateTime") ? 1 : 0);
                                        goto IL_10fd;
                                    case "date":
                                        {
                                            num = 1;
                                            goto IL_10fd;
                                        }
                                    IL_10fd:
                                        if (num != 0)
                                        {
                                            col2.DataType = Type.GetType("System.DateTime");
                                        }
                                        break;
                                }
                                tblHHDV.Columns.Add(col2);
                            }
                            DataRow row2 = tblHHDV.Rows[l];
                            if (!string.IsNullOrEmpty(dlieu2))
                            {
                                row2.BeginEdit();
                                row2[ttruong2] = dlieu2;
                                row2.EndEdit();
                                row2.AcceptChanges();
                            }
                        }
                    }
                }


                if (tblHHDV.Rows.Count < sdmau)
                {
                    for (int k = tblHHDV.Rows.Count; k < sdmau; k++)
                    {
                        DataRow dr = tblHHDV.NewRow();
                        tblHHDV.Rows.Add(dr);
                    }
                }
                nodeListTTKhac2 = tagTTChung.GetElementsByTagName("TTKhac");
                DataTable tblTTChung = ds.Tables["TTChung"];
                if (nodeListTTKhac2.Count > 0)
                {
                    for (int j = 0; j < nodeListTTKhac2.Count; j++)
                    {
                        XmlElement tagTTKhac = nodeListTTKhac2[j] as XmlElement;
                        XmlNodeList nodeListTTin = tagTTKhac.GetElementsByTagName("TTin");
                        foreach (XmlNode nodeTTin in nodeListTTin)
                        {
                            XmlElement tagTTin = nodeTTin as XmlElement;
                            XmlNode nodeTTruong = tagTTin.GetElementsByTagName("TTruong")[0];
                            XmlNode nodeKDLieu = tagTTin.GetElementsByTagName("KDLieu")[0];
                            XmlNode nodeDLieu = tagTTin.GetElementsByTagName("DLieu")[0];
                            string kdlieu = nodeKDLieu.InnerText;
                            string ttruong = nodeTTruong.InnerText;
                            string dlieu = ((nodeDLieu == null) ? string.Empty : nodeDLieu.InnerText);
                            if (j == 0)
                            {
                                DataColumn col = new DataColumn(ttruong);
                                int num2;
                                switch (kdlieu)
                                {
                                    case "string":
                                        col.DataType = Type.GetType("System.String");
                                        break;
                                    case "numeric":
                                        col.DataType = Type.GetType("System.Decimal");
                                        break;
                                    default:
                                        num2 = ((kdlieu == "dateTime") ? 1 : 0);
                                        goto IL_14f2;
                                    case "date":
                                        {
                                            num2 = 1;
                                            goto IL_14f2;
                                        }
                                    IL_14f2:
                                        if (num2 != 0)
                                        {
                                            col.DataType = Type.GetType("System.DateTime");
                                        }
                                        break;
                                }
                                tblTTChung.Columns.Add(col);
                            }
                            DataRow row = tblTTChung.Rows[j];
                            if (!string.IsNullOrEmpty(dlieu))
                            {
                                row.BeginEdit();
                                row[ttruong] = dlieu;
                                row.EndEdit();
                                row.AcceptChanges();
                            }
                        }
                    }
                }





                #endregion

                //DataTable tblXmlData = new DataTable();
                //tblXmlData.TableName = "TblXmlData";
                //tblXmlData.Columns.Add("data");

                //DataRow r = tblXmlData.NewRow();
                //r["data"] = xml;
                //tblXmlData.Rows.Add(r);
                //ds.Tables.Add(tblXmlData);

                //string datamember = report.DataMember;

                //if (datamember.Length > 0)
                //{
                //    if (ds.Tables.Contains(datamember))
                //    {
                //        DataTable tblChiTiet = ds.Tables[datamember];
                //        int rowcount = ds.Tables[datamember].Rows.Count;
                //    }
                //}

                if (tblHoadon.Rows[0]["tthdon"].ToString() == "3")
                {
                    string sql = "SELECT * FROM hoadon68 a "
                            + "WHERE a.hdon68_id_lk='" + invInvoiceAuthId + "'";

                    DataTable tblInv = _nopDbContext2.ExecuteCmd(sql);

                    if (tblInv.Rows.Count > 0)
                    {
                        msgTb = "Hóa đơn bị thay thế bởi hóa đơn số: " + tblInv.Rows[0]["shdon"].ToString() + " ký hiệu: " + tblInv.Rows[0]["khieu"].ToString() + "ngày: " + tblInv.Rows[0]["tdlap"].ToString();
                    }

                }

                if (tblHoadon.Rows[0]["tthdon"].ToString() == "2")
                {
                    string sql = "SELECT * FROM hoadon68 a "
                            + "WHERE a.hdon68_id_lk='" + invInvoiceAuthId + "'";

                    DataTable tblInv = _nopDbContext2.ExecuteCmd(sql);

                    if (tblInv.Rows.Count > 0)
                    {
                        msgTb = "Hóa đơn thay thế cho hóa đơn số: " + tblInv.Rows[0]["shdon"].ToString() + " ký hiệu: " + tblInv.Rows[0]["khieu"].ToString() + "ngày: " + tblInv.Rows[0]["tdlap"].ToString();
                    }
                }

                if (report.Parameters["MSG_TB"] != null)
                {
                    report.Parameters["MSG_TB"].Value = msgTb;
                }

                if (report.Parameters["MCCQT"] != null)
                {
                    report.Parameters["MCCQT"].Value = tblHoadon.Rows[0]["macqt"].ToString();
                }

                if (report.Parameters["NGAY_KY"] != null)
                {
                    report.Parameters["NGAY_KY"].Value = ngayky;
                }

                if (report.Parameters["TSUAT_MAX"] != null)
                {
                    report.Parameters["TSUAT_MAX"].Value = tsuatMAX;
                }

                if (report.Parameters["SO_BAO_MAT"] != null)
                {
                    report.Parameters["SO_BAO_MAT"].Value = sbmat;
                }
                DataTable Title = this._nopDbContext2.ExecuteCmd("SELECT a.lhdon FROM dbo.quanlykyhieu68 a INNER JOIN dbo.hoadon68 b ON a.quanlykyhieu68_id = b.cctbao_id WHERE b.hoadon68_id = '" + invInvoiceAuthId + "'");

                if (inchuyendoi)
                {
                    XRTable tblInChuyenDoi = report.AllControls<XRTable>().FirstOrDefault(c => c.Name == "tblInChuyenDoi");
                    if (tblInChuyenDoi != null)
                    {
                        tblInChuyenDoi.Visible = true;
                    }
                    if (report.Parameters["MSG_HD_TITLE"] != null)
                    {
                        report.Parameters["MSG_HD_TITLE"].Value = ((Title.Rows[0]["lhdon"].ToString() == "6") ? "Hóa đơn chuyển đổi từ phiếu xuất kho kiêm vạn chuyển nội bộ" : "Hóa đơn chuyển đổi từ hóa đơn điện tử");
                    }
                    if (report.Parameters["NGAY_IN_CDOI"] != null)
                    {
                        report.Parameters["NGAY_IN_CDOI"].Value = DateTime.Now;
                        report.Parameters["NGAY_IN_CDOI"].Visible = true;
                    }
                }

                //if (report.Parameters["MCCQT"] != null)
                //{
                //    report.Parameters["MCCQT"].Value = macqt;
                //}

                //var lblHoaDonMau = report.AllControls<XRLabel>().Where(c => c.Name == "lblHoaDonMau").FirstOrDefault<XRLabel>();

                //if (lblHoaDonMau != null)
                //{
                //    lblHoaDonMau.Visible = false;
                //}


                DetailBand detailBand = report.AllControls<DetailBand>().FirstOrDefault();
                if (detailBand != null)
                {
                    int sort = detailBand.SortFields.Count;
                    for (int i = 0; i < sort; i++)
                    {
                        detailBand.SortFields.RemoveAt(i);
                    }
                }

                var TblKyHoaDon = report.AllControls<XRTable>().Where(c => c.Name == "TblKyHoaDon").FirstOrDefault<XRTable>();
                if (TblKyHoaDon != null)
                {
                    // xóa event in
                    TblKyHoaDon.Scripts.OnBeforePrint = string.Empty;
                    DateTime dt = new DateTime();
                    if (ngayky.Year > dt.Year)
                    {
                        TblKyHoaDon.Visible = true;
                    }
                    else
                    {
                        TblKyHoaDon.Visible = false;
                    }
                }

                var picheckHoadon = report.AllControls<XRPictureBox>().Where(c => c.Name == "pictChek").FirstOrDefault<XRPictureBox>();
                if (picheckHoadon != null)
                {
                    // xóa event in
                    picheckHoadon.Scripts.OnBeforePrint = string.Empty;
                    DateTime dt = new DateTime();
                    if (ngayky.Year > dt.Year)
                    {
                        picheckHoadon.Visible = true;
                    }
                    else
                    {
                        picheckHoadon.Visible = false;
                    }
                }

                //if (inchuyendoi)
                //{
                //    var tblInChuyenDoi = report.AllControls<XRTable>().Where(c => c.Name == "tblInChuyenDoi").FirstOrDefault<XRTable>();

                //    if (tblInChuyenDoi != null)
                //    {
                //        tblInChuyenDoi.Visible = true;
                //    }

                //    if (report.Parameters["MSG_HD_TITLE"] != null)
                //    {
                //        report.Parameters["MSG_HD_TITLE"].Value = "Hóa đơn chuyển đổi từ hóa đơn điện tử";
                //    }

                //    if (report.Parameters["NGUOI_IN_CDOI"] != null)
                //    {
                //        DataTable dt = this._nopDbContext2.ExecuteCmd($"SELECT * FROM wb_user WHERE username ='{_webHelper.GetUser()}'");

                //        report.Parameters["NGUOI_IN_CDOI"].Value = dt.Rows[0]["ten_nguoi_sd"].ToString();
                //        report.Parameters["NGUOI_IN_CDOI"].Visible = true;
                //    }

                //    if (report.Parameters["TSUAT_MAX"] != null)
                //    {
                //        report.Parameters["TSUAT_MAX"].Value = tsuatMAX;
                //    }

                //    //string invCurrencyCode = tblInvInvoiceAuth.Rows[0]["inv_currencyCode"].ToString();
                //    //DataTable tbldmnt = _nopDbContext2.ExecuteCmd($"SELECT * FROM dbo.dmnt	WHERE ma_nt = '{invCurrencyCode}'");
                //    if (report.Parameters["NGAY_IN_CDOI"] != null)
                //    {
                //        report.Parameters["NGAY_IN_CDOI"].Value = DateTime.Now;
                //        report.Parameters["NGAY_IN_CDOI"].Visible = true;
                //    }
                //}

                report.DataSource = ds;
                var t = Task.Run(() =>
                {
                    var newCulture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
                    newCulture.NumberFormat.NumberDecimalSeparator = ",";
                    newCulture.NumberFormat.NumberGroupSeparator = ".";
                    Thread.CurrentThread.CurrentCulture = newCulture;
                    Thread.CurrentThread.CurrentUICulture = newCulture;
                    report.CreateDocument();
                });
                t.Wait();

                #region Bảng kê đính kèm
                if (!string.IsNullOrEmpty(sbke))
                {
                    string fileName = folder + "\\BangKeSkypec.repx";

                    XtraReport rpBangKe = null;

                    if (!File.Exists(fileName))
                    {
                        rpBangKe = new XtraReport();
                        rpBangKe.SaveLayout(fileName);
                    }
                    else
                    {
                        rpBangKe = XtraReport.FromFile(fileName, true);
                    }

                    rpBangKe.ScriptReferencesString = "AccountSignature.dll";
                    rpBangKe.Name = "rpBangKe";
                    rpBangKe.DisplayName = "BangKeDinhKem.repx";

                    Dictionary<string, string> parameters = new Dictionary<string, string>();
                    parameters.Add("hoadon68_id", invInvoiceAuthId.ToString());

                    DataSet dataSource = this._nopDbContext2.GetDataSet("sproc_print_bangke", parameters);

                    rpBangKe.DataSource = dataSource;
                    rpBangKe.DataMember = dataSource.Tables[0].TableName;
                    rpBangKe.CreateDocument();

                    report.Pages.AddRange(rpBangKe.Pages);
                }
                #endregion

                #region hóa đơn bị thay thế = 6, bị điều chỉnh 7, thay thế = 3, điều chỉnh = 2
                //if (is_trang_thai_hd == 2 || is_trang_thai_hd == 3)
                //{
                //    string rp_file = is_trang_thai_hd == 2 ? "INCT_BBDC.repx" : "INCT_BBTH.repx";
                //    string rp_code = is_trang_thai_hd == 2 ? "sproc_inct_hd_dieuchinh" : "sproc_inct_hd_thaythe";

                //    string fileName = folder + "\\" + rp_file;
                //    XtraReport rpBienBan = XtraReport.FromFile(fileName, true);

                //    rpBienBan.ScriptReferencesString = "AccountSignature.dll";
                //    rpBienBan.Name = "rpBienBanDC";
                //    rpBienBan.DisplayName = rp_file;

                //    Dictionary<string, string> parameters = new Dictionary<string, string>();
                //    //parameters.Add("ma_dvcs", _webHelper.GetDvcs());
                //    parameters.Add("document_id", invInvoiceAuthId);

                //    DataSet dataSource = this._nopDbContext2.GetDataSet(rp_code, parameters);

                //    rpBienBan.DataSource = dataSource;
                //    rpBienBan.DataMember = dataSource.Tables[0].TableName;

                //    rpBienBan.CreateDocument();

                //    rpBienBan.PrintingSystem.ContinuousPageNumbering = false;
                //    report.PrintingSystem.ContinuousPageNumbering = false;

                //    report.Pages.AddRange(rpBienBan.Pages);

                //    Page page = report.Pages[report.Pages.Count - 1];
                //    page.AssignWatermark(new PageWatermark());

                //}
                #endregion

                // hóa đơn hủy
                //if (trang_thai_hd == 1)
                //{

                //    string rp_file = "INCT_BBHUY.repx";
                //    string rp_code = "sproc_inct_hd_huy";

                //    string fileName = folder + "\\" + rp_file;
                //    XtraReport rpBienBan = XtraReport.FromFile(fileName, true);

                //    rpBienBan.ScriptReferencesString = "AccountSignature.dll";
                //    rpBienBan.Name = "rpBienBanHuy";
                //    rpBienBan.DisplayName = rp_file;

                //    Dictionary<string, string> parameters = new Dictionary<string, string>();
                //    parameters.Add("document_id", Guid.Parse(invInvoiceAuthId).ToString());

                //    DataSet dataSource = this._nopDbContext2.GetDataSet(rp_code, parameters);

                //    rpBienBan.DataSource = dataSource;
                //    rpBienBan.DataMember = dataSource.Tables[0].TableName;

                //    rpBienBan.CreateDocument();

                //    rpBienBan.PrintingSystem.ContinuousPageNumbering = false;
                //    report.PrintingSystem.ContinuousPageNumbering = false;

                //    report.Pages.AddRange(rpBienBan.Pages);

                //    Page page = report.Pages[report.Pages.Count - 1];
                //    page.AssignWatermark(new PageWatermark());
                //}

                if (tblHoadon.Rows[0]["tthdon"].ToString() == "1" || is_trang_thai_hd == 6)
                {
                    Bitmap bmp = Util.ReportUtil.DrawDiagonalLine(report);
                    report.Watermark.Image = bmp;
                    int pageCount = report.Pages.Count;
                    PageWatermark pmk = new PageWatermark();
                    pmk.Image = bmp;
                    report.Pages[0].AssignWatermark(pmk);
                    //for (int i = 0; i < pageCount; i++)
                    //{
                    //    PageWatermark pmk = new PageWatermark();
                    //    pmk.Image = bmp;
                    //    report.Pages[i].AssignWatermark(pmk);
                    //}
                }

                MemoryStream ms = new MemoryStream();
                #region HTML
                if (type == "Html")
                {
                    report.ExportOptions.Html.EmbedImagesInHTML = true;
                    report.ExportOptions.Html.ExportMode = HtmlExportMode.SingleFilePageByPage;
                    report.ExportOptions.Html.Title = "Hóa đơn điện tử";
                    report.ExportToHtml(ms);

                    string html = Encoding.UTF8.GetString(ms.ToArray());

                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(html);


                    string api = this._webHelper.GetRequest().ApplicationPath.StartsWith("/api") ? "/api" : "";
                    string serverApi = this._webHelper.GetRequest().Url.Scheme + "://" + this._webHelper.GetRequest().Url.Authority + api;

                    var nodes = doc.DocumentNode.SelectNodes("//td/@onmousedown");

                    if (nodes != null)
                    {
                        foreach (HtmlNode node in nodes)
                        {
                            string eventMouseDown = node.Attributes["onmousedown"].Value;

                            if (eventMouseDown.Contains("showCert('seller')"))
                            {
                                node.SetAttributeValue("id", "certSeller");
                            }
                            if (eventMouseDown.Contains("showCert('buyer')"))
                            {
                                node.SetAttributeValue("id", "certBuyer");
                            }
                            if (eventMouseDown.Contains("showCert('vacom')"))
                            {
                                node.SetAttributeValue("id", "certVacom");
                            }
                            if (eventMouseDown.Contains("showCert('minvoice')"))
                            {
                                node.SetAttributeValue("id", "certMinvoice");
                            }
                        }
                    }

                    HtmlNode head = doc.DocumentNode.SelectSingleNode("//head");

                    HtmlNode xmlNode = doc.CreateElement("script");
                    xmlNode.SetAttributeValue("id", "xmlData");
                    xmlNode.SetAttributeValue("type", "text/xmldata");

                    xmlNode.AppendChild(doc.CreateTextNode(xml));
                    head.AppendChild(xmlNode);

                    xmlNode = doc.CreateElement("script");
                    xmlNode.SetAttributeValue("src", serverApi + "/Content/Scripts/jquery-1.6.4.min.js");
                    head.AppendChild(xmlNode);

                    xmlNode = doc.CreateElement("script");
                    xmlNode.SetAttributeValue("src", serverApi + "/Content/Scripts/jquery.signalR-2.2.3.min.js");
                    head.AppendChild(xmlNode);

                    xmlNode = doc.CreateElement("script");
                    xmlNode.SetAttributeValue("type", "text/javascript");

                    xmlNode.InnerHtml = "$(function () { "
                                       + "  var url = 'http://localhost:19898/signalr'; "
                                       + "  var connection = $.hubConnection(url, {  "
                                       + "     useDefaultPath: false "
                                       + "  });"
                                       + " var invoiceHubProxy = connection.createHubProxy('invoiceHub'); "
                                       + " invoiceHubProxy.on('resCommand', function (result) { "
                                       + " }); "
                                       + " connection.start().done(function () { "
                                       + "      console.log('Connect to the server successful');"
                                       + "      $('#certSeller').click(function () { "
                                       + "         var arg = { "
                                       + "              xml: document.getElementById('xmlData').innerHTML, "
                                       + "              id: 'seller' "
                                       + "         }; "
                                       + "         invoiceHubProxy.invoke('execCommand', 'ShowCert', JSON.stringify(arg)); "
                                       + "         }); "
                                       + "      $('#certVacom').click(function () { "
                                       + "         var arg = { "
                                       + "              xml: document.getElementById('xmlData').innerHTML, "
                                       + "              id: 'vacom' "
                                       + "         }; "
                                       + "         invoiceHubProxy.invoke('execCommand', 'ShowCert', JSON.stringify(arg)); "
                                       + "         }); "
                                       + "      $('#certBuyer').click(function () { "
                                       + "         var arg = { "
                                       + "              xml: document.getElementById('xmlData').innerHTML, "
                                       + "              id: 'buyer' "
                                       + "         }; "
                                       + "         invoiceHubProxy.invoke('execCommand', 'ShowCert', JSON.stringify(arg)); "
                                       + "         }); "
                                       + "      $('#certMinvoice').click(function () { "
                                       + "         var arg = { "
                                       + "              xml: document.getElementById('xmlData').innerHTML, "
                                       + "              id: 'minvoice' "
                                       + "         }; "
                                       + "         invoiceHubProxy.invoke('execCommand', 'ShowCert', JSON.stringify(arg)); "
                                       + "         }); "
                                       + "})"
                                       + ".fail(function () { "
                                       + "     alert('failed in connecting to the signalr server'); "
                                       + "});"
                                       + "});";

                    head.AppendChild(xmlNode);

                    if (report.Watermark != null)
                    {
                        if (report.Watermark.Image != null)
                        {
                            ImageConverter _imageConverter = new ImageConverter();
                            byte[] img = (byte[])_imageConverter.ConvertTo(report.Watermark.Image, typeof(byte[]));

                            string imgUrl = "data:image/png;base64," + Convert.ToBase64String(img);

                            HtmlNode style = doc.DocumentNode.SelectSingleNode("//style");

                            string strechMode = report.Watermark.ImageViewMode == ImageViewMode.Stretch ? "background-size: 100% 100%;" : "";
                            string waterMarkClass = ".waterMark { background-image:url(" + imgUrl + ");background-repeat:no-repeat;background-position:center;" + strechMode + " }";

                            HtmlTextNode textNode = doc.CreateTextNode(waterMarkClass);
                            style.AppendChild(textNode);

                            HtmlNode body = doc.DocumentNode.SelectSingleNode("//body");

                            HtmlNodeCollection pageNodes = body.SelectNodes("div");

                            foreach (var pageNode in pageNodes)
                            {
                                pageNode.Attributes.Add("class", "waterMark");

                                string divStyle = pageNode.Attributes["style"].Value;
                                divStyle = divStyle + "margin-left:auto;margin-right:auto;";

                                pageNode.Attributes["style"].Value = divStyle;
                            }
                        }
                    }

                    ms.SetLength(0);
                    doc.Save(ms);

                    doc = null;
                }
                #endregion
                else if (type == "Image")
                {
                    var options = new ImageExportOptions(ImageFormat.Png)
                    {
                        ExportMode = ImageExportMode.SingleFilePageByPage,
                    };
                    report.ExportToImage(ms, options);
                }
                else if (type == "Excel")
                {
                    report.ExportToXlsx(ms);
                }
                else if (type == "Rtf")
                {
                    report.ExportToRtf(ms);
                }
                else
                {
                    report.ExportToPdf(ms);
                }

                bytes = ms.ToArray();
                ms.Close();

                if (bytes == null)
                {
                    throw new Exception("null");
                }

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

            return bytes;
        }
        #endregion

        #region print 32
        private byte[] PrintInvoice(string sobaomat, string masothue, string folder, string type, bool inchuyendoi, out string xml, out string fileNamePrint)
        {
            _nopDbContext2.setConnect(masothue);
            InvoiceDbContext db = _nopDbContext2.GetInvoiceDb();
            byte[] bytes;
            xml = "";
            string msgTb = "";
            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    {"@sobaomat", sobaomat}
                };
                var sql = "SELECT TOP 1 a.* FROM inv_InvoiceAuth AS a INNER JOIN dbo.InvoiceXmlData AS b ON b.inv_InvoiceAuth_id = a.inv_InvoiceAuth_id WHERE a.sobaomat = @sobaomat";
                DataTable tblInvInvoiceAuth = _nopDbContext2.ExecuteCmd(sql, CommandType.Text, parameters);
                if (tblInvInvoiceAuth.Rows.Count == 0)
                {
                    throw new Exception("Không tồn tại hóa đơn có số bảo mật " + sobaomat);
                }
                string invInvoiceAuthId = tblInvInvoiceAuth.Rows[0]["inv_InvoiceAuth_id"].ToString();
                DataTable tblInvoiceXmlData = _nopDbContext2.ExecuteCmd($"SELECT * FROM InvoiceXmlData WHERE inv_InvoiceAuth_id = '{invInvoiceAuthId}'");
                string mauHd = tblInvInvoiceAuth.Rows[0]["mau_hd"].ToString();
                string soSerial = tblInvInvoiceAuth.Rows[0]["inv_invoiceSeries"].ToString();
                string soHd = tblInvInvoiceAuth.Rows[0]["inv_invoiceNumber"].ToString();
                string maDvcs = "VP";
                if (tblInvInvoiceAuth.Columns.Contains("ma_dvcs"))
                {
                    maDvcs = tblInvInvoiceAuth.Rows[0]["ma_dvcs"].ToString();
                }

                fileNamePrint = $"{masothue}_invoice_{mauHd.Trim().Replace("/", "")}_{soSerial.Trim().Replace("/", "")}_{soHd}";
                xml = tblInvoiceXmlData.Rows.Count > 0 ? tblInvoiceXmlData.Rows[0]["data"].ToString() : db.Database.SqlQuery<string>($"EXECUTE sproc_export_XmlInvoice '{invInvoiceAuthId}'").FirstOrDefault();
                string invInvoiceCodeId = tblInvInvoiceAuth.Rows[0]["inv_InvoiceCode_id"].ToString();
                int trangThaiHd = Convert.ToInt32(tblInvInvoiceAuth.Rows[0]["trang_thai_hd"]);
                string invOriginalId = tblInvInvoiceAuth.Rows[0]["inv_originalId"].ToString();
                DataTable tblCtthongbao = _nopDbContext2.ExecuteCmd($"SELECT * FROM ctthongbao a INNER JOIN dpthongbao b ON a.dpthongbao_id = b.dpthongbao_id WHERE a.ctthongbao_id = '{invInvoiceCodeId}'");

                //string hang_nghin = ((tblCtthongbao.Rows[0]["hang_nghin"].ToString() == "") ? "," : tblCtthongbao.Rows[0]["hang_nghin"].ToString());
                //string thap_phan = ((tblCtthongbao.Rows[0]["thap_phan"].ToString() == "") ? "." : tblCtthongbao.Rows[0]["thap_phan"].ToString());
                string hangNghin = ".";
                string thapPhan = ",";
                DataColumnCollection columns = tblCtthongbao.Columns;
                if (columns.Contains("hang_nghin"))
                {
                    hangNghin = tblCtthongbao.Rows[0]["hang_nghin"].ToString();
                }
                if (columns.Contains("thap_phan"))
                {
                    thapPhan = tblCtthongbao.Rows[0]["thap_phan"].ToString();
                }
                if (string.IsNullOrEmpty(hangNghin))
                {
                    hangNghin = ".";
                }
                if (string.IsNullOrEmpty(thapPhan))
                {
                    thapPhan = ",";
                }
                string cacheReportKey = string.Format(Util.CachePattern.INVOICE_REPORT_PATTERN_KEY + "{0}", tblCtthongbao.Rows[0]["dmmauhoadon_id"]);
                XtraReport report;
                DataTable tblDmmauhd = _nopDbContext2.ExecuteCmd($"SELECT * FROM dmmauhoadon WHERE dmmauhoadon_id= '{tblCtthongbao.Rows[0]["dmmauhoadon_id"].ToString()}'");
                string invReport = tblDmmauhd.Rows[0]["report"].ToString();

                if (invReport.Length > 0)
                {
                    report = Util.ReportUtil.LoadReportFromString(invReport);
                    _cacheManager.Set(cacheReportKey, report, 30);
                }
                else
                {
                    throw new Exception("Không tải được mẫu hóa đơn");
                }
                report.Name = "XtraReport1";
                report.ScriptReferencesString = "AccountSignature.dll";
                DataSet ds = new DataSet();
                using (XmlReader xmlReader = XmlReader.Create(new StringReader(report.DataSourceSchema)))
                {
                    ds.ReadXmlSchema(xmlReader);
                    xmlReader.Close();
                }
                using (XmlReader xmlReader = XmlReader.Create(new StringReader(xml)))
                {
                    ds.ReadXml(xmlReader);
                    xmlReader.Close();
                }
                if (ds.Tables.Contains("TblXmlData"))
                {
                    ds.Tables.Remove("TblXmlData");
                }
                DataTable tblXmlData = new DataTable
                {
                    TableName = "TblXmlData"
                };
                tblXmlData.Columns.Add("data");
                DataRow r = tblXmlData.NewRow();
                r["data"] = xml;
                tblXmlData.Rows.Add(r);
                ds.Tables.Add(tblXmlData);
                if (trangThaiHd == 11 || trangThaiHd == 13 || trangThaiHd == 17 || trangThaiHd == 23 && !string.IsNullOrEmpty(invOriginalId))
                {
                    if (!string.IsNullOrEmpty(invOriginalId))
                    {
                        DataTable tblInv = _nopDbContext2.ExecuteCmd($"SELECT * FROM inv_InvoiceAuth WHERE inv_InvoiceAuth_id = '{invOriginalId}'");
                        string invAdjustmentType = tblInv.Rows[0]["inv_adjustmentType"].ToString();
                        string loai = invAdjustmentType == "5" || invAdjustmentType == "19" || invAdjustmentType == "21" || invAdjustmentType == "23" ? "điều chỉnh" : invAdjustmentType == "3" ? "thay thế" : invAdjustmentType == "7" ? "xóa bỏ" : "";
                        if (invAdjustmentType == "5" || invAdjustmentType == "7" || invAdjustmentType == "3" || invAdjustmentType == "19" || invAdjustmentType == "21" || invAdjustmentType == "23")
                        {
                            msgTb = string.Concat("Hóa đơn bị ", loai, " bởi hóa đơn số: ", tblInv.Rows[0]["inv_invoiceNumber"], " ngày ", string.Format("{0:dd/MM/yyyy}", tblInv.Rows[0]["inv_invoiceIssuedDate"]), " mẫu số ", tblInv.Rows[0]["mau_hd"], " ký hiệu", tblInv.Rows[0]["inv_invoiceSeries"]);
                        }
                    }
                }
                if (Convert.ToInt32(tblInvInvoiceAuth.Rows[0]["inv_adjustmentType"]) == 7)
                {
                    msgTb = "";
                }

                if (report.Parameters["MSG_TB"] != null)
                {
                    report.Parameters["MSG_TB"].Value = msgTb;
                }
                XRLabel lblHoaDonMau = report.AllControls<XRLabel>().FirstOrDefault(c => c.Name == "lblHoaDonMau");
                if (lblHoaDonMau != null)
                {
                    lblHoaDonMau.Visible = false;
                }

                if (inchuyendoi)
                {
                    XRTable tblInChuyenDoi = report.AllControls<XRTable>().FirstOrDefault(c => c.Name == "tblInChuyenDoi");
                    if (tblInChuyenDoi != null)
                    {
                        tblInChuyenDoi.Visible = true;
                    }
                    if (report.Parameters["MSG_HD_TITLE"] != null)
                    {
                        report.Parameters["MSG_HD_TITLE"].Value = ((tblInvInvoiceAuth.Rows[0]["ma_ct"].ToString() == "XKNB") ? "Hóa đơn chuyển đổi từ phiếu xuất kho kiêm vạn chuyển nội bộ" : "Hóa đơn chuyển đổi từ hóa đơn điện tử");
                    }
                    if (report.Parameters["NGAY_IN_CDOI"] != null)
                    {
                        report.Parameters["NGAY_IN_CDOI"].Value = DateTime.Now;
                        report.Parameters["NGAY_IN_CDOI"].Visible = true;
                    }
                }
                if (report.Parameters["LINK_TRACUU"] != null)
                {
                    string sqlQrCodeLink = "SELECT TOP 1 * FROM wb_setting WHERE ma = 'QR_CODE_LINK'";
                    DataTable tblQrCodeLink = _nopDbContext2.ExecuteCmd(sqlQrCodeLink);
                    if (tblQrCodeLink.Rows.Count > 0)
                    {
                        string giatri = tblQrCodeLink.Rows[0]["gia_tri"].ToString();
                        if (giatri.Equals("C"))
                        {
                            report.Parameters["LINK_TRACUU"].Value = $"http://{masothue.Trim().Replace("-", "")}.minvoice.com.vn/api/Invoice/Preview?id={invInvoiceAuthId}";
                            report.Parameters["LINK_TRACUU"].Visible = true;
                        }
                    }
                }
                string invCurrencyCode = tblInvInvoiceAuth.Rows[0]["inv_currencyCode"].ToString();
                DataTable tbldmnt = _nopDbContext2.ExecuteCmd($"SELECT * FROM dbo.dmnt	WHERE ma_nt = '{invCurrencyCode}'");

                if (tbldmnt.Rows.Count > 0)
                {
                    if (tbldmnt.Columns.Contains("inv_quantity"))
                    {
                        Util.ReportUtil.CreateParam(report, string.IsNullOrEmpty(tbldmnt.Rows[0]["inv_quantity"].ToString()) ? "0" : tbldmnt.Rows[0]["inv_quantity"].ToString(), "FM_inv_quantity");
                    }
                    if (tbldmnt.Columns.Contains("inv_unitPrice"))
                    {
                        Util.ReportUtil.CreateParam(report, string.IsNullOrEmpty(tbldmnt.Rows[0]["inv_unitPrice"].ToString()) ? "0" : tbldmnt.Rows[0]["inv_unitPrice"].ToString(), "FM_inv_unitPrice");
                    }
                    if (tbldmnt.Columns.Contains("inv_TotalAmountWithoutVat"))
                    {
                        Util.ReportUtil.CreateParam(report, string.IsNullOrEmpty(tbldmnt.Rows[0]["inv_TotalAmountWithoutVat"].ToString()) ? "0" : tbldmnt.Rows[0]["inv_TotalAmountWithoutVat"].ToString(), "FM_inv_TotalAmountWithoutVat");
                    }
                    if (tbldmnt.Columns.Contains("inv_TotalAmount"))
                    {
                        Util.ReportUtil.CreateParam(report, string.IsNullOrEmpty(tbldmnt.Rows[0]["inv_TotalAmount"].ToString()) ? "0" : tbldmnt.Rows[0]["inv_TotalAmount"].ToString(), "FM_inv_TotalAmount");
                    }
                    if (tbldmnt.Columns.Contains("inv_vatAmount"))
                    {
                        Util.ReportUtil.CreateParam(report, string.IsNullOrEmpty(tbldmnt.Rows[0]["inv_vatAmount"].ToString()) ? "0" : tbldmnt.Rows[0]["inv_vatAmount"].ToString(), "FM_inv_vatAmount");
                    }
                }
                report.DataSource = ds;
                Task t = Task.Run(() =>
                {
                    CultureInfo newCulture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
                    newCulture.NumberFormat.NumberDecimalSeparator = thapPhan;
                    newCulture.NumberFormat.NumberGroupSeparator = hangNghin;
                    System.Threading.Thread.CurrentThread.CurrentCulture = newCulture;
                    System.Threading.Thread.CurrentThread.CurrentUICulture = newCulture;
                    report.CreateDocument();
                });
                t.Wait();
                if (tblInvInvoiceAuth.Columns.Contains("inv_sobangke"))
                {
                    if (tblInvInvoiceAuth.Rows[0]["inv_sobangke"].ToString().Length > 0)
                    {
                        string fileName = folder + "\\BangKeDinhKem.repx";
                        XtraReport rpBangKe;
                        if (!File.Exists(fileName))
                        {
                            rpBangKe = new XtraReport();
                            rpBangKe.SaveLayout(fileName);
                        }
                        else
                        {
                            rpBangKe = XtraReport.FromFile(fileName, true);
                        }
                        rpBangKe.ScriptReferencesString = "AccountSignature.dll";
                        rpBangKe.Name = "rpBangKe";
                        rpBangKe.DisplayName = "BangKeDinhKem.repx";
                        rpBangKe.DataSource = report.DataSource;
                        rpBangKe.CreateDocument();
                        report.Pages.AddRange(rpBangKe.Pages);
                    }
                }
                if (tblInvInvoiceAuth.Rows[0]["trang_thai_hd"].ToString() == "7")
                {
                    Bitmap bmp = Util.ReportUtil.DrawDiagonalLine(report);
                    int pageCount = report.Pages.Count;
                    for (int i = 0; i < pageCount; i++)
                    {
                        Page page = report.Pages[i];
                        PageWatermark pmk = new PageWatermark
                        {
                            Image = bmp
                        };
                        page.AssignWatermark(pmk);
                    }
                    string fileName = folder + $@"\{masothue}_BienBanXoaBo.repx";
                    if (!File.Exists(fileName))
                    {
                        fileName = folder + $"\\BienBanXoaBo.repx";
                    }
                    XtraReport rpBienBan = XtraReport.FromFile(fileName, true);
                    rpBienBan.ScriptReferencesString = "AccountSignature.dll";
                    rpBienBan.Name = "rpBienBan";
                    rpBienBan.DisplayName = "BienBanXoaBo.repx";
                    rpBienBan.DataSource = report.DataSource;
                    rpBienBan.DataMember = report.DataMember;
                    rpBienBan.CreateDocument();
                    rpBienBan.PrintingSystem.ContinuousPageNumbering = false;
                    report.PrintingSystem.ContinuousPageNumbering = false;
                    report.Pages.AddRange(rpBienBan.Pages);
                    int idx = pageCount;
                    pageCount = report.Pages.Count;
                    for (int i = idx; i < pageCount; i++)
                    {
                        PageWatermark pmk = new PageWatermark
                        {
                            ShowBehind = false
                        };
                        report.Pages[i].AssignWatermark(pmk);
                    }
                }
                if (trangThaiHd == 13 || trangThaiHd == 17)
                {
                    Bitmap bmp = Util.ReportUtil.DrawDiagonalLine(report);
                    int pageCount = report.Pages.Count;
                    for (int i = 0; i < pageCount; i++)
                    {
                        PageWatermark pmk = new PageWatermark
                        {
                            Image = bmp
                        };
                        report.Pages[i].AssignWatermark(pmk);
                    }
                }

                string sqlCheck =
                    $@"IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'wb_setting') SELECT TOP 1 gia_tri FROM dbo.wb_setting WHERE ma = 'OTP_BIENBAN'";

                DataTable checkOtpBienBan = _nopDbContext2.ExecuteCmd(sqlCheck);

                var otpBienBan = "C";
                if (checkOtpBienBan.Rows.Count > 0)
                {
                    var checkOtpBienBanTable = checkOtpBienBan.Rows[0]["gia_tri"].ToString();
                    if (!string.IsNullOrEmpty(checkOtpBienBanTable))
                    {
                        otpBienBan = checkOtpBienBanTable;
                    }
                }

                if (trangThaiHd == 19 || trangThaiHd == 21 || trangThaiHd == 5 || trangThaiHd == 23)
                {
                    if (otpBienBan.Equals("C"))
                    {
                        string report_file = ((trangThaiHd == 19 || trangThaiHd == 21 || trangThaiHd == 23) ? "INCT_BBDC_GT.repx" : "INCT_BBDC_DD.repx");
                        string sql_sproc = ((trangThaiHd == 19 || trangThaiHd == 21 || trangThaiHd == 23) ? "sproc_inct_hd_dieuchinhgt" : "sproc_inct_hd_dieuchinhdg");
                        string file_name = folder + $@"\{masothue}_{report_file}";

                        if (!File.Exists(file_name))
                        {
                            file_name = folder + $"\\{report_file}";
                        }

                        XtraReport rpBienBan = XtraReport.FromFile(file_name, true);
                        rpBienBan.ScriptReferencesString = "AccountSignature.dll";
                        rpBienBan.Name = "rpBienBanDieuChinh";
                        rpBienBan.DisplayName = file_name;
                        Dictionary<string, string> pars = new Dictionary<string, string>
                        {
                            {"ma_dvcs", maDvcs},
                            {"document_id", invInvoiceAuthId }
                        };

                        DataSet dsDieuChinh = _nopDbContext2.GetDataSet(sql_sproc, pars);

                        rpBienBan.DataSource = dsDieuChinh;
                        rpBienBan.DataMember = dsDieuChinh.Tables[0].TableName;
                        rpBienBan.CreateDocument();
                        rpBienBan.PrintingSystem.ContinuousPageNumbering = false;
                        report.PrintingSystem.ContinuousPageNumbering = false;
                        report.Pages.AddRange(rpBienBan.Pages);

                        int pageCount = report.Pages.Count;
                        report.Pages[pageCount - 1].AssignWatermark(new PageWatermark());
                    }

                }

                if (trangThaiHd == 3)
                {
                    if (otpBienBan.Equals("C"))
                    {
                        string reportFileThayThe = "INCT_BBTT.repx";
                        string sqlThayThe = "sproc_inct_hd_thaythe";
                        string fileName = folder + $@"\{masothue}_{reportFileThayThe}";

                        if (!File.Exists(fileName))
                        {
                            fileName = folder + $"\\{reportFileThayThe}";
                        }

                        XtraReport rpBienBanThayThe = XtraReport.FromFile(fileName, true);
                        rpBienBanThayThe.ScriptReferencesString = "AccountSignature.dll";
                        rpBienBanThayThe.Name = "rpBienBanThayThe";
                        rpBienBanThayThe.DisplayName = reportFileThayThe;
                        Dictionary<string, string> pars = new Dictionary<string, string>
                        {
                            {"ma_dvcs", maDvcs},
                            {"document_id", invInvoiceAuthId }
                        };

                        DataSet dsThayThe = _nopDbContext2.GetDataSet(sqlThayThe, pars);

                        rpBienBanThayThe.DataSource = dsThayThe;
                        rpBienBanThayThe.DataMember = dsThayThe.Tables[0].TableName;
                        rpBienBanThayThe.CreateDocument();
                        rpBienBanThayThe.PrintingSystem.ContinuousPageNumbering = false;
                        report.PrintingSystem.ContinuousPageNumbering = false;
                        report.Pages.AddRange(rpBienBanThayThe.Pages);

                        int pageCount = report.Pages.Count;
                        report.Pages[pageCount - 1].AssignWatermark(new PageWatermark());
                    }
                }

                MemoryStream ms = new MemoryStream();
                if (type == "Html")
                {
                    report.ExportOptions.Html.EmbedImagesInHTML = true;
                    report.ExportOptions.Html.ExportMode = HtmlExportMode.SingleFilePageByPage;
                    report.ExportOptions.Html.Title = "Hóa đơn điện tử M-Invoice";
                    report.ExportToHtml(ms);
                    string html = Encoding.UTF8.GetString(ms.ToArray());
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//td/@onmousedown");
                    if (nodes != null)
                    {
                        foreach (HtmlNode node in nodes)
                        {
                            string eventMouseDown = node.Attributes["onmousedown"].Value;
                            if (eventMouseDown.Contains("showCert('seller')"))
                            {
                                node.SetAttributeValue("id", "certSeller");
                            }
                            if (eventMouseDown.Contains("showCert('buyer')"))
                            {
                                node.SetAttributeValue("id", "certBuyer");
                            }
                            if (eventMouseDown.Contains("showCert('vacom')"))
                            {
                                node.SetAttributeValue("id", "certVacom");
                            }
                            if (eventMouseDown.Contains("showCert('minvoice')"))
                            {
                                node.SetAttributeValue("id", "certMinvoice");
                            }
                        }
                    }
                    HtmlNode head = doc.DocumentNode.SelectSingleNode("//head");
                    HtmlNode xmlNode = doc.CreateElement("script");
                    xmlNode.SetAttributeValue("id", "xmlData");
                    xmlNode.SetAttributeValue("type", "text/xmldata");
                    xmlNode.AppendChild(doc.CreateTextNode(xml));
                    head.AppendChild(xmlNode);
                    if (report.Watermark?.Image != null)
                    {
                        ImageConverter imageConverter = new ImageConverter();
                        byte[] img = (byte[])imageConverter.ConvertTo(report.Watermark.Image, typeof(byte[]));
                        string imgUrl = "data:image/png;base64," + Convert.ToBase64String(img);
                        HtmlNode style = doc.DocumentNode.SelectSingleNode("//style");
                        string strechMode = report.Watermark.ImageViewMode == ImageViewMode.Stretch ? "background-size: 100% 100%;" : "";
                        string waterMarkClass = ".waterMark { background-image:url(" + imgUrl + ");background-repeat:no-repeat;background-position:center;" + strechMode + " }";
                        HtmlTextNode textNode = doc.CreateTextNode(waterMarkClass);
                        style.AppendChild(textNode);
                        HtmlNode body = doc.DocumentNode.SelectSingleNode("//body");
                        HtmlNodeCollection pageNodes = body.SelectNodes("div");
                        foreach (HtmlNode pageNode in pageNodes)
                        {
                            pageNode.Attributes.Add("class", "waterMark");
                            string divStyle = pageNode.Attributes["style"].Value;
                            divStyle = divStyle + "margin-left:auto;margin-right:auto;";
                            pageNode.Attributes["style"].Value = divStyle;
                        }
                    }
                    ms.SetLength(0);
                    doc.Save(ms);
                }
                else if (type == "Image")
                {
                    ImageExportOptions options = new ImageExportOptions(ImageFormat.Png)
                    {
                        ExportMode = ImageExportMode.SingleFilePageByPage,
                    };
                    report.ExportToImage(ms, options);
                }
                else
                {
                    report.ExportToPdf(ms);
                }
                bytes = ms.ToArray();
                ms.Close();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            return bytes;
        }
        #endregion

        #region XML 78
        //kết xuất XML 78
        public byte[] ExportXmlInvoieMulty(string sobaomat, string masothue, string pathReport, ref string fileName, ref string key)
        {
            _nopDbContext2.setConnect(masothue);
            Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    {"@sobaomat", sobaomat}
                };
            string checkTable = "SELECT * FROM INFORMATION_SCHEMA.TABLES where table_name ='wb_setting'";
            string mahoa = "SELECT gia_tri FROM dbo.wb_setting WHERE ma = 'MA_HOA_XML' AND gia_tri ='C'";
            string sql = "SELECT TOP 1 a.* FROM inv_InvoiceAuth AS a INNER JOIN dbo.InvoiceXmlData AS b ON b.inv_InvoiceAuth_id = a.inv_InvoiceAuth_id WHERE a.sobaomat = @sobaomat";
            DataTable dt = _nopDbContext2.ExecuteCmd(sql, CommandType.Text, parameters);

            try
            {
                string sql78 = "SELECT * FROM dbo.hoadon68 a INNER JOIN dbo.hoadon68_xml b ON b.hoadon68_id = a.hoadon68_id WHERE a.sbmat=@sobaomat";
                DataTable dt78 = _nopDbContext2.ExecuteCmd(sql78, CommandType.Text, parameters);

                DataTable check_loai_hd = this._nopDbContext2.ExecuteCmd("SELECT a.hthuc FROM dbo.quanlykyhieu68 a INNER JOIN dbo.hoadon68 b ON a.quanlykyhieu68_id = b.cctbao_id WHERE b.hoadon68_id = '" + dt78.Rows[0]["hoadon68_id"] + "'");
                string hinh_thuc = check_loai_hd.Rows[0]["hthuc"].ToString();

                if (hinh_thuc == "K")
                {
                    string query78 = "SELECT b.ghichu FROM dbo.hoadon68 a LEFT JOIN dbo.wb_log b ON a.matdiep=b.fields3 WHERE a.sbmat=@sobaomat AND b.fields2='204'";
                    DataTable datatabe78 = _nopDbContext2.ExecuteCmd(query78, CommandType.Text, parameters);
                    string ghichu = datatabe78.Rows[0]["ghichu"].ToString();
                    JObject ghichu1 = new JObject();






                    ghichu1 = JObject.Parse(ghichu).ToObject<JObject>();


                    if (datatabe78.Rows.Count <= 0)
                    {
                        return null;
                    }
                    var withoutSpecial = new string(sobaomat.Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)).ToArray());
                    if (sobaomat != withoutSpecial)
                    {
                        throw new Exception("Chuỗi số bảo mật không đúng định dạng ");
                    }


                    fileName = "Invoice";
                    Guid keyxml = Guid.NewGuid();
                    string xml = ghichu1["xml"].ToString();



                    XmlDocument xmlDocument = new XmlDocument(); // đọc file xml
                    xmlDocument.PreserveWhitespace = true;// bảo quản khoảng trăng
                    xmlDocument.LoadXml(xml);

                    string ltbao = xmlDocument.GetElementsByTagName("LTBao")[0].InnerText.ToString();
                    if (ltbao == "2")
                    {
                        xml = xml;
                        bool checkMaHoaXml = false;
                        DataTable CheckTable = _nopDbContext2.ExecuteCmd(checkTable, CommandType.Text, parameters);

                        if (CheckTable.Rows.Count > 0)
                        {
                            DataTable mahoaxml = _nopDbContext2.ExecuteCmd(mahoa, CommandType.Text, parameters);
                            if (mahoaxml.Rows.Count > 0)
                            {
                                xml = Util.EncodeXML.Encrypt(xml.ToString(), keyxml.ToString());
                                checkMaHoaXml = true;
                            }

                        }
                        MemoryStream outputMemStream = new MemoryStream();
                        ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);
                        zipStream.SetLevel(3);
                        ZipEntry newEntry = new ZipEntry(masothue + ".xml")
                        {
                            DateTime = DateTime.Now,
                            IsUnicodeText = true
                        };
                        zipStream.PutNextEntry(newEntry);
                        byte[] _keyxml = Encoding.UTF8.GetBytes(keyxml.ToString());
                        byte[] bytes = Encoding.UTF8.GetBytes(xml);

                        MemoryStream inStream = new MemoryStream(bytes);
                        inStream.WriteTo(zipStream);
                        inStream.Close();
                        zipStream.CloseEntry();
                        inStream = new MemoryStream();

                        if (CheckTable.Rows.Count > 0 && checkMaHoaXml)
                        {
                            newEntry = new ZipEntry("key.txt")
                            {
                                DateTime = DateTime.Now,
                                IsUnicodeText = true
                            };
                            zipStream.PutNextEntry(newEntry);

                            inStream = new MemoryStream(_keyxml);
                            inStream.WriteTo(zipStream);
                            inStream.Close();
                            zipStream.CloseEntry();
                        }
                        zipStream.IsStreamOwner = false;
                        zipStream.Close();
                        outputMemStream.Position = 0;
                        var result = outputMemStream.ToArray();
                        outputMemStream.Close();
                        return result;
                    }




                }
                else
                {
                    if (dt.Rows.Count == 0)
                    {
                        string query78 = "SELECT b.ghichu FROM dbo.hoadon68 a LEFT JOIN dbo.wb_log b ON a.matdiep=b.fields3 WHERE a.sbmat=@sobaomat AND b.fields2='202'";
                        DataTable datatabe78 = _nopDbContext2.ExecuteCmd(query78, CommandType.Text, parameters);
                        string ghichu = datatabe78.Rows[0]["ghichu"].ToString();
                        JObject ghichu1 = new JObject();





                        ghichu1 = JObject.Parse(ghichu).ToObject<JObject>();


                        if (datatabe78.Rows.Count <= 0)
                        {
                            return null;
                        }
                        var withoutSpecial = new string(sobaomat.Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)).ToArray());
                        if (sobaomat != withoutSpecial)
                        {
                            throw new Exception("Chuỗi số bảo mật không đúng định dạng ");
                        }


                        fileName = "Invoice";
                        Guid keyxml = Guid.NewGuid();
                        string xml = ghichu1["xml"].ToString();
                        xml = xml;
                        bool checkMaHoaXml = false;
                        DataTable CheckTable = _nopDbContext2.ExecuteCmd(checkTable, CommandType.Text, parameters);

                        if (CheckTable.Rows.Count > 0)
                        {
                            DataTable mahoaxml = _nopDbContext2.ExecuteCmd(mahoa, CommandType.Text, parameters);
                            if (mahoaxml.Rows.Count > 0)
                            {
                                xml = Util.EncodeXML.Encrypt(xml.ToString(), keyxml.ToString());
                                checkMaHoaXml = true;
                            }

                        }
                        MemoryStream outputMemStream = new MemoryStream();
                        ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);
                        zipStream.SetLevel(3);
                        ZipEntry newEntry = new ZipEntry(masothue + ".xml")
                        {
                            DateTime = DateTime.Now,
                            IsUnicodeText = true
                        };
                        zipStream.PutNextEntry(newEntry);
                        byte[] _keyxml = Encoding.UTF8.GetBytes(keyxml.ToString());
                        byte[] bytes = Encoding.UTF8.GetBytes(xml);

                        MemoryStream inStream = new MemoryStream(bytes);
                        inStream.WriteTo(zipStream);
                        inStream.Close();
                        zipStream.CloseEntry();
                        inStream = new MemoryStream();
                        //using (StreamWriter sw = new StreamWriter(inStream))
                        //{
                        //    sw.Write(tblMauHoaDon.Rows[0]["dulieumau"].ToString());
                        //    sw.Flush();
                        //    newEntry = new ZipEntry("invoice.repx")
                        //    {
                        //        DateTime = DateTime.Now,
                        //        IsUnicodeText = true
                        //    };
                        //    zipStream.PutNextEntry(newEntry);
                        //    inStream.WriteTo(zipStream);
                        //    inStream.Close();
                        //    zipStream.CloseEntry();
                        //    sw.Close();
                        //}

                        if (CheckTable.Rows.Count > 0 && checkMaHoaXml)
                        {
                            newEntry = new ZipEntry("key.txt")
                            {
                                DateTime = DateTime.Now,
                                IsUnicodeText = true
                            };
                            zipStream.PutNextEntry(newEntry);

                            inStream = new MemoryStream(_keyxml);
                            inStream.WriteTo(zipStream);
                            inStream.Close();
                            zipStream.CloseEntry();
                        }
                        zipStream.IsStreamOwner = false;
                        zipStream.Close();
                        outputMemStream.Position = 0;
                        var result = outputMemStream.ToArray();
                        outputMemStream.Close();
                        return result;

                    }
                    else if (dt.Rows.Count != 0)
                    {
                        var withoutSpecial = new string(sobaomat.Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)).ToArray());
                        if (sobaomat != withoutSpecial)
                        {
                            throw new Exception("Chuỗi số bảo mật không đúng định dạng ");
                        }
                        string mau_hd = dt.Rows[0]["mau_hd"].ToString();
                        string Serial_num = dt.Rows[0]["inv_invoiceSeries"].ToString();
                        string so_hd = dt.Rows[0]["inv_invoiceNumber"].ToString();
                        string InvoiceCodeId = dt.Rows[0]["inv_InvoiceCode_id"].ToString();
                        fileName = $"{masothue}_invoice_{ mau_hd.Trim().Replace("/", "")}_{Serial_num.Trim().Replace("/", "")}_{so_hd}";
                        Guid InvoiceAuthId = Guid.Parse(dt.Rows[0]["inv_InvoiceAuth_id"].ToString());
                        DataTable tblInvoiceXmlData = _nopDbContext2.ExecuteCmd($"SELECT * FROM InvoiceXmlData WHERE inv_InvoiceAuth_id = '{InvoiceAuthId}'");
                        if (tblInvoiceXmlData.Rows.Count == 0)
                        {
                            return null;
                        }
                        DataTable tblCtthongbao = _nopDbContext2.ExecuteCmd($"SELECT * FROM ctthongbao WHERE ctthongbao_id = '{InvoiceCodeId}'");
                        DataTable tblMauHoaDon = _nopDbContext2.ExecuteCmd($"SELECT dmmauhoadon_id, report FROM dmmauhoadon WHERE dmmauhoadon_id = '{tblCtthongbao.Rows[0]["dmmauhoadon_id"].ToString()}'");
                        Guid keyxml = Guid.NewGuid();
                        string xml = tblInvoiceXmlData.Rows[0]["data"].ToString();
                        xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>" + xml;

                        bool checkMaHoaXml = false;
                        DataTable CheckTable = _nopDbContext2.ExecuteCmd(checkTable, CommandType.Text, parameters);

                        if (CheckTable.Rows.Count > 0)
                        {
                            DataTable mahoaxml = _nopDbContext2.ExecuteCmd(mahoa, CommandType.Text, parameters);
                            if (mahoaxml.Rows.Count > 0)
                            {
                                xml = Util.EncodeXML.Encrypt(xml.ToString(), keyxml.ToString());
                                checkMaHoaXml = true;
                            }

                        }
                        MemoryStream outputMemStream = new MemoryStream();
                        ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);
                        zipStream.SetLevel(3);
                        ZipEntry newEntry = new ZipEntry(masothue + ".xml")
                        {
                            DateTime = DateTime.Now,
                            IsUnicodeText = true
                        };
                        zipStream.PutNextEntry(newEntry);
                        byte[] _keyxml = Encoding.UTF8.GetBytes(keyxml.ToString());
                        byte[] bytes = Encoding.UTF8.GetBytes(xml);

                        MemoryStream inStream = new MemoryStream(bytes);
                        inStream.WriteTo(zipStream);
                        inStream.Close();
                        zipStream.CloseEntry();
                        inStream = new MemoryStream();
                        using (StreamWriter sw = new StreamWriter(inStream))
                        {
                            sw.Write(tblMauHoaDon.Rows[0]["report"].ToString());
                            sw.Flush();
                            newEntry = new ZipEntry("invoice.repx")
                            {
                                DateTime = DateTime.Now,
                                IsUnicodeText = true
                            };
                            zipStream.PutNextEntry(newEntry);
                            inStream.WriteTo(zipStream);
                            inStream.Close();
                            zipStream.CloseEntry();
                            sw.Close();
                        }

                        if (CheckTable.Rows.Count > 0 && checkMaHoaXml)
                        {
                            newEntry = new ZipEntry("key.txt")
                            {
                                DateTime = DateTime.Now,
                                IsUnicodeText = true
                            };
                            zipStream.PutNextEntry(newEntry);

                            inStream = new MemoryStream(_keyxml);
                            inStream.WriteTo(zipStream);
                            inStream.Close();
                            zipStream.CloseEntry();
                        }
                        zipStream.IsStreamOwner = false;
                        zipStream.Close();
                        outputMemStream.Position = 0;
                        var result = outputMemStream.ToArray();
                        outputMemStream.Close();
                        return result;

                    }
                    else
                    {
                        return null;
                    }
                }




            }
            catch (Exception ex)
            {
                if (dt.Rows.Count != 0)
                {
                    var withoutSpecial = new string(sobaomat.Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)).ToArray());
                    if (sobaomat != withoutSpecial)
                    {
                        throw new Exception("Chuỗi số bảo mật không đúng định dạng ");
                    }
                    string mau_hd = dt.Rows[0]["mau_hd"].ToString();
                    string Serial_num = dt.Rows[0]["inv_invoiceSeries"].ToString();
                    string so_hd = dt.Rows[0]["inv_invoiceNumber"].ToString();
                    string InvoiceCodeId = dt.Rows[0]["inv_InvoiceCode_id"].ToString();
                    fileName = $"{masothue}_invoice_{ mau_hd.Trim().Replace("/", "")}_{Serial_num.Trim().Replace("/", "")}_{so_hd}";
                    Guid InvoiceAuthId = Guid.Parse(dt.Rows[0]["inv_InvoiceAuth_id"].ToString());
                    DataTable tblInvoiceXmlData = _nopDbContext2.ExecuteCmd($"SELECT * FROM InvoiceXmlData WHERE inv_InvoiceAuth_id = '{InvoiceAuthId}'");
                    if (tblInvoiceXmlData.Rows.Count == 0)
                    {
                        return null;
                    }
                    DataTable tblCtthongbao = _nopDbContext2.ExecuteCmd($"SELECT * FROM ctthongbao WHERE ctthongbao_id = '{InvoiceCodeId}'");
                    DataTable tblMauHoaDon = _nopDbContext2.ExecuteCmd($"SELECT dmmauhoadon_id, report FROM dmmauhoadon WHERE dmmauhoadon_id = '{tblCtthongbao.Rows[0]["dmmauhoadon_id"].ToString()}'");
                    Guid keyxml = Guid.NewGuid();
                    string xml = tblInvoiceXmlData.Rows[0]["data"].ToString();
                    xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>" + xml;

                    bool checkMaHoaXml = false;
                    DataTable CheckTable = _nopDbContext2.ExecuteCmd(checkTable, CommandType.Text, parameters);

                    if (CheckTable.Rows.Count > 0)
                    {
                        DataTable mahoaxml = _nopDbContext2.ExecuteCmd(mahoa, CommandType.Text, parameters);
                        if (mahoaxml.Rows.Count > 0)
                        {
                            xml = Util.EncodeXML.Encrypt(xml.ToString(), keyxml.ToString());
                            checkMaHoaXml = true;
                        }

                    }
                    MemoryStream outputMemStream = new MemoryStream();
                    ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);
                    zipStream.SetLevel(3);
                    ZipEntry newEntry = new ZipEntry(masothue + ".xml")
                    {
                        DateTime = DateTime.Now,
                        IsUnicodeText = true
                    };
                    zipStream.PutNextEntry(newEntry);
                    byte[] _keyxml = Encoding.UTF8.GetBytes(keyxml.ToString());
                    byte[] bytes = Encoding.UTF8.GetBytes(xml);

                    MemoryStream inStream = new MemoryStream(bytes);
                    inStream.WriteTo(zipStream);
                    inStream.Close();
                    zipStream.CloseEntry();
                    inStream = new MemoryStream();
                    using (StreamWriter sw = new StreamWriter(inStream))
                    {
                        sw.Write(tblMauHoaDon.Rows[0]["report"].ToString());
                        sw.Flush();
                        newEntry = new ZipEntry("invoice.repx")
                        {
                            DateTime = DateTime.Now,
                            IsUnicodeText = true
                        };
                        zipStream.PutNextEntry(newEntry);
                        inStream.WriteTo(zipStream);
                        inStream.Close();
                        zipStream.CloseEntry();
                        sw.Close();
                    }

                    if (CheckTable.Rows.Count > 0 && checkMaHoaXml)
                    {
                        newEntry = new ZipEntry("key.txt")
                        {
                            DateTime = DateTime.Now,
                            IsUnicodeText = true
                        };
                        zipStream.PutNextEntry(newEntry);

                        inStream = new MemoryStream(_keyxml);
                        inStream.WriteTo(zipStream);
                        inStream.Close();
                        zipStream.CloseEntry();
                    }
                    zipStream.IsStreamOwner = false;
                    zipStream.Close();
                    outputMemStream.Position = 0;
                    var result = outputMemStream.ToArray();
                    outputMemStream.Close();
                    return result;

                }
                else
                {
                    return null;
                }
            }


            return null;
        }
        #endregion
    }
}