using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using umbraco.NodeFactory;
using Umbraco.Web.WebApi;
using Newtonsoft.Json;
using Skybrud.WebApi.Json;
using Umbraco.Core.Services;
using Umbraco.Core.Models;
using Umbraco.Core;
using Umbraco.Web.Extensions;
using Umbraco.Web;

[JsonOnlyConfiguration]
public class JSONController : UmbracoApiController
{
    // GET api/ContentController/get/id
    public object GetAll(string id)
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

        // Om den skal se etter innholdstype eller ikke
        if (!isNumeric)
        {
            var contentType = ApplicationContext.Services.ContentTypeService.GetContentType(id);
            var contentPages = ApplicationContext.Services.ContentService.GetContentOfContentType(contentType.Id).OrderBy(x => x.CreateDate).Where(page => page.Trashed == false && page.Published == true);

            foreach (var item in contentPages)
            {
                Node childNode = new Node(item.Id);
                nodeListe.Add(single(childNode));
            }
        }
        else
        {
            // Om noden har barn
            if (inputNode.Children.Count > 0)
            {
                var contentPages = ApplicationContext.Services.ContentService.GetChildren(inputNode.Id).OrderBy(x => x.CreateDate).Where(page => page.Trashed == false && page.Published == true);

                foreach (var item in contentPages)
                {
                    Node childNode = new Node(item.Id);
                    nodeListe.Add(single(childNode));
                }
            }
            else
            {
                return single(inputNode);
            }
        }
        return nodeListe;
    }
    // Bruk Get om noden har barn men man fortsatt vil vise info
    public object Get(int id)
    {
        Node child = new Node(id);

        if (child == null || child.Name == null)
        {
            throw new HttpResponseException(HttpStatusCode.NotFound);
        }

        return single(child);
    }
    private object single(Node child)
    {
        var dynNodeData = new Dictionary<string, object>();
        var Bilder = new List<object> { };
        var Filer = new List<object> { };

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
                    if (verdi.Alias == "urlName" || verdi.Alias == "src" || verdi.Alias == "umbracoWidth" || verdi.Alias == "umbracoHeight"  || verdi.Alias == "umbracoExtension" || verdi.Alias == "createDate")
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
                    if (verdi.Alias == "urlName" || verdi.Alias == "umbracoFile" || verdi.Alias == "umbracoExtension" || verdi.Alias == "createDate")
                    {
                        nyFil[verdi.Alias] = verdi.Value.Trim();
                    }
                }

                Filer.Add(nyFil);
            }
            dynNodeData["Filer"] = Filer;
        }
        dynNodeData["NodeName"] = child.Name;
        dynNodeData["NodeId"] = child.Id;
        dynNodeData["OpprettetDato"] = child.CreateDate.ToString("dd.MM.yyyy");
        dynNodeData["OpprettetAv"] = child.WriterName;

        foreach (umbraco.NodeFactory.Property egenskap in child.Properties)
        {
            if (egenskap.Alias != "media")
            {
                dynNodeData[egenskap.Alias] = egenskap.Value.Trim();
            }
        }
        return dynNodeData;
    }
}
