﻿using System;
using System.Collections.Generic;
using System.Web.Http;
using SiteServer.CMS.Caches;
using SiteServer.CMS.Caches.Content;
using SiteServer.CMS.Core;
using SiteServer.CMS.Database.Models;
using SiteServer.CMS.Plugin;
using SiteServer.CMS.Plugin.Impl;
using SiteServer.Plugin;
using SiteServer.Utils;

namespace SiteServer.API.Controllers.Home
{
    [RoutePrefix("home/contents")]
    public class HomeContentsController : ApiController
    {
        private const string Route = "";

        [HttpGet, Route(Route)]
        public IHttpActionResult List()
        {
            try
            {
                var rest = Request.GetAuthenticatedRequest();

                var siteId = Request.GetQueryInt("siteId");
                var channelId = Request.GetQueryInt("channelId");
                var page = Request.GetQueryInt("page");

                if (!rest.IsUserLoggin ||
                    !rest.UserPermissions.HasChannelPermissions(siteId, channelId,
                        ConfigManager.ChannelPermissions.ContentView))
                {
                    return Unauthorized();
                }

                var siteInfo = SiteManager.GetSiteInfo(siteId);
                if (siteInfo == null) return BadRequest("无法确定内容对应的站点");

                var channelInfo = ChannelManager.GetChannelInfo(siteId, channelId);
                if (channelInfo == null) return BadRequest("无法确定内容对应的栏目");

                var onlyAdminId = ((PermissionsImpl)rest.AdminPermissions).GetOnlyAdminId(siteId, channelId);

                var columns = ContentManager.GetContentColumns(siteInfo, channelInfo, false);
                var pluginIds = PluginContentManager.GetContentPluginIds(channelInfo);
                var pluginColumns = PluginContentManager.GetContentColumns(pluginIds);

                var pageContentInfoList = new List<IDictionary<string, object>>();
                var count = ContentManager.GetCount(siteInfo, channelInfo, onlyAdminId);

                var pages = Convert.ToInt32(Math.Ceiling((double)count / siteInfo.PageSize));
                if (pages == 0) pages = 1;

                if (count > 0)
                {
                    var offset = siteInfo.PageSize * (page - 1);
                    var limit = siteInfo.PageSize;

                    var pageContentIds = ContentManager.GetContentIdList(siteInfo, channelInfo, onlyAdminId, offset, limit);

                    var sequence = offset + 1;
                    foreach (var contentId in pageContentIds)
                    {
                        var contentInfo = ContentManager.GetContentInfo(siteInfo, channelInfo, contentId);
                        if (contentInfo == null) continue;

                        pageContentInfoList.Add(ContentManager.Calculate(sequence++, contentInfo, columns, pluginColumns));
                    }
                }

                var permissions = new
                {
                    IsAdd = rest.UserPermissions.HasChannelPermissions(siteInfo.Id, channelInfo.Id, ConfigManager.ChannelPermissions.ContentAdd) && channelInfo.IsContentAddable,
                    IsDelete = rest.UserPermissions.HasChannelPermissions(siteInfo.Id, channelInfo.Id, ConfigManager.ChannelPermissions.ContentDelete),
                    IsEdit = rest.UserPermissions.HasChannelPermissions(siteInfo.Id, channelInfo.Id, ConfigManager.ChannelPermissions.ContentEdit),
                    IsTranslate = rest.UserPermissions.HasChannelPermissions(siteInfo.Id, channelInfo.Id, ConfigManager.ChannelPermissions.ContentTranslate),
                    IsCheck = rest.UserPermissions.HasChannelPermissions(siteInfo.Id, channelInfo.Id, ConfigManager.ChannelPermissions.ContentCheck),
                    IsCreate = rest.UserPermissions.HasSitePermissions(siteInfo.Id, ConfigManager.WebSitePermissions.Create) || rest.UserPermissions.HasChannelPermissions(siteInfo.Id, channelInfo.Id, ConfigManager.ChannelPermissions.CreatePage),
                    IsChannelEdit = rest.UserPermissions.HasChannelPermissions(siteInfo.Id, channelInfo.Id, ConfigManager.ChannelPermissions.ChannelEdit)
                };

                return Ok(new
                {
                    Value = pageContentInfoList,
                    Count = count,
                    Pages = pages,
                    Permissions = permissions,
                    Columns = columns
                });
            }
            catch (Exception ex)
            {
                LogUtils.AddErrorLog(ex);
                return InternalServerError(ex);
            }
        }
    }
}
