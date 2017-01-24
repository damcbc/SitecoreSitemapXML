using System;
using System.Collections.Generic;
using System.Linq;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;

namespace Sitemap.XML.Models
{
    public class SitemapPageManager
    {
        

        
        private static SitemapManagerConfiguration sm;
        private static SitemapManagerConfiguration Config
        {
            get
            {
                if(sm == null)
                    sm = new SitemapManagerConfiguration(Context.Site.Name);
                return sm;
            }
        }

        private static Item siteConfig { get;  set; }

        public static Item SiteConfig
        {
            get
            {
                if (siteConfig == null)
                {
                    var database = Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);
                    database.GetItem(Config.SitemapConfigurationItemPath);
                }
                return siteConfig;
            }
        }

        public static Database Db
        {
            get
            {
                var database = Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);
                return database;
            }
        }

        /// <summary>
        /// List of All excluded Items
        /// </summary>
        /// <param name="siteConfig"></param>
        /// <returns></returns>
        public static List<Item> ExcludedItems()
        {

            var excludedItems = new List<Item>();
            if (SiteConfig != null)
            {
                MultilistField excluded = SiteConfig.Fields[Settings.GetSetting("Sitemap.XML.Fields.ExcludeItemFromSitemap", "Exclude From Sitemap")];
                if (excluded != null)
                {
                    //builds list of excluded items
                    excludedItems = excluded.GetItems().ToList();
                }
            }

            return excludedItems;
        }

        public static List<Item> GetWildCardMapping(Item item)
        {
            //It will look on the shared definitions for items that have the current "item" set as the parent and get the mapping on the children
            var mappingItems = new List<Item>();
            //var sharedDefinitions = Db.SelectItems(string.Format("fast:{0}/*", SiteConfig.Paths.FullPath));

            //var x = GetContentLocation(item);
            //var y = GetSharedLocationParent(item)
            mappingItems = GetSharedWildCard(item);

            return mappingItems;
        }

        /// <summary>
        /// List of all wildcard items of the site
        /// </summary>
        /// <returns></returns>
        public static List<Item> WildCardItems()
        {
            if (!IsChildWildCardEnabled())
                return null;
            var wildcardItems = new List<Item>();
            var route = Context.Database.GetItem(Config.WildcardRoutesPath);
            if (route != null)
            {
                foreach (Item routeChild in route.Children)
                {
                    MultilistField routeItems = routeChild.Fields["Items"];
                    if (routeItems != null)
                    {
                        var list = routeItems.GetItems();
                        if(list != null)
                            wildcardItems.AddRange(list.ToList());
                    }
                }
            }
            return wildcardItems;
        }


        public static bool IsChildrenWildcard(Item item)
        {
            if (item == null)
                return false;

            var wildcards = WildCardItems();
            if (wildcards != null && wildcards.Any())
            {
                foreach (Item itemChild in item.Children)
                {
                    if (wildcards.Select(c => c.ID.ToString()).Contains(itemChild.ID.ToString()))
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        #region Private Methods

        private static IEnumerable<Item> GetSharedContentDefinitions()
        {
            var siteNode = GetContextSiteDefinitionItem();
            if (siteNode == null || string.IsNullOrWhiteSpace(siteNode.Name)) return null;

            var sharedDefinitions = siteNode.Children;
            return sharedDefinitions;
        }

        private static Item GetContextSiteDefinitionItem()
        {
            var database = Context.Database;
#if DEBUG
            database = Factory.GetDatabase("master");
#endif
            var sitemapModuleItem = database.GetItem(Constants.SitemapModuleSettingsRootItemId);
            var contextSite = Context.GetSiteName().ToLower();
            if (!sitemapModuleItem.Children.Any()) return null;
            var siteNode = sitemapModuleItem.Children.FirstOrDefault(i => i.Key == contextSite);
            return siteNode;
        }

        #endregion

        public static bool IsUnderContent(Item item)
        {
            return Context.Database.GetItem(Context.Site.StartPath).Axes.IsAncestorOf(item);
        }

        public static bool IsShared(Item item)
        {
            var sharedDefinitions = GetSharedContentDefinitions();
            if (sharedDefinitions == null) return false;
            var sharedItemContentRoots =
                sharedDefinitions.Select(i => ((DatasourceField)i.Fields[Constants.SharedContent.ParentItemFieldName]).TargetItem).ToList();
            if (!sharedItemContentRoots.Any()) return false;

            //adding check for null reference exception
            return sharedItemContentRoots.Where(i => i != null).Any(i => i.ID == item.ID);
        }

        public static bool IsChildWildCardEnabled()
        {
            return Config.HasWildcardItems;
        }

        public static bool SitemapDefinitionExists()
        {
            var sitemapModuleSettingsItem = Context.Database.GetItem(Constants.SitemapModuleSettingsRootItemId);
            var siteDefinition = sitemapModuleSettingsItem.Children[Context.Site.Name];
            return siteDefinition != null;
        }

        public static Item GetContentLocation(Item item)
        {
            var sharedNodes = GetSharedContentDefinitions();
            var contentParent = sharedNodes
                .Where(n => ((DatasourceField)n.Fields[Constants.SharedContent.ContentLocationFieldName]).TargetItem.Axes.IsAncestorOf(item))
                .Select(n => ((DatasourceField)n.Fields[Constants.SharedContent.ContentLocationFieldName]).TargetItem)
                .FirstOrDefault();
            return contentParent;
        }

        public static bool IsChildUnderSharedLocation(Item child)
        {
            var sharedNodes = GetSharedContentDefinitions();
            var sharedContentLocations = sharedNodes.Select(n => ((DatasourceField)n.Fields[Constants.SharedContent.ContentLocationFieldName]).TargetItem);
            var isUnderShared = sharedContentLocations.Any(l => l.Axes.IsAncestorOf(child));
            return isUnderShared;
        }

        public static Item GetSharedLocationParent(Item child)
        {
            var sharedNodes = GetSharedContentDefinitions();
            var parent = sharedNodes
                .Where(n => ((DatasourceField)n.Fields[Constants.SharedContent.ContentLocationFieldName]).TargetItem.Axes.IsAncestorOf(child))
                .Select(n => ((DatasourceField)n.Fields[Constants.SharedContent.ParentItemFieldName]).TargetItem)
                .FirstOrDefault();
            return parent;
        }

        public static List<Item> GetSharedWildCard(Item child)
        {
            var list = new List<Item>();
            var sharedNodes = GetSharedContentDefinitions();
            
            var wildcard = sharedNodes.FirstOrDefault(n => ((DatasourceField)n.Fields[Constants.SharedContent.ParentItemFieldName]).TargetItem != null 
            && ((DatasourceField)n.Fields[Constants.SharedContent.ParentItemFieldName]).TargetItem.ID.ToString() == child.ID.ToString());
            if (wildcard != null && wildcard.Fields[Constants.SharedContent.ContentLocationFieldName] != null && wildcard.Fields[Constants.SharedContent.ContentLocationFieldName].Value != "")
            {
                var target = (DatasourceField) wildcard.Fields[Constants.SharedContent.ContentLocationFieldName];
                if (target != null && target.TargetItem != null)
                {
                    foreach (Item children in target.TargetItem.Children)
                    {
                        list.Add(children);
                    }
                }
            }

            return list;
        }

        public static bool IsEnabledTemplate(Item item)
        {
            if (item == null)
                return false;

            var config = new SitemapManagerConfiguration(Context.GetSiteName());
            return config.EnabledTemplates.ToLower().Contains(item.TemplateID.ToGuid().ToString());
        }

        public static bool IsExcludedItem(Item item)
        {
            //keeps consistency wit items being excluded on the item level
            if (item[Settings.GetSetting("Sitemap.XML.Fields.ExcludeItemFromSitemap", "Exclude From Sitemap")] == "1")
            {
                return true;
            }

            //global exclude
            var excludedItems = ExcludedItems();
            if (excludedItems.Any())
            {
                //filters for only those not excluded
                return (excludedItems.Select(e => e.ID).Contains(item.ID));

            }

            return false;
        }

        public static bool ContainsItemsToShow(IEnumerable<Item> items)
        {
            return items == null
                ? false
                : items.Any() && items.Any(IsEnabledTemplate) && items.Count(IsExcludedItem) < items.Count();
        }

        
    }
}