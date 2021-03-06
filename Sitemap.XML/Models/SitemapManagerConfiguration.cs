﻿/* *********************************************************************** *
 * File   : SitemapManagerConfiguration.cs                Part of Sitecore *
 * Version: 1.0.0                                         www.sitecore.net *
 *                                                                         *
 *                                                                         *
 * Purpose: Class for getting config information from db and conf file     *
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
using System.Web;
using System.Xml;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Xml;
using Sitecore.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using Sitecore;
using Sitecore.Data.Fields;

namespace Sitemap.XML.Models
{
    public class SitemapManagerConfiguration
    {
        #region Fields

        #endregion

        #region Constructor

        public SitemapManagerConfiguration(string siteName)
        {
            Assert.IsNotNullOrEmpty(siteName, "siteName");
            SiteName = siteName;
        }

        #endregion

        #region Properties

        public static string XmlnsTpl
        {
            get
            {
                return GetValueByName("xmlnsTpl");
            }
        }

        public static string WorkingDatabase
        {
            get
            {
                return GetValueByName("database");
            }
        }

        private string sitemapPath { get; set; }
        public string SitemapConfigurationItemPath
        {
            get
            {
                if (String.IsNullOrWhiteSpace(sitemapPath))
                {
                    var site = Factory.GetSite(SiteName); // GetSite(SiteName);
                    sitemapPath = site.Properties["sitemapPath"];
                    if (string.IsNullOrWhiteSpace(sitemapPath))
                    {
                        if (site.Name == "publisher")
                            sitemapPath = GetValueByName("sitemapConfigurationItemPath");
                        else
                            sitemapPath = GetValueByName("sitemapConfigurationItemPath") + SiteName;
                    }
                }
                return sitemapPath;

            }
        }

        public bool HasWildcardItems
        {
            get
            {
                return GetValueByName("hasWildcardItems") == "true";
            }
        }

        public string WildcardRoutesPath
        {
            get
            {
                return GetValueByName("wildcardRoutesPath");
            }
        }

        public bool GenerateRobotsFile
        {
            get
            {
                string doGenerate = GetValueByName("generateRobotsFile");
                return !string.IsNullOrEmpty(doGenerate) && (doGenerate.ToLower() == "true" || doGenerate == "1");
            }
        }

        public string EnabledTemplates
        {
            get
            {
                return GetValueByNameFromDatabase(Constants.WebsiteDefinition.EnabledTemplatesFieldName);
            }
        }

        public bool CleanupBucketPath
        {
            get
            {
                return GetValueByNameFromDatabase(Constants.WebsiteDefinition.CleanupBucketPath) == "1";
            }
        }

        public string ServerUrl
        {
            get
            {
                var url = GetValueByNameFromDatabase(Constants.WebsiteDefinition.ServerUrlFieldName);
                return string.IsNullOrWhiteSpace(url) ? HttpContext.Current.Request.Url.Scheme + "://"
                    + Context.Site.Properties["hostname"] : url.Trim('/');
            }
        }

        public static bool IsProductionEnvironment
        {
            get
            {
                var production = GetValueByName("productionEnvironment");
                return !string.IsNullOrEmpty(production) && (production.ToLower() == "true" || production == "1");
            }
        }

        public string CustomStartPath
        {
            get
            {
                return GetCustomPathItem(Constants.WebsiteDefinition.CustomStartPath);

            }
        }

        public string SiteName { get; set; }

        public string FileName
        {
            get
            {
                return GetValueByNameFromDatabase(Constants.WebsiteDefinition.FileNameFieldName);
            }
        }

        #endregion properties

        #region Private Methods

        private static string GetValueByName(string name)
        {
            var result = string.Empty;

            foreach (XmlNode node in Factory.GetConfigNodes("sitemapVariables/sitemapVariable"))
            {
                if (XmlUtil.GetAttribute("name", node) != name) continue;
                result = XmlUtil.GetAttribute("value", node);
                break;
            }

            return result;
        }

        private string GetValueByNameFromDatabase(string name)
        {
            string result = string.Empty;

            Database db = Factory.GetDatabase(WorkingDatabase);
            if (db != null)
            {
                Item configItem = db.Items[SitemapConfigurationItemPath];
                if (configItem != null)
                {
                    result = configItem[name];
                }
            }

            return result;
        }

        /// <summary>
        /// It is specified by a field on the configuration item. This overrides the default site start path for a custom item on the content tree
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private string GetCustomPathItem(string item)
        {
            string result = string.Empty;

            Database db = Factory.GetDatabase(WorkingDatabase);
            if (db != null)
            {

                var configItem = db.Items[SitemapConfigurationItemPath];
                if (configItem != null)
                {
                    var reference = (ReferenceField)configItem.Fields[Constants.WebsiteDefinition.CustomStartPath];
                    if (reference != null && reference.TargetItem != null)
                    {
                        //return LinkManager.GetItemUrl(reference.TargetItem, new UrlOptions { Site = SiteContext.Current, SiteResolving = false, LanguageEmbedding = LanguageEmbedding.Never });
                        return reference.TargetItem.Paths.FullPath;
                    }
                }
            }

            return result;
        }

        #endregion

        #region Public Methods

        public static IEnumerable<string> GetSiteNames()
        {
            var sitemapXmlSystemRootId = Constants.SitemapModuleSettingsRootItemId;
            var configRoot = Factory.GetDatabase(WorkingDatabase).GetItem(sitemapXmlSystemRootId);
            if (configRoot == null) return null;

            var configs = configRoot.Children.Where(i => i.TemplateName != "Folder");
            if (!configs.Any()) return null;

            var siteNames = configs.Select(c => c.Name);
            return siteNames;
        }

        public static string GetServerUrl(string siteName)
        {
            string result = string.Empty;

            foreach (XmlNode node in Factory.GetConfigNodes("sitemapVariables/sites/site"))
            {

                if (XmlUtil.GetAttribute("name", node) == siteName)
                {
                    result = XmlUtil.GetAttribute("serverUrl", node);
                    break;
                }
            }

            return result;
        }

        #endregion
    }
}
