using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace LogAnalyzer
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            // Web API routes
            config.MapHttpAttributeRoutes();

            var matches = config.Formatters
                               .Where(f => f.SupportedMediaTypes
                               .Where(m => m.MediaType.ToString() == "application/xml"
                                   || m.MediaType.ToString() == "text/xml")
                               .Count() > 0).ToList();

            foreach (var match in matches)
                config.Formatters.Remove(match);

        }
    }
}
