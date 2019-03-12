using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using umbraco.NodeFactory;
using Umbraco.Web.WebApi;
using Umbraco.Core;
using Umbraco.Web.Extensions;
using Umbraco.Web;
using System.Web.Mvc;
using System.Runtime.Caching;
using System.Collections;
using System.Web;

/// <summary>
/// Attribute that can be added to controller methods to force content
/// to be GZip encoded if the client supports it
/// </summary>
public class CompressContentAttribute : ActionFilterAttribute
{

    /// <summary>
    /// Override to compress the content that is generated by
    /// an action method.
    /// </summary>
    /// <param name="filterContext"></param>
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        GZipEncodePage();
    }

    /// <summary>
    /// Determines if GZip is supported
    /// </summary>
    /// <returns></returns>
    public static bool IsGZipSupported()
    {
        string AcceptEncoding = HttpContext.Current.Request.Headers["Accept-Encoding"];
        if (!string.IsNullOrEmpty(AcceptEncoding) &&
                (AcceptEncoding.Contains("gzip") || AcceptEncoding.Contains("deflate")))
            return true;
        return false;
    }

    /// <summary>
    /// Sets up the current page or handler to use GZip through a Response.Filter
    /// IMPORTANT:  
    /// You have to call this method before any output is generated!
    /// </summary>
    public static void GZipEncodePage()
    {
        HttpResponse Response = HttpContext.Current.Response;

        if (IsGZipSupported())
        {
            string AcceptEncoding = HttpContext.Current.Request.Headers["Accept-Encoding"];

            //if (AcceptEncoding.Contains("br") || AcceptEncoding.Contains("brotli"))
            //{
            //    Response.Filter = new Brotli.BrotliStream(baseStream, System.IO.Compression.CompressionMode.Compress);
            //    Response.AppendHeader("Content-Encoding", "br");
            //}
            if (AcceptEncoding.Contains("gzip"))
            {
                Response.Filter = new System.IO.Compression.GZipStream(Response.Filter,
                                            System.IO.Compression.CompressionMode.Compress);
                Response.Headers.Remove("Content-Encoding");
                Response.AppendHeader("Content-Encoding", "gzip");
            }
            else
            {
                Response.Filter = new System.IO.Compression.DeflateStream(Response.Filter,
                                            System.IO.Compression.CompressionMode.Compress);
                Response.Headers.Remove("Content-Encoding");
                Response.AppendHeader("Content-Encoding", "deflate");
            }


        }

        // Allow proxy servers to cache encoded and unencoded versions separately
        Response.AppendHeader("Vary", "Content-Encoding");
    }
}
[CompressContent]
public class JSONController : UmbracoApiController
{
   [CompressContent]
    // GET api/ContentController/get/id
    public object GetAll(string id)
    {
        var idstring = id.Replace(",", "");
        var cache = MemoryCache.Default;
        var listItems = MemoryCache.Default.Get("LIST" + idstring);


        var rootNode = Umbraco.TypedContentAtRoot().First();
        // Setter standard inputNode - dette er rotnoden for innhold
        Node inputNode = new Node(rootNode.Id);

        // Sjekker om id er nummer eller ikke.
        var isListe = id.IndexOf(",");
        int n;
        bool isNumeric = int.TryParse(id, out n);
        if (isListe != -1)
        {
            string[] idliste = id.Split(',');
            isNumeric = int.TryParse(idliste[0], out n);
        }
        // Om input er numeric - bruk verdi
        if (isNumeric)
        {
            inputNode = new Node(n);
        }
        // Lager en dynamisk, anonym - nodeliste
        var nodeListe = new List<object>();

        // Om den skal se etter innholdstype eller ikke
        if (!isNumeric)
        {
            var contentType = ApplicationContext.Services.ContentTypeService.GetContentType(id);
            var contentPages = ApplicationContext.Services.ContentService.GetContentOfContentType(contentType.Id).OrderBy(x => x.SortOrder).Where(page => page.Trashed == false && page.Published == true);

            foreach (var item in contentPages)
            {
                Node childNode = new Node(item.Id);
                nodeListe.Add(single(childNode, false));
            }
        }
        else
        {
            if (isListe != -1)
            {
                if (listItems == null)
                {
                    string[] noder = id.Split(',');
                    var listeavnoder = new Dictionary<string, List<object>>();
                    foreach (string type in noder)
                    {
                        var typeId = Int32.Parse(type);
                        var contentPages = ApplicationContext.Services.ContentService.GetChildren(typeId).OrderBy(x => x.SortOrder).Where(page => page.Trashed == false && page.Published == true);
                        listeavnoder.Add(type, new List<object>());

                        foreach (var item in contentPages)
                        {
                            Node childNode = new Node(item.Id);
                            if(childNode.NodeTypeAlias == "nettskypakke") {
                                listeavnoder[type].Add(single(childNode, true));
                            }
                            else {
                            listeavnoder[type].Add(single(childNode, false));
                            }
                        }

                    }
                    CacheItemPolicy policy = new CacheItemPolicy();
                    policy.AbsoluteExpiration = DateTime.Now.AddDays(14); //14 dager
                    cache.Set("LIST" + idstring, listeavnoder, policy);
                    return Json(listeavnoder);
                }
                else
                {
                    return Json(MemoryCache.Default.Get("LIST" + idstring));
                }

            }
            // Om noden har barn
            if (inputNode.Children.Count > 0)
            {
                var contentPages = ApplicationContext.Services.ContentService.GetChildren(inputNode.Id).OrderBy(x => x.SortOrder).Where(page => page.Trashed == false && page.Published == true);

                foreach (var item in contentPages)
                {
                    Node childNode = new Node(item.Id);
                    nodeListe.Add(single(childNode, false));
                }
            }
            else
            {
                return Json(single(inputNode, false));
            }
        }
        return Json(nodeListe);
    }
   [CompressContent]
    public object GetList(string id)
    {
        var rootNode = Umbraco.TypedContentAtRoot().First();
        // Setter standard inputNode - dette er rotnoden for innhold
        Node inputNode = new Node(rootNode.Id);

        // Sjekker om id er nummer eller ikke.
        int n;
        bool isNumeric = int.TryParse(id, out n);

        // Om input er numeric - bruk verdi
        if (isNumeric)
        {
            inputNode = new Node(n);
        }
        // Lager en dynamisk, anonym - nodeliste
        var nodeListe = new List<object>();

        // SJekker om input er array
        var isListe = id.IndexOf(",");

        // Om den skal se etter innholdstype eller ikke
        if (!isNumeric)
        {
            if (isListe != -1)
            {
                string[] innholdstyper = id.Split(',');
                var innholdstype = new Dictionary<string, List<object>>();
                foreach (string type in innholdstyper)
                {
                    var contentType = ApplicationContext.Services.ContentTypeService.GetContentType(type);
                    var contentPages = ApplicationContext.Services.ContentService.GetContentOfContentType(contentType.Id).OrderBy(x => x.SortOrder).Where(page => page.Trashed == false && page.Published == true);
                    innholdstype.Add(type, new List<object>());

                    foreach (var item in contentPages)
                    {
                        Node childNode = new Node(item.Id);
                        innholdstype[type].Add(singleInfo(childNode));
                    }

                }
                return Json(innholdstype);
            }
            else
            {
                var contentType = ApplicationContext.Services.ContentTypeService.GetContentType(id);
                var contentPages = ApplicationContext.Services.ContentService.GetContentOfContentType(contentType.Id).OrderBy(x => x.SortOrder).Where(page => page.Trashed == false && page.Published == true);

                foreach (var item in contentPages)
                {
                    Node childNode = new Node(item.Id);
                    nodeListe.Add(singleInfo(childNode));
                }
            }

        }
        else
        {
            // Om noden har barn
            if (inputNode.Children.Count > 0)
            {
                var contentPages = ApplicationContext.Services.ContentService.GetChildren(inputNode.Id).OrderBy(x => x.SortOrder).Where(page => page.Trashed == false && page.Published == true);

                foreach (var item in contentPages)
                {
                    Node childNode = new Node(item.Id);
                    nodeListe.Add(singleInfo(childNode));
                }
            }
            else
            {
                return Json(singleInfo(inputNode));
            }
        }
        return Json(nodeListe);
    }
    // Bruk Get om noden har barn men man fortsatt vil vise info
   [CompressContent]
    public object Get(int id, bool visbarn = false)
    {
        Node child = new Node(id);

        if (child == null || child.Name == null)
        {
            throw new HttpResponseException(HttpStatusCode.NotFound);
        }

        return Json(single(child, visbarn));
    }
   [CompressContent]
    private Dictionary<string, object> singleInfo(Node child)
    {
        var dynNodeData = new Dictionary<string, object>();
        dynNodeData["Type"] = child.NodeTypeAlias;
        dynNodeData["url"] = child.UrlName;
        dynNodeData["NodeName"] = child.Name;
        dynNodeData["NodeId"] = child.Id;
        //dynNodeData["OpprettetDato"] = child.CreateDate.ToString("dd.MM.yyyy");
        //dynNodeData["OpprettetAv"] = child.WriterName;
        return dynNodeData;
    }
   [CompressContent]
    public object GetTree(int id)
    {
        Node inputNode = new Node(id);

        if (inputNode == null) return "";

        Dictionary<string, object> first = single(inputNode, false);

        if (inputNode.Children.Count > 0)
        {
            first["children"] = RecursiveNodeTree(inputNode, first);
        }


        return Json(first);
    }
   [CompressContent]
    public object GetTreeList(int id)
    {
        var cache = MemoryCache.Default;
        var listItems = MemoryCache.Default.Get("TLIST" + id);
        if (listItems == null)
        {
            Node inputNode = new Node(id);

            if (inputNode == null) return "";

            Dictionary<string, object> first = single(inputNode, false);

            if (inputNode.Children.Count > 0)
            {
                first["children"] = RecursiveNodeTree(inputNode, first);
            }

            CacheItemPolicy policy = new CacheItemPolicy();
            policy.AbsoluteExpiration = DateTime.Now.AddDays(14); //14 dager
            cache.Set("TLIST" + id, first, policy);

            return Json(first);
        }
        else
        {
            return Json(MemoryCache.Default.Get("TLIST" + id));
        }


    }
   [CompressContent]
    private List<Object> RecursiveNodeTree(Node CurrentNode, Dictionary<string, object> CurrentDict)
    {

        List<Object> CurrentList = new List<object>();
        if (CurrentNode.Children.Count > 0)
        {

            var contentPages = ApplicationContext.Services.ContentService.GetChildren(CurrentNode.Id).OrderBy(x => x.SortOrder).Where(page => page.Trashed == false && page.Published == true);
            foreach (var item in contentPages)
            {

                Node childNode = new Node(item.Id);

                Dictionary<string, object> childDict = single(childNode, false);

                if (childNode.Children.Count > 0)
                {
                    childDict["children"] = RecursiveNodeTree(childNode, childDict);
                }

                CurrentList.Add(childDict);
            }
        }
        else
        {
            CurrentList.Add(single(CurrentNode, false));
        }

        return CurrentList;

    }
   [CompressContent]
    private List<Object> RecursiveNodeTreeList(Node CurrentNode, Dictionary<string, object> CurrentDict)
    {

        List<Object> CurrentList = new List<object>();
        if (CurrentNode.Children.Count > 0)
        {

            var contentPages = ApplicationContext.Services.ContentService.GetChildren(CurrentNode.Id).OrderBy(x => x.SortOrder).Where(page => page.Trashed == false && page.Published == true);
            foreach (var item in contentPages)
            {

                Node childNode = new Node(item.Id);

                Dictionary<string, object> childDict = singleInfo(childNode);

                if (childNode.Children.Count > 0)
                {
                    childDict["children"] = RecursiveNodeTreeList(childNode, childDict);
                }

                CurrentList.Add(childDict);
            }
        }
        else
        {
            CurrentList.Add(singleInfo(CurrentNode));
        }

        return CurrentList;

    }
   [CompressContent]
    private Dictionary<string, object> single(Node child, bool viseBarn)
    {
        if(child.NodeTypeAlias == "nettskypakker")
        {
            viseBarn = true;
        }
        var cache = MemoryCache.Default;
        var cacheItems = MemoryCache.Default.Get("NODE" + child.Id);
        var dynNodeData = new Dictionary<string, object>();
        var barn = new List<object> { };
        var Bilder = new List<object> { };
        var Ikoner = new List<object> { };
        var Filer = new List<object> { };
        var Tjenester = new List<object> { };
        var Nettskypakker = new List<object> { };


        if (child.Children.Count > 0 && viseBarn)
        {
            var contentPages = ApplicationContext.Services.ContentService.GetChildren(child.Id).OrderBy(x => x.SortOrder).Where(page => page.Trashed == false && page.Published == true);
            foreach (var item in contentPages)
            {
                Node childNode = new Node(item.Id);
                barn.Add(single(childNode, false));
            }
            dynNodeData["barn"] = barn;
        }

        if (cacheItems == null)
        {
            if (child.GetProperty("bilder") != null)
            {
                string[] splitted = child.GetProperty("bilder").Value.ToString().Split(',');
                foreach (string split in splitted)
                {
                    //var mediaId = Udi.Parse(split).ToPublishedContent();
                    var mediaId = Udi.Parse(split).ToPublishedContent();
                    var item_node = new umbraco.MacroEngines.DynamicNode(mediaId);
                    var nyttBilde = new Dictionary<string, object>();

                    foreach (var verdi in item_node.PropertiesAsList)
                    {
                        if (verdi.Alias == "title" || verdi.Alias == "alt" || verdi.Alias == "src" || verdi.Alias == "umbracoWidth" || verdi.Alias == "umbracoHeight" || verdi.Alias == "umbracoExtension" || verdi.Alias == "createDate")
                        {
                            nyttBilde[verdi.Alias] = verdi.Value.Trim();
                        }
                    }
                    nyttBilde["x"] = 0;
                    nyttBilde["y"] = 0;

                    var med = Umbraco.Media(item_node.Id);
                    Umbraco.Web.Models.ImageCropDataSet bilde = (Umbraco.Web.Models.ImageCropDataSet)med.umbracoFile;

                    nyttBilde["src"] = bilde.Src;

                    int x = Convert.ToInt32(Convert.ToDouble(nyttBilde["umbracoWidth"]) * 0.5);
                    int y = Convert.ToInt32(Convert.ToDouble(nyttBilde["umbracoHeight"]) * 0.5);
                    nyttBilde["x"] = x;
                    nyttBilde["y"] = y;

                    if (bilde.FocalPoint != null)
                    {
                        double dLeft = Convert.ToDouble(bilde.FocalPoint.Left);
                        double dTop = Convert.ToDouble(bilde.FocalPoint.Top);
                        nyttBilde["focalx"] = Convert.ToDouble(Math.Round((Decimal)dLeft, 2));
                        nyttBilde["focaly"] = Convert.ToDouble(Math.Round((Decimal)dTop, 2));
                    }
                    Bilder.Add(nyttBilde);
                }
                dynNodeData["Media"] = Bilder;
            }
            if (child.GetProperty("ikon") != null)
            {
                string[] splitted = child.GetProperty("ikon").Value.ToString().Split(',');
                foreach (string split in splitted)
                {
                    //var mediaId = Udi.Parse(split).ToPublishedContent();
                    var mediaId = Udi.Parse(split).ToPublishedContent();
                    var item_node = new umbraco.MacroEngines.DynamicNode(mediaId);
                    var nyttBilde = new Dictionary<string, object>();

                    foreach (var verdi in item_node.PropertiesAsList)
                    {
                        if (verdi.Alias == "title" || verdi.Alias == "alt" || verdi.Alias == "urlName" || verdi.Alias == "src" || verdi.Alias == "umbracoWidth" || verdi.Alias == "umbracoHeight" || verdi.Alias == "umbracoExtension" || verdi.Alias == "createDate")
                        {
                            nyttBilde[verdi.Alias] = verdi.Value.Trim();
                        }
                    }
                    nyttBilde["x"] = 0;
                    nyttBilde["y"] = 0;

                    var med = Umbraco.Media(item_node.Id);
                    Umbraco.Web.Models.ImageCropDataSet bilde = (Umbraco.Web.Models.ImageCropDataSet)med.umbracoFile;

                    nyttBilde["src"] = bilde.Src;

                    int x = Convert.ToInt32(Convert.ToDouble(nyttBilde["umbracoWidth"]) * 0.5);
                    int y = Convert.ToInt32(Convert.ToDouble(nyttBilde["umbracoHeight"]) * 0.5);
                    nyttBilde["x"] = x;
                    nyttBilde["y"] = y;

                    if (bilde.FocalPoint != null)
                    {
                        double dLeft = Convert.ToDouble(bilde.FocalPoint.Left);
                        double dTop = Convert.ToDouble(bilde.FocalPoint.Top);
                        nyttBilde["focalx"] = Convert.ToDouble(Math.Round((Decimal)dLeft, 2));
                        nyttBilde["focaly"] = Convert.ToDouble(Math.Round((Decimal)dTop, 2));
                    }
                    Ikoner.Add(nyttBilde);
                }
                dynNodeData["ikoner"] = Ikoner;
            }
            if (child.GetProperty("vedlegg") != null)
            {
                string[] splitted = child.GetProperty("vedlegg").Value.ToString().Split(',');
                foreach (string split in splitted)
                {
                    //var mediaId = Udi.Parse(split).ToPublishedContent();
                    var mediaId = Udi.Parse(split).ToPublishedContent();
                    var item_node = new umbraco.MacroEngines.DynamicNode(mediaId);
                    var nyFil = new Dictionary<string, object>();

                    foreach (var verdi in item_node.PropertiesAsList)
                    {
                        if (verdi.Alias == "umbracoFile" || verdi.Alias == "umbracoExtension" || verdi.Alias == "createDate")
                        {
                            nyFil[verdi.Alias] = verdi.Value.Trim();
                        }
                    }

                    Filer.Add(nyFil);
                }
                dynNodeData["Filer"] = Filer;
            }

            if (child.GetProperty("tjenester") != null)
            {
                foreach (string uditjenestestring in child.GetProperty("tjenester").Value.ToString().Split(','))
                {
                    Tjenester.Add(single(new Node(Umbraco.GetIdForUdi(Udi.Parse(uditjenestestring))), false));
                }
                dynNodeData["Tjenester"] = Tjenester;
            }

           // if (child.GetProperty("nettskypakker") != null)
          //  {
                //foreach (string udipakkestreng in child.GetProperty("nettskypakker").Value.ToString().Split(','))
                //{
                //    Nettskypakker.Add(single(new Node(Umbraco.GetIdForUdi(Udi.Parse(udipakkestreng))), false));
                //}
                //dynNodeData["pakker"] = Nettskypakker;
          //  }

            if (child.Parent != null)
            {
                dynNodeData["Parent"] = child.Parent.UrlName;
            }
            dynNodeData["NodeTypeAlias"] = child.NodeTypeAlias;
            dynNodeData["NodeName"] = child.Name;
            dynNodeData["url"] = child.UrlName;
            dynNodeData["NodeId"] = child.Id;
            dynNodeData["OpprettetDato"] = child.CreateDate.ToString("dd.MM.yyyy");
            dynNodeData["OpprettetAv"] = child.WriterName;

            foreach (umbraco.NodeFactory.Property egenskap in child.Properties)
            {
                if (egenskap.Alias == "media")
                {
                }
                else if (egenskap.Alias == "tjenester")
                {
                }
                else
                {
                    dynNodeData[egenskap.Alias] = egenskap.Value.Trim();
                }

            }
            CacheItemPolicy policy = new CacheItemPolicy();
            policy.AbsoluteExpiration = DateTime.Now.AddDays(14); //14 dager
            cache.Set("NODE" + child.Id, dynNodeData, policy);
            return dynNodeData;
        }
        else
        {
            IDictionary node = (IDictionary)MemoryCache.Default.Get("NODE" + child.Id);
            Dictionary<string, object> newDict = new Dictionary<string, object>();

            foreach (object key in node.Keys)
            {
                newDict.Add(key.ToString(), node[key]);
            }
            return newDict;
        }
    }
}
