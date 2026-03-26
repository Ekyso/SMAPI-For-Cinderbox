using System;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace StardewModdingAPI.Web.Framework.Extensions;

/// <summary>Provides extensions for <see cref="IUrlHelper"/>.</summary>
public static class UrlHelperExtensions
{
    /// <param name="helper">The URL helper to extend.</param>
    extension(IUrlHelper helper)
    {
        /// <summary>Get a URL for an action method. Unlike <see cref="IUrlHelper.Action"/>, only the specified <paramref name="values"/> are added to the URL without merging values from the current HTTP request.</summary>
        /// <param name="action">The name of the action method.</param>
        /// <param name="controller">The name of the controller.</param>
        /// <param name="values">An object that contains route values.</param>
        /// <param name="absoluteUrl">Get an absolute URL instead of a server-relative path/</param>
        /// <returns>The generated URL.</returns>
        public string? PlainAction([AspMvcAction] string action, [AspMvcController] string controller, object? values = null, bool absoluteUrl = false)
        {
            // get route values
            RouteValueDictionary valuesDict = new(values);
            foreach (var value in helper.ActionContext.RouteData.Values)
                valuesDict.TryAdd(value.Key, null); // explicitly remove it from the URL

            // get relative URL
            string? url = helper.Action(action, controller, valuesDict);
            if (url == null && action.EndsWith("Async"))
                url = helper.Action(action[..^"Async".Length], controller, valuesDict);

            // get absolute URL
            if (absoluteUrl)
            {
                HttpRequest request = helper.ActionContext.HttpContext.Request;
                Uri baseUri = new($"{request.Scheme}://{request.Host}");
                url = new Uri(baseUri, url).ToString();
            }

            return url;
        }

        /// <summary>Convert a virtual (relative, starting with ~/) path to an application absolute path, and append a query argument to force browsers to re-download the asset if needed.</summary>
        /// <param name="url">The virtual path of the content.</param>
        public string ContentWithCacheBust(string url)
        {
            char delimiter = url.Contains('?') ? '&' : '?';

            return helper.Content($"{url}{delimiter}v={Program.CacheBustValue}");
        }
    }
}
