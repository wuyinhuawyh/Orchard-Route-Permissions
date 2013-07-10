﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Lombiq.RoutePermissions.Models;
using Orchard.Caching;
using Orchard.ContentManagement;
using Orchard.Mvc;
using Orchard.Mvc.Filters;
using Orchard.Security;
using System.Text.RegularExpressions;
using Orchard.ContentPermissions.Models;

namespace Lombiq.RoutePermissions.Filters
{
    public class UrlPermissionFilter : FilterProvider, IAuthorizationFilter
    {
        private readonly IContentManager _contentManager;
        private readonly ICacheManager _cacheManager;
        private readonly ISignals _signals;
        private readonly IHttpContextAccessor _hca;
        private readonly IAuthorizer _authorizer;


        public UrlPermissionFilter(
            IContentManager contentManager, 
            ICacheManager cacheManager,
            ISignals signals,
            IHttpContextAccessor hca,
            IAuthorizer authorizer)
        {
            _contentManager = contentManager;
            _cacheManager = cacheManager;
            _signals = signals;
            _hca = hca;
            _authorizer = authorizer;
        }


        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var urlPatterns = _cacheManager.Get("Lombiq.RoutePermissions.UrlPatterns", ctx =>
                {
                    ctx.Monitor(_signals.When(Signals.UrlPatternsSignal));

                    return _contentManager
                        .Query(ContentTypes.UrlPermission)
                        .Join<UrlPermissionPartRecord>()
                        .List<UrlPermissionPart>()
                        .Select(urlPermission => new UrlPatternDefinition
                        {
                            UrlPattern = urlPermission.UrlPattern,
                            Priority = urlPermission.Priority,
                            ContentItemId = urlPermission.ContentItem.Id
                        })
                        .OrderByDescending(pattern => pattern.Priority);
                });

            var url = _hca.Current().Request.Url.PathAndQuery;
            foreach (var pattern in urlPatterns)
            {
                if (Regex.IsMatch(url, pattern.UrlPattern))
                {
                    var item = _contentManager.Get(pattern.ContentItemId, VersionOptions.Published, new QueryHints().ExpandParts<ContentPermissionsPart>());
                    if (item == null) continue;
                    if (!_authorizer.Authorize(Orchard.Core.Contents.Permissions.ViewContent, item))
                    {
                        filterContext.Result = new HttpUnauthorizedResult();
                    }
                    return; // A pattern matched, no need to check anything else.
                }
            }
        }


        private class UrlPatternDefinition
        {
            public string UrlPattern { get; set; }
            public int Priority { get; set; }
            public int ContentItemId { get; set; }
        }
    }
}