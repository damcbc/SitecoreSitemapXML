/* *********************************************************************** *
 * File   : SitemapManager.cs                             Part of Sitecore *
 * Version: 1.0.0                                         www.sitecore.net *
 *                                                                         *
 *                                                                         *
 * Purpose: Manager class what contains all main logic                     *
 *                                                                         *
 * Copyright (C) 1999-2009 by Sitecore A/S. All rights reserved.           *
 *                                                                         *
 * This work is the property of:                                           *
 *                                                                         *
 *        Sitecore A/S                                                     *
 *        Meldahlsgade 5, 4.                                               *
 *        1613 Copenhagen V.                                               *
 *        Denmark                                                          *
 *                                                                         *
 * This is a Sitecore published work under Sitecore's                      *
 * shared source license.                                                  *
 *                                                                         *
 * *********************************************************************** */

using System;
using System.Web.UI;
using Sitecore;
using Sitecore.Buckets.Managers;
using Sitecore.Configuration;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Sites;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Sitecore.Buckets.Extensions;

namespace Sitemap.XML.Models
{
    public class SitemapManager
    {
        #region Fields 

        private readonly SitemapManagerConfiguration _config;

        #endregion

        #region Constructor

        public SitemapManager(SitemapManagerConfiguration config)
        {
            Assert.IsNotNull(config, "config");
            _config = config;
            if (!string.IsNullOrWhiteSpace(_config.FileName))
            {
                BuildSiteMap();
            }
        }

        public SitemapManager(SitemapManagerConfiguration config, bool refreshSitemap)
        {
            Assert.IsNotNull(config, "config");
            _config = config;
            if (refreshSitemap  && !string.IsNullOrWhiteSpace(_config.FileName))
            {
                BuildSiteMap();
            }
        }

        #endregion

        #region Properties

        public Database Db
        {
            get
            {
                Database database = Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);
                return database;
            }
        }

        #endregion

        

        private string BuildSitemapXML(List<SitemapItem> items, Site site)
        {
            XmlDocument doc = new XmlDocument();

            XmlNode declarationNode = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.AppendChild(declarationNode);
            XmlNode urlsetNode = doc.CreateElement("urlset");
            XmlAttribute xmlnsAttr = doc.CreateAttribute("xmlns");
            xmlnsAttr.Value = SitemapManagerConfiguration.XmlnsTpl;
            urlsetNode.Attributes.Append(xmlnsAttr);

            doc.AppendChild(urlsetNode);


            foreach (var itm in items)
            {
                doc = this.BuildSitemapItem(doc, itm, site);
            }

            return doc.OuterXml;
        }

        private XmlDocument BuildSitemapItem(XmlDocument doc, SitemapItem item, Site site)
        {
            XmlNode urlsetNode = doc.LastChild;

            XmlNode urlNode = doc.CreateElement("url");
            urlsetNode.AppendChild(urlNode);

            XmlNode locNode = doc.CreateElement("loc");
            urlNode.AppendChild(locNode);
            locNode.AppendChild(doc.CreateTextNode(item.Location));

            XmlNode lastmodNode = doc.CreateElement("lastmod");
            urlNode.AppendChild(lastmodNode);
            lastmodNode.AppendChild(doc.CreateTextNode(item.LastModified));

            if (!string.IsNullOrWhiteSpace(item.ChangeFrequency))
            {
                XmlNode changeFrequencyNode = doc.CreateElement("changefreq");
                urlNode.AppendChild(changeFrequencyNode);
                changeFrequencyNode.AppendChild(doc.CreateTextNode(item.ChangeFrequency));
            }

            if (!string.IsNullOrWhiteSpace(item.Priority))
            {
                var priorityNode = doc.CreateElement("priority");
                urlNode.AppendChild(priorityNode);
                priorityNode.AppendChild(doc.CreateTextNode(item.Priority));
            }

            return doc;
        }

        private void SubmitEngine(string engine, string sitemapUrl)
        {
            //Check if it is not localhost because search engines returns an error
            if (!sitemapUrl.Contains("http://localhost"))
            {
                string request = string.Concat(engine, SitemapItem.HtmlEncode(sitemapUrl));

                System.Net.HttpWebRequest httpRequest =
                    (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(request);
                try
                {
                    System.Net.WebResponse webResponse = httpRequest.GetResponse();

                    System.Net.HttpWebResponse httpResponse = (System.Net.HttpWebResponse)webResponse;
                    if (httpResponse.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        Log.Error(string.Format("Cannot submit sitemap to \"{0}\"", engine), this);
                    }
                }
                catch
                {
                    Log.Warn(string.Format("The serachengine \"{0}\" returns an 404 error", request), this);
                }
            }
        }

        public string GetRootPath(out Item root)
        {
            SiteContext siteContext = Factory.GetSite(_config.SiteName);

            string rootPath = siteContext.StartPath;
            var siteConfig = new SitemapManagerConfiguration(_config.SiteName);

            //overrides the startpath from the root of the site definition
            if (!string.IsNullOrWhiteSpace(siteConfig.CustomStartPath))
                rootPath = siteConfig.CustomStartPath;

            var database = Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);

            root = database.GetItem(rootPath);
            return rootPath;
        }

        private void BuildSiteMap()
        {
            Site site = Sitecore.Sites.SiteManager.GetSite(_config.SiteName);

            Item root;
            var rootPath = GetRootPath(out root);

            List<SitemapItem> items = GetSitemapItems(rootPath);

            string fullPath = MainUtil.MapPath(string.Concat("/", _config.FileName));
            string xmlContent = this.BuildSitemapXML(items, site);

            StreamWriter strWriter = new StreamWriter(fullPath, false);
            strWriter.Write(xmlContent);
            strWriter.Close();
        }

        


        private List<SitemapItem> GetSitemapItems(string rootPath)
        {
            string disTpls = _config.EnabledTemplates;

            Database database = Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);

            Item contentRoot = database.Items[rootPath];

            //Sitemap site configuration item
            var siteConfig = database.GetItem(_config.SitemapConfigurationItemPath);

            var excludedItems = SitemapPageManager.ExcludedItems();
            

            IEnumerable<Item> descendants;
            List<Item> descendantsFiltered = new List<Item>();
            Sitecore.Security.Accounts.User user = Sitecore.Security.Accounts.User.FromName(Constants.SitemapParserUser, true);
            using (new Sitecore.Security.Accounts.UserSwitcher(user))
            {
                //gets all the descendants from the root item
                descendants = contentRoot.Axes.GetDescendants();

                //Possibility of exclude from a configuration field on the items
                descendants =
                    descendants.Where(
                        i =>
                            i[
                                Settings.GetSetting("Sitemap.XML.Fields.ExcludeItemFromSitemap", "Exclude From Sitemap")] != "1");

                //Global exclude
                if (excludedItems.Any())
                {
                    //filters for only those not excluded
                    foreach (var descendant in descendants)
                    {
                        if (excludedItems.Select(e => e.ID).Contains(descendant.ID) == false)
                        {
                            descendantsFiltered.Add(descendant);
                        }
                    }
                }
                else
                {
                    descendantsFiltered = descendants.ToList();
                }
            }

            // getting shared content
            var sharedModels = new List<SitemapItem>();
            var sharedDefinitions = Db.SelectItems(string.Format("fast:{0}/*", _config.SitemapConfigurationItemPath));
            var site = Factory.GetSite(_config.SiteName);
            var enabledTemplates = BuildListFromString(disTpls, '|');
            foreach (var sharedDefinition in sharedDefinitions)
            {
                if (string.IsNullOrWhiteSpace(sharedDefinition[Constants.SharedContent.ContentLocationFieldName]) ||
                    string.IsNullOrWhiteSpace(sharedDefinition[Constants.SharedContent.ParentItemFieldName]))
                    continue;

                var parentItem = ((DatasourceField)sharedDefinition.Fields[Constants.SharedContent.ParentItemFieldName]).TargetItem;
                if(parentItem!= null && contentRoot.Axes.GetDescendants().Any(c=> c.ID == parentItem.ID))
                {
                    var contentLocation = ((DatasourceField)sharedDefinition.Fields[Constants.SharedContent.ContentLocationFieldName]).TargetItem;
                    var sharedItems = new List<Item>();
                    if (BucketManager.IsBucket(contentLocation))
                    {
                        var index = ContentSearchManager.GetIndex(new SitecoreIndexableItem(contentLocation));
                        using (var searchContext = index.CreateSearchContext())
                        {
                            var searchResultItem =
                                searchContext.GetQueryable<SearchResultItem>()
                                    .Where(item => item.Paths.Contains(contentLocation.ID) && item.ItemId != contentLocation.ID)
                                    .ToList();
                            var result = searchResultItem.Select(i => i.GetItem());
                            
                            MultilistField refFields = sharedDefinition.Fields[Constants.SharedContent.Filters];
                            if (refFields != null && refFields.List != null)
                            {
                                var output = new List<Item>();
                                string urlParamsToParse = refFields.List.ToString();
                                NameValueCollection nameValueCollection = Sitecore.Web.WebUtil.ParseUrlParameters(urlParamsToParse);
                                
                                foreach (var nv in nameValueCollection)
                                {
                                    Item filter = database.GetItem(nameValueCollection[nv.ToString()]);
                                    output = result.Where(x =>
                                        x.Fields[nv.ToString()] != null &&
                                        x.Fields[nv.ToString()].Value != String.Empty
                                        && ((MultilistField) x.Fields[nv.ToString()]) != null
                                        && ((MultilistField) x.Fields[nv.ToString()]).List != null
                                        && ((MultilistField) x.Fields[nv.ToString()]).List.Any(
                                            i => i.ToString() == filter.ID.ToString())).ToList();
                                    
                                }
                                
                                sharedItems.AddRange(output);
                            }
                            else
                            {
                                sharedItems.AddRange(result);
                            }
                            
                        }
                    }
                    else
                    {
                        sharedItems.AddRange(contentLocation.Axes.GetDescendants());
                    }

                    var cleanedSharedItems = from itm in sharedItems
                                             where itm.Template != null && enabledTemplates.Select(t => t.ToLower()).Contains(itm.Template.ID.ToString().ToLower())
                                             select itm;
                    var sharedSitemapItems = cleanedSharedItems.Select(i => new SitemapItem(i, site, parentItem));
                    sharedModels.AddRange(sharedSitemapItems);
                }
                
            }

            var sitemapItems = descendantsFiltered;
            sitemapItems.Insert(0, contentRoot);

            var selected = from itm in sitemapItems
                           where itm.Template != null && enabledTemplates.Contains(itm.Template.ID.ToString())
                           select itm;

            var selectedModels = selected.Select(i => new SitemapItem(i, site, null)).ToList();
            selectedModels.AddRange(sharedModels);
            selectedModels = selectedModels.OrderBy(u => u.Priority).Take(int.Parse(Settings.GetSetting("Sitemap.XML.UrlLimit", "1000"))).ToList();
            return selectedModels;
        }

        private static List<string> BuildListFromString(string str, char separator)
        {
            var enabledTemplates = str.Split(separator);
            var selected = from dtp in enabledTemplates
                           where !string.IsNullOrEmpty(dtp)
                           select dtp;

            var result = selected.ToList();

            return result;
        }


        

        #region Public Members

        public string BuildSiteMapForHandler()
        {
            var site = SiteManager.GetSite(Context.Site.Name);
            Item root;
            string rootPath = GetRootPath(out root);

            var items = GetSitemapItems(rootPath);

            string xmlContent = this.BuildSitemapXML(items, site);
            return xmlContent;
        }
        
        public bool SubmitSitemapToSearchenginesByHttp()
        {
            if (!SitemapManagerConfiguration.IsProductionEnvironment)
                return false;

            bool result = false;
            Item sitemapConfig = Db.Items[_config.SitemapConfigurationItemPath];

            if (sitemapConfig != null)
            {
                //TODO: URL
                string engines = sitemapConfig.Fields[Constants.WebsiteDefinition.SearchEnginesFieldName].Value;
                var filePath = !_config.ServerUrl.EndsWith("/")
                            ? _config.ServerUrl + "/" + _config.FileName
                            : _config.ServerUrl + _config.FileName;
                foreach (string id in engines.Split('|'))
                {
                    Item engine = Db.Items[id];
                    if (engine != null)
                    {
                        string engineHttpRequestString = engine.Fields[Constants.SitemapSubmissionUriFieldName].Value;
                        SubmitEngine(engineHttpRequestString, filePath);
                    }
                }
                result = true;
            }

            return result;
        }

        public void RegisterSitemapToRobotsFile()
        {
            if (string.IsNullOrWhiteSpace(_config.FileName)) return;
            string robotsPath = MainUtil.MapPath(string.Concat("/", Constants.RobotsFileName));
            StringBuilder sitemapContent = new StringBuilder(string.Empty);
            if (File.Exists(robotsPath))
            {
                StreamReader sr = new StreamReader(robotsPath);
                sitemapContent.Append(sr.ReadToEnd());
                sr.Close();
            }

            StreamWriter sw = new StreamWriter(robotsPath, false);
            string sitemapLine = string.Concat("Sitemap: ", _config.FileName);
            if (!sitemapContent.ToString().Contains(sitemapLine))
            {
                sitemapContent.AppendLine(sitemapLine);
            }
            sw.Write(sitemapContent.ToString());
            sw.Close();
        }

        #endregion
    }
}